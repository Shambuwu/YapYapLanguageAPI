using HarmonyLib;
using System.Collections.Generic;

[HarmonyPatch(typeof(VoskSpeechToText), "StartVosk",
    new[] { typeof(List<string>), typeof(string), typeof(int) })]
class Patch_StartVosk
{
    static void Prefix(ref List<string> grammer)
    {
        if (grammer == null) return;

        if (!grammer.Contains("douwe klip"))
            grammer.Add("douwe klip");

        if (!grammer.Contains("сука"))
            grammer.Add("сука");

        if (!grammer.Contains("иди нахуй"))
            grammer.Add("иди нахуй");

        if (!grammer.Contains("clamboosterbeern"))
            grammer.Add("clamboosterbeern");
    }
}
