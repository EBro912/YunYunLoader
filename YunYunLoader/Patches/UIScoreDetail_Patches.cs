using App;
using App.Binding;
using App.Data;
using HarmonyLib;
using System.Linq;
using YunyunLoader;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(UIScoreDetail), "Set")]
    internal class UIScoreDetail_Patches
    {
        private static bool Prefix(string musicID, UIScoreDetail __instance)
        {
            if (Plugin.TryGetSongByTitle(musicID, out ModdedScoreData data))
            {
                __instance.Title = LKey.ScoreData.Get(musicID);
                __instance.Singer = LKey.Text.HUD_Score_Singer.WithParam("name", data.Artist);
                __instance.Composer = LKey.Text.HUD_Score_Composer.WithParam("name", data.Composer);
                __instance.Writer = LKey.Text.HUD_Score_Lyricist.WithParam("name", data.Lyricist);
                __instance.Arrangement = LKey.Text.HUD_Score_Arrangement.WithParam("name", data.Arranger);
                return false;
            }
            return true;
        }
    }
}
