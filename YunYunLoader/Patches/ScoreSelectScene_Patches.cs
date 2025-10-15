using App;
using App.Data;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using YunyunLoader;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(ScoreSelectScene), "CollectData")]
    internal class ScoreSelectScene_Patches
    {
        // reimplementation of ScoreSelectScene.CollectData
        // all members are private so we have to use reflection in order to recreate the functionality
        private static void Postfix(ScoreSelectScene __instance)
        {
            FieldInfo field = AccessTools.Field(typeof(ScoreSelectScene), "m_Data");
            // we have to work with generic dictionaries here as harmony fusses over specifics
            IDictionary dict = (IDictionary)field.GetValue(__instance);

            // local ScoreData class is private
            Type scoreDataType = AccessTools.TypeByName("App.ScoreSelectScene+ScoreData");

            foreach (var data in Plugin.ModdedSongs.Values)
            {
                object? newScoreData;

                if (!dict.Contains(data.ID))
                {
                    newScoreData = Activator.CreateInstance(scoreDataType);
                    AccessTools.Field(scoreDataType, "MusicName").SetValue(newScoreData, data.ID);
                }
                else
                {
                    // if there is already a modded entry in the game's dictionary, just reuse it
                    newScoreData = dict[data.ID];
                }
                
                // build our fake ScoreInfo data
                FieldInfo infosField = AccessTools.Field(scoreDataType, "Infos");
                IDictionary? infos = infosField.GetValue(newScoreData) as IDictionary;
                if (infos == null)
                {
                    Plugin.Log.LogError("Failed to get Infos dictionary.");
                    continue;
                }
                foreach (ModdedLevelData d in data.Levels!)
                    infos.Add((ScoreLevel)d.Data!.Level, new ScoreInfo { Id = d.ID, MusicName = data.ID, Level = d.Data!.Level, Timestamp = 0 });
                infosField.SetValue(newScoreData, infos);

                // build our fake ScoreLevelData data
                FieldInfo levelsField = AccessTools.Field(scoreDataType, "Levels");
                IDictionary? levels = levelsField.GetValue(newScoreData) as IDictionary;
                if (levels == null)
                {
                    Plugin.Log.LogError("Failed to get Levels dictionary.");
                    continue;
                }
                foreach (ModdedLevelData d in data.Levels!)
                    levels.Add((ScoreLevel)d.Data!.Level, new ScoreLevelData { Level = (ScoreLevel)d.Data!.Level, Difficulty = d.Difficulty, MusicID = data.ID, Name = d.ID, TAG = "MOD" });
                levelsField.SetValue(newScoreData, levels);

                // add the result
                dict[data.ID] = newScoreData;
            }
            // update the field with our modified dictionary
            field.SetValue(__instance, dict);
        }
    }
}
