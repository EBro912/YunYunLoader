using App;
using App.Binding;
using HarmonyLib;
using UnityEngine;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(UIScoreDetail), "Set")]
    internal class UIScoreDetail_Patches
    {
        private static bool Prefix(string musicID, UIScoreDetail __instance)
        {
            if (Plugin.ModdedSongs.TryGetValue(musicID, out ModdedScoreData? data))
            {
                if (data?.ID is null)
                    return true;

                __instance.MusicId = musicID;
                Sprite? icon = null;
                __instance.HasMusicLogo = !string.IsNullOrWhiteSpace(data.Icon) && Plugin.LoadedSprites.TryGetValue(data.ID, out icon);
                __instance.MusicLogo = icon;
                __instance.Title = LKey.ScoreData.Get(musicID);
                __instance.Singer = LKey.Text.HUD_Score_Singer.WithParam("name", LKey.ScoreData.Get(musicID + "_ARTIST"));
                __instance.Composer = LKey.Text.HUD_Score_Composer.WithParam("name", LKey.ScoreData.Get(musicID + "_COMP"));
                __instance.Writer = LKey.Text.HUD_Score_Lyricist.WithParam("name", LKey.ScoreData.Get(musicID + "_LYRICS"));
                __instance.Arrangement = LKey.Text.HUD_Score_Arrangement.WithParam("name", LKey.ScoreData.Get(musicID + "_ARRAN"));
                return false;
            }
            return true;
        }
    }
}
