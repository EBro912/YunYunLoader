using App.Audio;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using YunyunLoader;

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
            string? current = AccessTools.Field(typeof(SoundMediator), "m_PlayCurrent").GetValue(self) as string;
            if (current == null)
            {
                // if we can't get the current song, mirror original behavior
                source.clip = clip;
                return;
            }

            if (Plugin.ModdedSongs.TryGetValue(current, out ModdedScoreData result))
            {
                if (Plugin.LoadedAudioClips.TryGetValue(Path.GetFileNameWithoutExtension(result.Audio), out AudioClip c))
                {
                    source.clip = c;
                    return;
                }
            }
            else
            {
                // if the song is not modded, use the original
                source.clip = clip;
            }
        }
    }
}
