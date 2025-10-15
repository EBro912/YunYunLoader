using BepInEx;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Networking;
using YunYunLoader;
using YYNoteEditor;
using Newtonsoft.Json;

namespace YunyunLoader
{
    [BepInPlugin("YunYunLoader", "YunYunLoader", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony Harmony = new Harmony("YunYunLoader");

        internal static ManualLogSource Log;

        internal static Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();
        internal static Dictionary<string, ModdedScoreData> ModdedSongs = new Dictionary<string, ModdedScoreData>();

        public static readonly string SongsPath = Paths.GameRootPath + "\\Songs";

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
            StringTable table = LocalizationSettings.StringDatabase.GetTable("ScoreData");
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
                     data = JsonConvert.DeserializeObject<ModdedScoreData>(File.ReadAllText(dir + "\\song.json"));
                }
                catch
                {
                    Logger.LogError("Failed to parse song.json in " + dir);
                    continue;
                }

                if (data == null)
                {
                    Logger.LogError("Failed to parse song.json in " + dir);
                    continue;
                }

                if (ModdedSongs.ContainsKey(data.ID!))
                {
                    Logger.LogWarning("Duplicate song ID " + data.ID + " in " + dir + ", skipping!");
                    continue;
                }

                bool failure = false;
                foreach (FieldInfo f in typeof(ModdedScoreData).GetFields())
                {
                    if (f.GetValue(data) == null)
                    {
                        Logger.LogError("Missing field " + f.Name + " in song.json in " + dir);
                        failure = true;
                    }
                }
                if (failure)
                    continue;

                AudioClip? clip = await LoadOggAsync(dir + "\\" + data.Audio!);
                if (clip == null)
                    continue;

                LoadedAudioClips[Path.GetFileNameWithoutExtension(data.Audio!)] = clip;
                foreach (ModdedLevelData l in data.Levels!)
                {
                    ScoreData? score;
                    try
                    {
                        score = JsonConvert.DeserializeObject<ScoreData>(File.ReadAllText(dir + "\\" + l.Path));
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
                    table.AddEntry(l.ID + "_EDITOR", l.Editor);
                    l.Data = score;
                }
                AddLocalizationData(data, ref table);
                ModdedSongs[data.ID!] = data;
                songsLoaded++;
            }

            Logger.LogInfo("Loaded " + songsLoaded + " custom songs!");
        }

        internal static bool TryGetSongByTitle(string musicName, out ModdedScoreData result)
        {
            result = ModdedSongs.Values.FirstOrDefault(x => x.Title == musicName);
            return result != null;
        }

        private void AddLocalizationData(ModdedScoreData data, ref StringTable table)
        {
            table.AddEntry(data.ID, data.Title);
            table.AddEntry(data.ID + "_ARTIST", data.Artist);
            table.AddEntry(data.ID + "_LYRICS", data.Lyricist);
            table.AddEntry(data.ID + "_COMP", data.Composer);
            table.AddEntry(data.ID + "_ARRAN", data.Arranger);
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
                Plugin.Log.LogError("Failed to load .ogg file from " + path);
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(www);
        }
    }
}
