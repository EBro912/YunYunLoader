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
        private static void Postfix(TitleYunYunController __instance)
        {
            // UITitleBase/Contents/DefaultChara/YunYun/Frame..TitleYunYun
            // UIHelp/Contents/TitleCharaSet/DefaultChara/YunYun/Frame/TitleYunYun

            // DefaultChara/YunYun/TitleYunYun
            Transform defaultChara = __instance.transform.parent.parent;
            // DefaultChara/TextFrame/Text
            Transform text = defaultChara.GetChild(1).GetChild(0);
            if (text == null)
            {
                Plugin.Log.LogError("Failed to find DefaultChara/TextFrame/Text.");
                return;
            }

            // scaffold upward in the tree to find the Contents parent
            // the layout is different depending on the UI, but Contents should always be there
            // it's usually only 1-2 away but loop it just to be safe
            Transform? contents = defaultChara.parent;
            while (contents?.name != "Contents") 
                contents = contents?.parent;
            
            if (contents == null)
            {
                Plugin.Log.LogError("Failed to find Contents parent.");
                return;
            }

            GameObject watermark = Object.Instantiate(text.gameObject, contents);
            // we don't need localization for the watermark
            LocalizeStringBinder b = watermark.GetComponent<LocalizeStringBinder>();
            if (b != null) Object.Destroy(b);
            LocalizeStringEvent e = watermark.GetComponent<LocalizeStringEvent>();
            if (e != null) Object.Destroy(e);
            watermark.GetComponent<Text>().text = "YunYunLoader v1.4.0";
            watermark.transform.position = new Vector3(2.925f, -1.85f, 100f);
        }
    }
}