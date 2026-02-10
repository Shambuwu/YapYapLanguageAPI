using HarmonyLib;
using System.Linq;
using UnityEngine;
using YAPYAP;

[HarmonyPatch(typeof(UISettings), "SetVoiceLanguage")]
class Patch_SetVoiceLanguage
{
    static bool Prefix(UISettings __instance, int index)
    {
        VoiceManager vm;
        if (!Service.Get<VoiceManager>(out vm))
            return true;

        int baseCount = vm.VoskLocalisations.Count;

        if (index < baseCount)
            return true; // let game handle normal languages

        int modIndex = index - baseCount;
        if (modIndex < 0 || modIndex >= LanguageRegistry.Languages.Count)
            return true;

        var lang = LanguageRegistry.Languages[modIndex];

        var grammar = LanguageRegistry.GrammarById[lang.id]
            .SelectMany(x => x.Value)
            .Distinct()
            .ToList();

        var modelPath = LanguageRegistry.ModelPathById[lang.id];

        vm.Vosk.StartVosk(grammar, modelPath, 3);

        return false; // block original SetVoiceLanguage
    }
}
