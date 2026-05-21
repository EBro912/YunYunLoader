using App;
using App.Binding;
using App.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UVMBinding;
using UVMBinding.Core;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(UIScoreList), "ApplySort")]
    internal class UIScoreList_ApplySort
    {
        // reimplementation of UIScoreList.ApplySort with our mod filtering included.
        // FilterMod is a bool property we inject into the VM's PropertyContainer in
        // UIScoreList.Bind, so it behaves exactly like the game's FilterNormal/FilterDLC
        private static bool Prefix(UIScoreList __instance)
        {
            bool showMod = ((IViewModel)__instance).Get<bool>("FilterMod");
            ScoreSortMode sortMode = GameConfig.I.SystemConfig.ScoreSortMode;

            List<UIScoreItem> list = new List<UIScoreItem>();
            foreach (UIScoreItem item in __instance.OriginalItems)
            {
                if (item.Select)
                {
                    list.Add(item);
                    continue;
                }

                bool isMod = Plugin.ModdedSongs.ContainsKey(item.MusicId);
                bool isDlc = item.IsDLC;
                bool isNormal = !isDlc && !isMod;

                bool categoryVisible =
                    (isNormal && __instance.FilterNormal) ||
                    (isDlc && __instance.FilterDLC) ||
                    (isMod && showMod);

                bool searchVisible = string.IsNullOrEmpty(__instance.SearchText.Value)
                    || item.Title.ToString().Contains(__instance.SearchText.Value, System.StringComparison.OrdinalIgnoreCase);

                if (categoryVisible && searchVisible)
                    list.Add(item);
            }

            if (sortMode == ScoreSortMode.NumberSort)
            {
                __instance.SortMode = LKey.Text.UIScoreList_NumberSort;
                list = list.OrderBy(x => x, Comparer<UIScoreItem>.Create((a, b) =>
                {
                    bool aLocked = a.IsLock || a.Notpurchased;
                    bool bLocked = b.IsLock || b.Notpurchased;
                    if (aLocked && !bLocked) return 1;
                    if (!aLocked && bLocked) return -1;
                    return a.Order.CompareTo(b.Order);
                })).ToList();
            }
            else if (sortMode == ScoreSortMode.AlphabetSort)
            {
                __instance.SortMode = LKey.Text.UIScoreList_SongSort;
                list = list.OrderBy(x => x.Title.ToString()).ToList();
            }

            if (__instance.SortAscending)
                list.Reverse();

            __instance.Items.Clear();
            __instance.Items.AddRange(list);
            return false;
        }
    }

    [HarmonyPatch(typeof(UIScoreList), "App.Binding.IBindUIControl.Bind")]
    internal class UIScoreList_Bind
    {
        // Bind runs right after the UIScoreList prefab has been created and bound to the scene
        // so utilize it to adjust the existing checkboxes and add ours.
        // we can repurpose the cloned checkbox into a real checkbox identical to
        // the game's own Base/DLC ones utilizing the game's actual event system
        private static void Postfix(UIScoreList __instance, AppUIControl control)
        {
            GameObject? checkNormal = FindDeep(control.transform, "CheckNormal");
            GameObject? checkDlc = FindDeep(control.transform, "CheckDLC");

            if (checkNormal is null)
            {
                Plugin.Log.LogError("Failed to find CheckNormal GameObject.");
                return;
            }

            if (checkDlc is null)
            {
                Plugin.Log.LogError("Failed to find CheckDLC GameObject.");
                return;
            }

            View? view = checkDlc.GetComponentInParent<View>();
            if (view is null)
            {
                Plugin.Log.LogError("Failed to find the UVMBinding View, cannot register the Mod checkbox.");
                return;
            }

            // shift the existing check boxes over to make room for ours
            checkNormal.transform.GetChild(1).localPosition = new Vector3(-3f, 1f, 0f);
            checkDlc.transform.localPosition = new Vector3(-48f, 0f, 0f);
            checkDlc.transform.GetChild(1).localPosition = new Vector3(-8f, 1f, 0f);

            // clone the DLC checkbox (with the edits above already applied) for ours
            GameObject checkMod = UnityEngine.Object.Instantiate(checkDlc, checkDlc.transform.parent);
            checkMod.transform.localPosition = new Vector3(25f, 0f, 0f);
            checkMod.transform.GetChild(1).localPosition = new Vector3(-3f, 1f, 0f);

            // we don't need localization for the checkbox, so gut it out
            // without this it reverts to "DLC" on locale changes
            foreach (LocalizeStringEvent loc in checkMod.GetComponentsInChildren<LocalizeStringEvent>(true))
                UnityEngine.Object.Destroy(loc);
            
            // shrink the label rect to fit "Mod" exactly so it stays clickable without overhanging
            Text label = checkMod.transform.GetChild(1).GetComponent<Text>();
            label.text = "Mod";
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, label.rectTransform.sizeDelta.y);

            // repoint the cloned checkmark binder and click event from DLC to our new
            // FilterMod / OnToggleFilterMod, then register them with the View so the
            // framework drives them
            UVMBinding.Binders.ActiveBinder? checkmark = checkMod
                .GetComponentsInChildren<UVMBinding.Binders.ActiveBinder>(true)
                .FirstOrDefault(b => b.Path == "FilterDLC");

            var eventNameField = AccessTools.Field(typeof(ViewEventBase), "m_EventName");
            ViewEventBase? click = checkMod
                .GetComponentsInChildren<ViewEventBase>(true)
                .FirstOrDefault(e => (string)eventNameField.GetValue(e) == "OnToggleFilterDLC");

            if (checkmark is null || click is null)
            {
                Plugin.Log.LogError("Failed to resolve the cloned checkbox's binder/event; Mod checkbox will not function.");
                UnityEngine.Object.Destroy(checkMod);
                return;
            }

            checkmark.ChangePath("FilterMod");
            eventNameField.SetValue(click, "OnToggleFilterMod");

            // mirror the game's OnToggleFilterDLC handler for our injected property
            IViewModel vm = __instance;
            __instance.Event.Subscribe("OnToggleFilterMod", () =>
            {
                vm.Set("FilterMod", !vm.Get<bool>("FilterMod"));
                __instance.ApplySort();
            });

            // create the property (default on) before
            // registering the binder so View.Add binds it immediately
            vm.Set("FilterMod", true);
            view.Add(checkmark);
            view.Add(click);
        }

        private static GameObject? FindDeep(Transform root, string name)
        {
            return (from t in root.GetComponentsInChildren<Transform>(true)
                where t.name == name
                select t.gameObject).FirstOrDefault();
        }
    }
}
