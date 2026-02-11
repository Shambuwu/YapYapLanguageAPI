using BepInEx;
using HarmonyLib;
using System.IO;

[BepInPlugin("yapyap.language.api", "YapYap Language API", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    void Awake()
    {
        string baseDir = Paths.PluginPath;

        // Use the specific subfolder where you place languages, models and localisation
        string pluginSub = Path.Combine(baseDir, "GOOGNA_DEV_SQUAD-YapYapMoreLanguages", "YapYapMoreLanguages");
        Directory.CreateDirectory(pluginSub);
        Directory.CreateDirectory(Path.Combine(pluginSub, "Models"));
        Directory.CreateDirectory(Path.Combine(pluginSub, "Localisation"));

        LanguageRegistry.LoadAll(pluginSub);

        new Harmony("yapyap.language.api").PatchAll();
        Logger.LogInfo("YapYap Language API loaded");
    }
}
