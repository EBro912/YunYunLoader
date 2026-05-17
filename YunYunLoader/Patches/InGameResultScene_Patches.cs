using App;
using App.Data;
using App.Flags;
using App.Score;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace YunYunLoader.Patches
{
    // keep track of whether the song currently playing is modded
    [HarmonyPatch]
    internal class InGameResultScene_PlayResult
    {
        private static readonly System.Type sceneType = AccessTools.TypeByName("App.InGameResultScene");
        private static readonly PropertyInfo dataProp = AccessTools.Property(sceneType, "Data");

        private static MethodBase TargetMethod() => AccessTools.Method(sceneType, "PlayResult");

        private static void Prefix(object __instance)
        {
            ScoreDataSet? data = dataProp.GetValue(__instance) as ScoreDataSet;
            string? musicId = data?.LevelData?.MusicID;
            Plugin.IsModdedSongResult = musicId != null && Plugin.ModdedSongs.ContainsKey(musicId);
            if (Plugin.IsModdedSongResult)
            {
                // these fields are hard to patch so we can just keep track of them ourselves
                Plugin.SavedIsPlayedPost = SaveData.Global.IsPlayedPost;
                Plugin.SavedIsRankingEntry = SaveData.Global.IsRankingEntry;
            }
        }

        private static void Postfix(ref UniTask __result)
        {
            if (Plugin.IsModdedSongResult)
                __result = WrapAndCleanup(__result);
        }

        private static async UniTask WrapAndCleanup(UniTask original)
        {
            try
            {
                await original;
            }
            finally
            {
                // restore the pre-modded state after finishing
                SaveData.Global.IsPlayedPost = Plugin.SavedIsPlayedPost;
                SaveData.Global.IsRankingEntry = Plugin.SavedIsRankingEntry;
                Plugin.IsModdedSongResult = false;
            }
        }
    }

    // prevent adding denpa points for modded songs
    [HarmonyPatch(typeof(SaveData), nameof(SaveData.AddDenpaPoint))]
    internal class SaveData_AddDenpaPoint
    {
        private static bool Prefix() => !Plugin.IsModdedSongResult;
    }

    // prevent adding followers for modded songs
    [HarmonyPatch(typeof(SaveData), nameof(SaveData.AddFollowCount))]
    internal class SaveData_AddFollowCount
    {
        private static bool Prefix() => !Plugin.IsModdedSongResult;
    }

    // prevent any GameParameter gain (DokiDoki, YunYun, Charisma) for modded songs
    [HarmonyPatch(typeof(GameParameter), nameof(GameParameter.Add))]
    internal class GameParameter_Add
    {
        private static bool Prefix() => !Plugin.IsModdedSongResult;
    }

    // prevent adding conspiracy theory for modded songs
    [HarmonyPatch(typeof(ConspiracyTheoryData), nameof(ConspiracyTheoryData.ApplyGetCount))]
    internal class ConspiracyTheoryData_ApplyGetCount
    {
        private static bool Prefix() => !Plugin.IsModdedSongResult;
    }

    // prevent story flags from being triggered from modded songs
    [HarmonyPatch(typeof(FlagData), nameof(FlagData.Set))]
    internal class FlagData_Set
    {
        private static bool Prefix() => !Plugin.IsModdedSongResult;
    }

    // when attempting to submit a modded score, mirror how the game behaves in demo mode
    // in demo mode, the IsUpdateRank flag is set to false and the NewRank is negated
    [HarmonyPatch(typeof(AppSteamManager), nameof(AppSteamManager.SubmitScore))]
    internal class AppSteamManager_SubmitScore
    {
        private static bool Prefix(ref UniTask<RankingSubmitResult> __result)
        {
            if (!Plugin.IsModdedSongResult) return true;
            __result = UniTask.FromResult(new RankingSubmitResult { IsUpdateRank = false, NewRank = -1 });
            return false;
        }
    }

    // suppress Steam achievement unlocks during a modded result
    [HarmonyPatch(typeof(AppSteamManager), nameof(AppSteamManager.SetAchievement))]
    internal class AppSteamManager_SetAchievement
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Plugin.IsModdedSongResult) return true;
            __result = false;
            return false;
        }
    }

    // unconditionally filter modded song IDs out of the any-record achievements 
    [HarmonyPatch(typeof(ScoreRecordDataList), nameof(ScoreRecordDataList.HasAnyRecordData))]
    internal class ScoreRecordDataList_HasAnyRecordData
    {
        private static readonly FieldInfo listField = AccessTools.Field(typeof(ScoreRecordDataList), "List");

        private static bool Prefix(ScoreRecordDataList __instance, ref bool __result)
        {
            List<ScoreRecordData> list = listField.GetValue(__instance) as List<ScoreRecordData>;
            __result = false;
            if (list == null) return false;
            foreach (ScoreRecordData rec in list)
            {
                if (rec.Rank != RecordRank.None && !Plugin.ModdedSongs.ContainsKey(rec.Name))
                {
                    __result = true;
                    break;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ScoreRecordDataList), nameof(ScoreRecordDataList.HasAnyFullComboRecordData))]
    internal class ScoreRecordDataList_HasAnyFullComboRecordData
    {
        private static readonly FieldInfo listField = AccessTools.Field(typeof(ScoreRecordDataList), "List");

        private static bool Prefix(ScoreRecordDataList __instance, ref bool __result)
        {
            List<ScoreRecordData> list = listField.GetValue(__instance) as List<ScoreRecordData>;
            __result = false;
            if (list == null) return false;
            foreach (ScoreRecordData rec in list)
            {
                if (rec.FullCombo && !Plugin.ModdedSongs.ContainsKey(rec.Name))
                {
                    __result = true;
                    break;
                }
            }
            return false;
        }
    }

    // prevent modded scores from being included in total denpa point calculation
    [HarmonyPatch(typeof(GlobalSaveData), nameof(GlobalSaveData.GetDenpaPoint))]
    internal class GlobalSaveData_GetDenpaPoint
    {
        private static bool Prefix(GlobalSaveData __instance, ref long __result)
        {
            long sum = __instance.DenpaPlayPoint + __instance.DenpaConspiracyTheoryPoint + __instance.DenpaEndingPoint;
            foreach (ScoreRecordData rec in __instance.ScoreRecords.GetAllData())
            {
                if (Plugin.ModdedSongs.ContainsKey(rec.Name)) continue;
                sum += DenpaPointUtil.CalcRecordDenpaPoint(rec);
            }
            __result = sum;
            return false;
        }
    }

    // prevent modded scores from being included in story trigger calculation 
    [HarmonyPatch(typeof(TotalScoreEventCondition), "GetValue")]
    internal class TotalScoreEventCondition_GetValue
    {
        private static bool Prefix(ref int __result)
        {
            __result = SaveData.GetAllRecordData()
                .Where(x => !Plugin.ModdedSongs.ContainsKey(x.Name))
                .Sum(x => x.Point);
            return false;
        }
    }
    
    [HarmonyPatch(typeof(TotalFullComboEventCondition), "GetValue")]
    internal class TotalFullComboEventCondition_GetValue
    {
        private static bool Prefix(ref int __result)
        {
            __result = SaveData
                .GetAllRecordData()
                .Count(x => x.FullCombo && !Plugin.ModdedSongs.ContainsKey(x.Name));
            return false;
        }
    }

    // during the VS-Pchan story arc, prevent modded songs from contributing 
    [HarmonyPatch(typeof(FlagData), nameof(FlagData.NextStepVSPchan))]
    internal class FlagData_NextStepVSPchan
    {
        private static bool Prefix(ref FlagData.VSPchanStepData __result)
        {
            if (!Plugin.IsModdedSongResult) return true;
            __result = null!;
            return false;
        }
    }
}
