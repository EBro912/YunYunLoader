using App;
using App.Title;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UVMBinding.Binders;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(TitleYunYunController), "Start")]
    public class TitleYunYunController_Start
    {
        // utilize the title screen YunYun GameObject to help add our version watermark to the title screen
        // has some node traversal hacks, but it works
        // UITitleBase doesn't implement IBindUIControl like UIScoreList does so we can't reuse that patch method
        private static async void Postfix(TitleYunYunController __instance)
        {
            // UITitleBase/Contents (relative to TitleYunYun)
            Transform contents = __instance.transform.parent.parent.parent;
            if (contents == null)
            {
                Plugin.Log.LogError("Failed to find UITitleBase/Contents.");
                return;
            }
            
            // the title screen has multiple textframes, so scaffold by children
            // UITitleBase/Contents/DefaultChara/TextFrame/Text
            Transform text = contents.GetChild(1).GetChild(1).GetChild(0);
            if (text == null)
            {
                Plugin.Log.LogError("Failed to find UITitleBase/Contents/DefaultChara/TextFrame/Text.");
                return;
            }

            GameObject watermark = Object.Instantiate(text.gameObject, contents);
            // we don't need localization for the watermark
            LocalizeStringBinder b = watermark.GetComponent<LocalizeStringBinder>();
            if (b != null) Object.Destroy(b);
            LocalizeStringEvent e = watermark.GetComponent<LocalizeStringEvent>();
            if (e != null) Object.Destroy(e);
            watermark.GetComponent<Text>().text = "YunYunLoader v1.3.0";
            watermark.transform.position = new Vector3(2.925f, -1.85f, 100f);
        }
    }
}