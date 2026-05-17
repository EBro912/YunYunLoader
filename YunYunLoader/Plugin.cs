using BepInEx;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Networking;
using YYNoteEditor;
using Newtonsoft.Json;

namespace YunYunLoader
{
    [BepInPlugin("YunYunLoader", "YunYunLoader", "1.2.0")]
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
            int songsLoaded = 0;
            foreach (string dir in Directory.GetDirectories(SongsPath))
            {
                if (!File.Exists(dir + "\\song.json"))
                {
                    Logger.LogWarning("No song.json found in " + dir + ", skipping!");
                    continue;
                }
                ModdedScoreData? data;
                try
                {
                    string json = await File.ReadAllTextAsync(dir + "\\song.json");
                    data = JsonConvert.DeserializeObject<ModdedScoreData>(json);
                }
                catch
                {
                    Logger.LogError("Failed to parse song.json in " + dir);
                    continue;
                }

                if (data is null)
                {
                    Logger.LogError("Failed to parse song.json in " + dir);
                    continue;
                }

                bool failure = false;
                foreach (FieldInfo f in typeof(ModdedScoreData).GetFields())
                {
                    if (f.GetValue(data) is null)
                    {
                        Logger.LogError("Missing field " + f.Name + " in song.json in " + dir);
                        failure = true;
                    }
                }
                if (failure || data.ID is null)
                    continue;
                
                if (ModdedSongs.ContainsKey(data.ID))
                {
                    Logger.LogWarning("Duplicate song ID " + data.ID + " in " + dir + ", skipping!");
                    continue;
                }

                AudioClip? clip = await LoadOggAsync(dir + "\\" + data.Audio);
                if (clip is null)
                    continue;

                if (!string.IsNullOrEmpty(data.Icon))
                {
                    Texture2D? texture = await LoadTextureAsync(dir + "\\" + data.Icon);
                    if (texture is null)
                    {
                        Logger.LogWarning("Failed to load icon " + data.Icon + " from song.json in " + dir + ", skipping!");
                    }
                    else
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        if (!LoadedSprites.TryAdd(data.ID, sprite))
                        {
                            Logger.LogWarning(data.ID + " already has a loaded sprite, skipping!");
                        }
                    }
                }

                if (!LoadedAudioClips.TryAdd(data.ID, clip))
                {
                    Logger.LogWarning(data.ID + " already has a loaded audio, skipping!");
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
                        Logger.LogError("Failed to parse song data " + l.Path + " in " + dir);
                        continue;
                    }

                    if (score == null)
                    {
                        Logger.LogError("Failed to parse song data " + l.Path + " in " + dir);
                        continue;
                    }

                    l.ID = Path.GetFileNameWithoutExtension(l.Path);
                    l.Data = score;
                }
                ModdedSongs[data.ID] = data;
                songsLoaded++;
            }

            LocalizedStringDatabase db = LocalizationSettings.StringDatabase;
            PopulateScoreDataTable(db.GetTable("ScoreData"));
            // hook our table postprocessor into the localization system, the base game doesn't use it
            db.TablePostprocessor = new ScoreDataTablePostprocessor(db.TablePostprocessor);

            Logger.LogInfo("Loaded " + songsLoaded + " custom songs!");
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

        private async UniTask<AudioClip?> LoadOggAsync(string path)
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
        
        private async UniTask<Texture2D?> LoadTextureAsync(string path)
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
    }
}
