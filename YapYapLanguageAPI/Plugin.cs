using BepInEx;
using HarmonyLib;
using System.IO;

[BepInPlugin("yapyap.language.api", "YapYap Language API", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    private const string LANGUAGE_CONFIG_FILE = "yapyap_custom_languages.json";
    
    void Awake()
    {
        Logger.LogInfo("YapYap Language API initializing...");

        // Scan all plugin directories for yapyap_languages.json files
        string pluginRoot = Paths.PluginPath;
        if (!Directory.Exists(pluginRoot))
        {
            Logger.LogError($"Plugin path does not exist: {pluginRoot}");
            return;
        }

        int discovered = 0;
        foreach (var pluginDir in Directory.GetDirectories(pluginRoot))
        {
            // Look for yapyap_languages.json in the root of each plugin folder
            string languagesJson = Path.Combine(pluginDir);
            if (File.Exists(languagesJson))
            {
                Logger.LogInfo($"Found {LANGUAGE_CONFIG_FILE} in: {pluginDir}");
                LanguageRegistry.LoadFromPlugin(pluginDir);
                discovered++;
            }
            else
            {
                // Also check one level deeper (for plugins with subfolders)
                foreach (var subDir in Directory.GetDirectories(pluginDir))
                {
                    string subLanguagesJson = Path.Combine(subDir, LANGUAGE_CONFIG_FILE);
                    if (File.Exists(subLanguagesJson))
                    {
                        Logger.LogInfo($"Found {LANGUAGE_CONFIG_FILE} in: {subDir}");
                        LanguageRegistry.LoadFromPlugin(subDir);
                        discovered++;
                    }
                }
            }
        }

        if (discovered == 0)
        {
            Logger.LogWarning($"No language packs discovered. Modders should create plugins with {LANGUAGE_CONFIG_FILE} files.");
        }
        else
        {
            Logger.LogInfo($"Discovered {discovered} language pack(s). Total languages loaded: {LanguageRegistry.Languages.Count}");
        }

        new Harmony("yapyap.language.api").PatchAll();
        Logger.LogInfo("YapYap Language API loaded");
    }
}
