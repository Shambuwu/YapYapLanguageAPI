using BepInEx;
using HarmonyLib;
using System.IO;
using UnityEngine;

[BepInPlugin("yapyap.language.api", "YapYap Language API", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    void Awake()
    {
        string baseDir = Paths.PluginPath;
        Directory.CreateDirectory(Path.Combine(baseDir, "Models"));
        Directory.CreateDirectory(Path.Combine(baseDir, "Localisation"));

        LanguageRegistry.LoadAll(baseDir);

        // quick runtime check / debug info
        string jsonPath = Path.Combine(baseDir, "languages.json");
        Debug.Log($"[YapYapLanguageAPI] Plugin Awake: baseDir='{baseDir}', languages.json exists={File.Exists(jsonPath)}");
        Debug.Log($"[YapYapLanguageAPI] LanguageRegistry.Languages.Count = {LanguageRegistry.Languages?.Count ?? 0}");

        new Harmony("yapyap.language.api").PatchAll();
        Logger.LogInfo("YapYap Language API loaded");
    }
}
