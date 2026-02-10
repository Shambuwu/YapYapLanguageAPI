using HarmonyLib;
using System.Linq;
using System.IO;
using UnityEngine;
using YAPYAP;
using BepInEx;
using System.Collections.Generic;

[HarmonyPatch(typeof(UISettings), "SetVoiceLanguage")]
class Patch_SetVoiceLanguage
{
    static bool Prefix(UISettings __instance, int index)
    {
        VoiceManager vm;
        if (!Service.Get<VoiceManager>(out vm))
            return true;

        int baseCount = vm.VoskLocalisations?.Count ?? 0;
        if (index < baseCount) return true;

        int modIndex = index - baseCount;
        if (modIndex < 0 || modIndex >= LanguageRegistry.Languages.Count) return true;

        var lang = LanguageRegistry.Languages[modIndex];

        if (!LanguageRegistry.ModelPathById.TryGetValue(lang.id, out var configuredPath))
        {
            Debug.LogWarning($"[YapYapLanguageAPI] No model path for language '{lang.id}'. Aborting StartVosk.");
            return true;
        }

        // Build candidate paths and pick the first existing directory.
        var candidates = new List<string>();

        // Use configured path as-is
        if (!string.IsNullOrEmpty(configuredPath))
            candidates.Add(configuredPath);

        // If configuredPath looks relative, try interpreting it relative to the plugin folder
        try
        {
            var pluginBase = Paths.PluginPath;
            if (!string.IsNullOrEmpty(pluginBase) && !Path.IsPathRooted(configuredPath))
                candidates.Add(Path.Combine(pluginBase, configuredPath));
        }
        catch { /* ignore if Paths not available */ }

        // Also try relative to game's StreamingAssets locations where models commonly live
        try
        {
            var appData = Application.dataPath; // <game>/yapyap_Data
            if (!string.IsNullOrEmpty(appData))
            {
                candidates.Add(Path.Combine(appData, "StreamingAssets", configuredPath));
                var modelName = Path.GetFileName(configuredPath);
                if (!string.IsNullOrEmpty(modelName))
                    candidates.Add(Path.Combine(appData, "StreamingAssets", "Vosk", "Model", modelName));
            }
        }
        catch { /* swallow */ }

        // Normalize and dedupe
        var uniq = candidates
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Path.GetFullPath(p))
            .Distinct()
            .ToList();

        string chosen = uniq.FirstOrDefault(p => Directory.Exists(p));
        if (chosen == null)
        {
            Debug.LogError($"[YapYapLanguageAPI] Model folder not found for language '{lang.id}'. Tried:\n  {string.Join("\n  ", uniq)}\nAborting StartVosk to avoid breaking audio.");
            return true; // fall back to game's behavior
        }

        LanguageRegistry.GrammarById.TryGetValue(lang.id, out var grammarDict);
        var grammar = (grammarDict ?? new System.Collections.Generic.Dictionary<string,string[]>())
            .SelectMany(x => x.Value).Distinct().ToList();

        Debug.Log($"[YapYapLanguageAPI] Starting Vosk for '{lang.id}' (modelPath='{chosen}') grammarWords={grammar.Count}");

        try
        {
            vm.Vosk.StartVosk(grammar, chosen, 3);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[YapYapLanguageAPI] StartVosk failed for '{lang.id}': {ex}. Aborting and falling back.");
            return true;
        }

        return false; // we handled it
    }
}
