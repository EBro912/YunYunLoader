using App;
using App.Data;
using App.Score;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YunyunLoader;

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
            if (!Plugin.LoadedAudioClips.TryGetValue(Path.GetFileNameWithoutExtension(data.Audio), out AudioClip clip))
                return null!;
            ModdedLevelData? levelData = data.Levels.FirstOrDefault(x => x.Data!.Level == level);
            if (levelData == null)
                return null!;
            return new ScoreDataSet
            {
                MusicName = data.ID,
                Info = new ScoreInfo
                {
                    Id = levelData.ID,
                    MusicName = data.ID,
                    Level = level,
                    Timestamp = 0 // unused
                },
                LevelData = new ScoreLevelData
                {
                    Difficulty = levelData.Difficulty,
                    MusicID = data.ID,
                    Name = levelData.ID,
                    Level = (ScoreLevel)level,
                    TAG = "MOD" // unused
                },
                Clip = clip,
                ScoreData = levelData.Data!
            };
        }
    }
}
