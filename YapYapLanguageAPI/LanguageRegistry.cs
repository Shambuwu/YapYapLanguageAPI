using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class LanguageDef
{
    public string id;
    public string displayName;
    public string systemLanguage;
    public string modelFolder;
    public string localisationFile;
    public string fallback;

    // Internal: track which plugin this language came from
    [NonSerialized]
    public string sourcePluginPath;
}

[Serializable]
public class LanguageDefList
{
    public List<LanguageDef> languages;
}

public static class LanguageRegistry
{
    public static List<LanguageDef> Languages = new List<LanguageDef>();
    public static Dictionary<string, Dictionary<string, string[]>> GrammarById =
        new Dictionary<string, Dictionary<string, string[]>>();
    public static Dictionary<string, string> ModelPathById =
        new Dictionary<string, string>();

    /// <summary>
    /// Load languages from a specific plugin directory containing languages.json
    /// </summary>
    public static void LoadFromPlugin(string pluginDir)
    {
        string jsonPath = Path.Combine(pluginDir, "yapyap_custom_languages.json");
        Debug.Log($"[YapYapLanguageAPI] LoadFromPlugin called. pluginDir='{pluginDir}', jsonPath='{jsonPath}', exists={File.Exists(jsonPath)}");

        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning($"[YapYapLanguageAPI] yapyap_custom_languages.json not found in {pluginDir}");
            return;
        }

        string fileText;
        try
        {
            fileText = File.ReadAllText(jsonPath);
            Debug.Log($"[YapYapLanguageAPI] Read yapyap_custom_languages.json ({fileText.Length} chars) from {pluginDir}.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[YapYapLanguageAPI] Failed to read yapyap_custom_languages.json from {pluginDir}: {ex}");
            return;
        }

        LanguageDefList defs = null;
        try
        {
            defs = JsonUtility.FromJson<LanguageDefList>(fileText);
            Debug.Log($"[YapYapLanguageAPI] JsonUtility.FromJson returned {(defs == null ? "null" : "non-null")}.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[YapYapLanguageAPI] JSON parse exception: {ex}");
        }

        // Fallback: try to parse array elements individually if the wrapper didn't populate
        if (defs == null || defs.languages == null)
        {
            Debug.LogWarning("[YapYapLanguageAPI] JsonUtility didn't populate languages array — attempting fallback parse.");

            try
            {
                int idx = fileText.IndexOf("\"languages\"", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int start = fileText.IndexOf('[', idx);
                    int end = fileText.IndexOf(']', start + 1);
                    if (start >= 0 && end > start)
                    {
                        string inner = fileText.Substring(start + 1, end - start - 1).Trim();
                        if (!string.IsNullOrEmpty(inner))
                        {
                            // break objects by replacing "},{" with "}|{" and split
                            var items = inner.Replace("},{", "}|{").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            var parsedList = new List<LanguageDef>();
                            foreach (var raw in items)
                            {
                                var objJson = raw.Trim();
                                if (!objJson.StartsWith("{")) objJson = "{" + objJson;
                                if (!objJson.EndsWith("}")) objJson = objJson + "}";
                                try
                                {
                                    var ld = JsonUtility.FromJson<LanguageDef>(objJson);
                                    if (ld != null) parsedList.Add(ld);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[YapYapLanguageAPI] Fallback parse failed for segment: {ex.Message}");
                                }
                            }

                            if (parsedList.Count > 0)
                            {
                                defs = new LanguageDefList { languages = parsedList };
                                Debug.Log($"[YapYapLanguageAPI] Fallback parsed {parsedList.Count} language(s).");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YapYapLanguageAPI] Exception during fallback parse: {ex}");
                return;
            }
        }

        if (defs == null || defs.languages == null || defs.languages.Count == 0)
        {
            Debug.LogWarning($"[YapYapLanguageAPI] No languages found in {pluginDir}/languages.json");
            return;
        }

        // Process each language definition
        foreach (var lang in defs.languages)
        {
            if (lang == null || string.IsNullOrEmpty(lang.id))
            {
                Debug.LogWarning("[YapYapLanguageAPI] Skipping language with null or empty id.");
                continue;
            }

            // Check for duplicate IDs
            if (Languages.Exists(l => l.id == lang.id))
            {
                Debug.LogWarning($"[YapYapLanguageAPI] Language id '{lang.id}' already loaded. Skipping duplicate from {pluginDir}.");
                continue;
            }

            // Store the source plugin path for reference
            lang.sourcePluginPath = pluginDir;

            // Resolve model path relative to plugin directory
            string modelPath = Path.Combine(pluginDir, lang.modelFolder ?? string.Empty);
            ModelPathById[lang.id] = modelPath;

            // Load localisation/grammar file
            var dict = new Dictionary<string, string[]>();
            string locPath = Path.Combine(pluginDir, lang.localisationFile ?? string.Empty);

            if (File.Exists(locPath))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(locPath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        int colonIdx = line.IndexOf("::", StringComparison.Ordinal);
                        if (colonIdx < 0) continue;

                        string key = line.Substring(0, colonIdx).Trim();
                        string[] words = line.Substring(colonIdx + 2)
                            .Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        dict[key] = words;
                    }
                    Debug.Log($"[YapYapLanguageAPI] Loaded {dict.Count} grammar entries for '{lang.id}' from {locPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[YapYapLanguageAPI] Failed to load localisation file {locPath}: {ex}");
                }
            }
            else
            {
                Debug.LogWarning($"[YapYapLanguageAPI] Localisation file not found: {locPath}");
            }

            GrammarById[lang.id] = dict;
            Languages.Add(lang);

            Debug.Log($"[YapYapLanguageAPI] Registered language '{lang.displayName}' (id: {lang.id}) from plugin: {pluginDir}");
        }

        Debug.Log($"[YapYapLanguageAPI] Finished loading from {pluginDir}. Total languages: {Languages.Count}");
    }
}
