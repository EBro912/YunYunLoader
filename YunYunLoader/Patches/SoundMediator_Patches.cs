using App.Audio;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace YunYunLoader.Patches
{
    [HarmonyPatch(typeof(SoundMediator), "Update")]
    internal class SoundMediator_Patches
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var clipMethod = AccessTools.PropertySetter(typeof(AudioSource), "clip");

            foreach (CodeInstruction instr in instructions)
            {
                if (instr.opcode == OpCodes.Callvirt && instr.operand is MethodInfo mi && mi == clipMethod) {
                    // push 'this' onto the stack
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    // call our set method instead of the original
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SoundMediator_Patches), nameof(SetClip)));
                }
                else
                    yield return instr;
            }
        }

        private static void SetClip(AudioSource source, AudioClip clip, SoundMediator self)
        {
            if (!(AccessTools.Field(typeof(SoundMediator), "m_PlayCurrent").GetValue(self) is string current))
            {
                // if we can't get the current song, mirror original behavior
                source.clip = clip;
                return;
            }

            if (Plugin.ModdedSongs.TryGetValue(current, out ModdedScoreData result))
            {
                if (result.ID != null && Plugin.LoadedAudioClips.TryGetValue(result.ID, out AudioClip c))
                {
                    source.clip = c;
                }
            }
            else
            {
                // if the song is not modded, use the original
                source.clip = clip;
            }
        }
    }

    // prevent attempts to unload modded resources, ours are loaded via UnityWebRequest so they aren't a Resource
    // prevents an error in the logs
    [HarmonyPatch(typeof(Resources), nameof(Resources.UnloadAsset))]
    internal class Resources_UnloadAsset
    {
        private static bool Prefix(Object assetToUnload)
        {
            if (assetToUnload is AudioClip clip && Plugin.LoadedAudioClips.ContainsValue(clip))
                return false;
            return true;
        }
    }
}
