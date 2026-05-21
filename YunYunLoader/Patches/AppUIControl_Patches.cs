using System.Linq;
using App.Binding;
using App.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UVMBinding;
using UVMBinding.Core;
using UVMBinding.Events;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(AppUIControl), "OnCreated")]
    internal class AppUIControl_Patches
    {
        private static bool Loading = false;
        
        private static void Postfix(AppUIControl __instance, ViewModel prm)
        {
            // we only care about the UITitleHelp
            // we also want to avoid doing multiple loads at once
            if (!(prm is UITitleHelp vm) || Loading)
                return;

            Transform? buttonsRoot = __instance.transform
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Buttons");
            if (buttonsRoot is null || buttonsRoot.childCount == 0)
            {
                Plugin.Log.LogError("UITitleHelp Buttons container not found or empty!");
                return;
            }

            View? view = __instance.GetComponent<View>();
            if (view is null)
            {
                Plugin.Log.LogError("AppUIControl missing View component!");
                return;
            }

            vm.Event.Subscribe("OnReloadMods", OnReloadMods);

            GameObject button = Object.Instantiate(buttonsRoot.GetChild(0).gameObject, buttonsRoot);
            button.name = "ReloadMods";
            button.transform.position = new Vector3(0.285f, -0.8f, 100f);
            // TODO: fix button position on the title help page (it's different for some reason...)
            // button.transform.position = new Vector3(0.845f, -0.8f, 100f);
            
            // strip localization so our label survives locale changes
            foreach (LocalizeStringEvent loc in button.GetComponentsInChildren<LocalizeStringEvent>(true))
                Object.Destroy(loc);

            Text? label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = "Reload Mods";

            EventButton? eventButton = button.GetComponent<EventButton>();
            if (eventButton is null)
            {
                Plugin.Log.LogError("Cloned button has no EventButton, ReloadMods button will not function!");
                Object.Destroy(button);
                return;
            }
            AccessTools.Field(typeof(ViewEventBase), "m_EventName").SetValue(eventButton, "OnReloadMods");
            view.Add(eventButton);
        }

        // SubscribeHandle.Subscribe expects a void function
        // ReSharper disable once AsyncVoidMethod
        private static async void OnReloadMods()
        {
            Loading = true;
            await Plugin.LoadCustomSongs();
            Loading = false;
            await Plugin.Notify($"Reloaded {Plugin.ModdedSongs.Count} modded songs!");
        }
    }
}
