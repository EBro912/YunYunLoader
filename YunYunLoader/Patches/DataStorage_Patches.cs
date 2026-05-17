using App;
using App.Data;
using App.Score;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace YunYunLoader.Patches
{
    [HarmonyPatch]
    internal class DataStorage_Patches
    {
        private static MethodBase TargetMethod()
        {
            // target the explicit implementation
            return AccessTools.Method(typeof(DataStorage), "App.Data.IDataStorage.LoadDataSet");
        }

        private static void Postfix(ScoreInfo data, ref UniTask<ScoreDataSet> __result)
        {
            if (Plugin.ModdedSongs.TryGetValue(data.MusicName, out ModdedScoreData d))
            {
                // override the result with our custom data
                __result = AppendCustomData(d, data.Level);
            }
        }

        private static async UniTask<ScoreDataSet> AppendCustomData(ModdedScoreData data, int level)
        {
            await UniTask.CompletedTask;
            if (data.ID is null || data.Levels is null || !Plugin.LoadedAudioClips.TryGetValue(data.ID, out AudioClip clip))
                return null!;
            ModdedLevelData? levelData = data.Levels.FirstOrDefault(x => x.Data!.Level == level);
            if (levelData is null)
                return null!;
            return new ScoreDataSet
            {
                MusicName = data.ID,
                Info = new ScoreInfo
                {
                    Id = levelData.ID,
                    MusicName = data.ID,
                    Level = level,
                    Timestamp = -1 // unused
                },
                LevelData = new ScoreLevelData
                {
                    Difficulty = levelData.Difficulty,
                    MusicID = data.ID,
                    Name = levelData.ID,
                    Level = (ScoreLevel)level,
                    TAG = "MOD", // unused
                    Group = ScoreGroup.Normal,
                    Order = 0 // TODO: proper ordering for modded songs
                },
                Clip = clip,
                ScoreData = levelData.Data!
            };
        }
    }

    /* Song dumping for development
    [HarmonyPatch]
    internal class DataStorage_Dump 
    {
        private static MethodBase TargetMethod()
        {
            // target the explicit implementation
            return AccessTools.Method(typeof(DataStorage), "App.Data.IDataStorage.Init");
        }
        
        private static void Postfix()
        {
            MasterData data = ServInjector.Resolve<MasterData>();
            if (data == null)
            {
                Plugin.Log.LogError("MasterData is null");
                return;
            }
            
            string scoreInfos = Resources.Load<TextAsset>("YunYun/list").text;
            File.WriteAllText(BepInEx.Paths.GameRootPath + "/score_infos.json", scoreInfos);

            if (Directory.Exists(BepInEx.Paths.GameRootPath + "/dump"))
            {
                Plugin.Log.LogWarning("Skipping dump, folder already exists.");
                return;
            }
            
            Directory.CreateDirectory(BepInEx.Paths.GameRootPath + "/dump");
            int count = 0;
            int success = 0;
            foreach (ScoreLevelData d in data.Data.ScoreLevelData)
            {
                count++;
                try
                {
                    TextAsset text = Resources.Load<TextAsset>("YunYun/Score/" + d.Name);
                    File.WriteAllText(BepInEx.Paths.GameRootPath + "/dump/" + d.Name + ".json", text.text);
                    Plugin.Log.LogInfo("Dumped " + d.Name);
                    Plugin.Log.LogInfo($"(TAG: {d.TAG}, Name: {d.Name}, MusicID: {d.MusicID}, Level: {d.Level}, Diff: {d.Difficulty}, Group: {d.Group}, Order: {d.Order})");
                    success++;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError("Dump of " + d.Name + " failed: " + e.Message);
                }
            }
            Plugin.Log.LogInfo($"Done! Dumped {success}/{count} data.");
        }
    } */
}
