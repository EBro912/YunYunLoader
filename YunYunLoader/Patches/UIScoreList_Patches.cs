using App;
using App.Binding;
using App.Data;
using App.UI;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static readonly FieldInfo applySortingField = AccessTools.Field(typeof(UIScoreList), "m_ApplySorting");
        private static readonly FieldInfo onSortAppliedField = AccessTools.Field(typeof(UIScoreList), "OnSortApplied");

        // Full reimplementation of UIScoreList.ApplySort with our extra "Mod" filter category
        private static bool Prefix(UIScoreList __instance)
        {
            // reentrancy guard, shared with the game's own field
            if ((bool)applySortingField.GetValue(__instance))
                return false;
            applySortingField.SetValue(__instance, true);

            bool showMod = ((IViewModel)__instance).Get<bool>("FilterMod");
            ScoreSortMode sortMode = App.GameConfig.I.SystemConfig.ScoreSortMode;

            List<UIScoreItem> list = new List<UIScoreItem>();
            foreach (UIScoreItem item in __instance.OriginalItems)
            {
                if (item.Select)
                {
                    list.Add(item);
                    continue;
                }

                bool isMod = Plugin.IsModdedSong(item.MusicId);
                bool isDlc = item.IsDLC;
                bool isNormal = !isDlc && !isMod;

                bool categoryVisible =
                    (isNormal && __instance.FilterNormal) ||
                    (isDlc && __instance.FilterDLC) ||
                    (isMod && showMod);

                bool searchVisible = string.IsNullOrEmpty(__instance.SearchText.Value)
                    || item.SearchableCredits.Contains(__instance.SearchText.Value, System.StringComparison.OrdinalIgnoreCase);

                if (categoryVisible && searchVisible)
                    list.Add(item);
            }

            switch (sortMode)
            {
                default:
                    __instance.SortMode = LKey.Text.UIScoreList_NumberSort;
                    list = list.OrderBy(GetLockGroup).ThenBy(x => x.Order).ToList();
                    break;
                case ScoreSortMode.AlphabetSort:
                    __instance.SortMode = LKey.Text.UIScoreList_SongSort;
                    list = list.OrderBy(GetLockGroup).ThenBy(x => x.Title.ToString()).ToList();
                    break;
                case ScoreSortMode.LevelSort_LV1:
                    __instance.SortMode = LKey.Text.UIScoreList_LevelSort1;
                    list = SortByLevel(list, ScoreLevel.LV1);
                    break;
                case ScoreSortMode.LevelSort_LV3:
                    __instance.SortMode = LKey.Text.UIScoreList_LevelSort3;
                    list = SortByLevel(list, ScoreLevel.LV3);
                    break;
                case ScoreSortMode.LevelSort_LV4:
                    __instance.SortMode = LKey.Text.UIScoreList_LevelSort4;
                    list = SortByLevel(list, ScoreLevel.LV4);
                    break;
                case ScoreSortMode.LevelSort_LV5:
                    __instance.SortMode = LKey.Text.UIScoreList_LevelSort5;
                    list = SortByLevel(list, ScoreLevel.LV5);
                    break;
                case ScoreSortMode.ScoreSort_LV1:
                    __instance.SortMode = LKey.Text.UIScoreList_ScoreSort1;
                    list = SortByScore(list, ScoreLevel.LV1);
                    break;
                case ScoreSortMode.ScoreSort_LV3:
                    __instance.SortMode = LKey.Text.UIScoreList_ScoreSort3;
                    list = SortByScore(list, ScoreLevel.LV3);
                    break;
                case ScoreSortMode.ScoreSort_LV4:
                    __instance.SortMode = LKey.Text.UIScoreList_ScoreSort4;
                    list = SortByScore(list, ScoreLevel.LV4);
                    break;
                case ScoreSortMode.ScoreSort_LV5:
                    __instance.SortMode = LKey.Text.UIScoreList_ScoreSort5;
                    list = SortByScore(list, ScoreLevel.LV5);
                    break;
            }

            if (__instance.SortAscending)
            {
                // the game reverses each lock group independently, then concatenates them back
                List<UIScoreItem> unlocked = list.Where(x => GetLockGroup(x) == 0).Reverse().ToList();
                List<UIScoreItem> notPurchased = list.Where(x => GetLockGroup(x) == 1).Reverse().ToList();
                List<UIScoreItem> locked = list.Where(x => GetLockGroup(x) == 2).Reverse().ToList();
                list = unlocked.Concat(notPurchased).Concat(locked).ToList();
            }

            float scrollbar = __instance.Scrollbar;
            ScoreLevel levelBeforeRebuild = __instance.Detail.Level;
            __instance.Items.Clear();
            __instance.Items.AddRange(list);
            __instance.Scrollbar = scrollbar;
            applySortingField.SetValue(__instance, false);
            if (__instance.Detail.IsVisible && __instance.Detail.LevelDatas != null
                && __instance.Detail.Level != levelBeforeRebuild
                && __instance.Detail.LevelDatas.Any(x => x.Level == levelBeforeRebuild))
            {
                __instance.Detail.SetScoreLevel(levelBeforeRebuild);
            }

            (onSortAppliedField.GetValue(__instance) as System.Action)?.Invoke();
            return false;
        }

        // mirrors of UIScoreList's private GetLockGroup / SortByLevel / SortByScore helpers
        private static int GetLockGroup(UIScoreItem x)
        {
            if (x.Notpurchased) return 1;
            if (x.IsLock) return 2;
            return 0;
        }

        private static List<UIScoreItem> SortByLevel(List<UIScoreItem> items, ScoreLevel level)
        {
            return items.OrderBy(GetLockGroup)
                .ThenByDescending(x => x.LevelDifficulties.GetValueOrDefault(level, int.MinValue))
                .ToList();
        }

        private static List<UIScoreItem> SortByScore(List<UIScoreItem> items, ScoreLevel level)
        {
            return items.OrderBy(GetLockGroup)
                .ThenByDescending(x => x.LevelScores.GetValueOrDefault(level, -1))
                .ToList();
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
            
            // clone the DLC checkbox (with the edits above already applied) for ours
            GameObject checkMod = UnityEngine.Object.Instantiate(checkDlc, checkDlc.transform.parent);

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
            
            // parent auto-positions checkboxes vertically, so just shift it up and squish it so it fits in the UI
            checkDlc.transform.parent.localPosition = new Vector3(1, 16, 0);
            checkDlc.transform.parent.GetComponent<VerticalLayoutGroup>().spacing = -7;
        }

        private static GameObject? FindDeep(Transform root, string name)
        {
            return (from t in root.GetComponentsInChildren<Transform>(true)
                where t.name == name
                select t.gameObject).FirstOrDefault();
        }
    }
}
