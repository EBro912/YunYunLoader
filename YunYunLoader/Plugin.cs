using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using App;
using Cysharp.Threading.Tasks;
using ILib.ServInject;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Networking;
using YYNoteEditor;
using Newtonsoft.Json;

namespace YunYunLoader
{
    [BepInPlugin("YunYunLoader", "YunYunLoader", "1.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony Harmony = new Harmony("YunYunLoader");

        internal static ManualLogSource Log;
        
        internal static readonly Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();
        internal static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
        internal static readonly Dictionary<string, ModdedScoreData> ModdedSongs = new Dictionary<string, ModdedScoreData>();

        internal static bool IsModdedSongResult;
        internal static bool SavedIsPlayedPost;
        internal static bool SavedIsRankingEntry;

        private static readonly string SongsPath = Paths.GameRootPath + "\\Songs";
        private static readonly string ExtractedZipsPath = Path.Combine(Path.GetTempPath(), "YunYunLoader");

        private void Awake()
        {
            Log = Logger;

            Harmony.PatchAll();

            if (!Directory.Exists(SongsPath))
            {
                Directory.CreateDirectory(SongsPath);
                Logger.LogWarning("Created Songs directory at " + SongsPath);
            }

            Logger.LogInfo("YunYunLoader loaded!");
        }

        async UniTaskVoid Start()
        {
            await LoadCustomSongs();
            
            LocalizedStringDatabase db = LocalizationSettings.StringDatabase;
            PopulateScoreDataTable(db.GetTable("ScoreData"));
            // hook our table postprocessor into the localization system, the base game doesn't use it
            db.TablePostprocessor = new ScoreDataTablePostprocessor(db.TablePostprocessor);
        }

        internal static async UniTask<bool> LoadCustomSongs()
        {
            ModdedSongs.Clear();
            LoadedAudioClips.Clear();
            LoadedSprites.Clear();

            if (Directory.Exists(ExtractedZipsPath))
                Directory.Delete(ExtractedZipsPath, true);
            Directory.CreateDirectory(ExtractedZipsPath);

            int songsAvailable = 0;
            int songsLoaded = 0;

            foreach (string dir in Directory.GetDirectories(SongsPath))
            {
                songsAvailable++;
                if (await LoadSongFromDirectory(dir))
                    songsLoaded++;
            }

            foreach (string zipPath in Directory.GetFiles(SongsPath, "*.zip"))
            {
                songsAvailable++;
                string extractDir = Path.Combine(ExtractedZipsPath, Path.GetFileNameWithoutExtension(zipPath));
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, extractDir);
                }
                catch
                {
                    Log.LogError("Failed to extract zip " + zipPath);
                    continue;
                }
                if (await LoadSongFromDirectory(ResolveSongDirectory(extractDir)))
                    songsLoaded++;
            }

            Log.LogInfo($"Loaded {LoadedAudioClips.Count} audio clips.");
            Log.LogInfo($"Loaded {LoadedSprites.Count} custom sprites.");
            Log.LogInfo($"Loaded {songsLoaded}/{songsAvailable} custom songs!");

            // return true if everything loaded cleanly
            return songsLoaded == songsAvailable;
        }

        // support both flat and nested zips (like the ones exported from YunYunEditor)
        private static string ResolveSongDirectory(string extractDir)
        {
            if (File.Exists(Path.Combine(extractDir, "song.json")))
                return extractDir;

            string[] subDirs = Directory.GetDirectories(extractDir);
            if (subDirs.Length == 1 && File.Exists(Path.Combine(subDirs[0], "song.json")))
                return subDirs[0];

            return extractDir;
        }

        private static async UniTask<bool> LoadSongFromDirectory(string dir)
        {
            if (!File.Exists(dir + "\\song.json"))
            {
                Log.LogWarning("No song.json found in " + dir + ", skipping!");
                return false;
            }
            ModdedScoreData? data;
            try
            {
                string json = await File.ReadAllTextAsync(dir + "\\song.json");
                data = JsonConvert.DeserializeObject<ModdedScoreData>(json);
            }
            catch
            {
                Log.LogError("Failed to parse song.json in " + dir);
                return false;
            }

            if (data is null)
            {
                Log.LogError("Failed to parse song.json in " + dir);
                return false;
            }

            bool failure = false;
            foreach (FieldInfo f in typeof(ModdedScoreData).GetFields())
            {
                if (f.GetValue(data) is null)
                {
                    Log.LogError("Missing field " + f.Name + " in song.json in " + dir);
                    failure = true;
                }
            }
            if (failure || data.ID is null)
                return false;

            if (ModdedSongs.ContainsKey(data.ID))
            {
                Log.LogWarning("Duplicate song ID " + data.ID + " in " + dir + ", skipping!");
                return false;
            }

            AudioClip? clip = await LoadOggAsync(dir + "\\" + data.Audio);
            if (clip is null)
                return false;

            if (!string.IsNullOrEmpty(data.Icon))
            {
                Texture2D? texture = await LoadTextureAsync(dir + "\\" + data.Icon);
                if (texture is null)
                {
                    Log.LogWarning("Failed to load icon " + data.Icon + " from song.json in " + dir + ", skipping!");
                }
                else
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    if (!LoadedSprites.TryAdd(data.ID, sprite))
                    {
                        Log.LogWarning(data.ID + " already has a loaded sprite, skipping!");
                    }
                }
            }

            if (!LoadedAudioClips.TryAdd(data.ID, clip))
            {
                Log.LogWarning(data.ID + " already has a loaded audio, skipping!");
            }
            foreach (ModdedLevelData l in data.Levels!)
            {
                ScoreData? score;
                try
                {
                    string json = await File.ReadAllTextAsync(dir + "\\" + l.Path);
                    score = JsonConvert.DeserializeObject<ScoreData>(json);
                }
                catch
                {
                    Log.LogError("Failed to parse song data " + l.Path + " in " + dir);
                    continue;
                }

                if (score == null)
                {
                    Log.LogError("Failed to parse song data " + l.Path + " in " + dir);
                    continue;
                }

                l.ID = Path.GetFileNameWithoutExtension(l.Path);
                l.Data = score;
            }
            ModdedSongs[data.ID] = data;
            return true;
        }

        internal static void PopulateScoreDataTable(StringTable table)
        {
            // modded songs only have one language (for now), so just use whatever it is every time regardless of locale
            foreach (ModdedScoreData data in ModdedSongs.Values)
            {
                table.AddEntry(data.ID, data.Title);
                table.AddEntry(data.ID + "_ARTIST", data.Artist);
                table.AddEntry(data.ID + "_LISTARTIST", data.ListArtist);
                table.AddEntry(data.ID + "_LYRICS", data.Lyricist);
                table.AddEntry(data.ID + "_COMP", data.Composer);
                table.AddEntry(data.ID + "_ARRAN", data.Arranger);

                if (data.Levels == null)
                    continue;

                foreach (ModdedLevelData l in data.Levels)
                {
                    if (!string.IsNullOrEmpty(l.ID))
                        table.AddEntry(l.ID + "_EDITOR", l.Editor);
                }
            }
        }

        private static async UniTask<AudioClip?> LoadOggAsync(string path)
        {
            string uri = Utility.ConvertToWWWFormat(path);
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
            var operation = www.SendWebRequest();
            while (!operation.isDone)
                await UniTask.Yield();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Log.LogError("Failed to load .ogg file from " + path);
                return null;
            }
            
            return DownloadHandlerAudioClip.GetContent(www);
        }
        
        private static async UniTask<Texture2D?> LoadTextureAsync(string path)
        {
            string uri = Utility.ConvertToWWWFormat(path);
            using UnityWebRequest www = UnityWebRequestTexture.GetTexture(uri);
            var operation = www.SendWebRequest();
            while (!operation.isDone)
                await UniTask.Yield();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Log.LogError("Failed to load .png/.jpg file from " + path);
                return null;
            }
            
            return DownloadHandlerTexture.GetContent(www);
        }

        public static async UniTask Notify(string text)
        {
            await ServInjector.Resolve<ISystemUI>().Notify(text);
        }
        
        public static async UniTask<bool> Confirm(string text)
        {
            return await ServInjector.Resolve<ISystemUI>().Confirm(text);
        }
        
        internal static bool IsModdedSong(string? musicId)
        {
            return musicId != null && ModdedSongs.ContainsKey(musicId);
        }
        
        internal static bool IsModdedScoreId(string scoreId)
        {
            if (string.IsNullOrEmpty(scoreId))
                return false;
            foreach (ModdedScoreData data in ModdedSongs.Values)
            {
                if (data.Levels is null)
                    continue;
                foreach (ModdedLevelData level in data.Levels)
                {
                    if (level.ID == scoreId)
                        return true;
                }
            }
            return false;
        }
    }
}