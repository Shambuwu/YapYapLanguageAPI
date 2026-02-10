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

    public static void LoadAll(string baseDir)
    {
        string jsonPath = Path.Combine(baseDir, "languages.json");
        Debug.Log($"[YapYapLanguageAPI] LoadAll called. baseDir='{baseDir}', jsonPath='{jsonPath}', exists={File.Exists(jsonPath)}");

        if (!File.Exists(jsonPath))
        {
            Debug.Log("[YapYapLanguageAPI] languages.json not found — writing default example file.");
            File.WriteAllText(jsonPath,
@"{
  ""languages"": [
    {
      ""id"": ""dutch"",
      ""displayName"": ""Nederlands (Community)"",
      ""systemLanguage"": ""Dutch"",
      ""modelFolder"": ""Models/dutch"",
      ""localisationFile"": ""Localisation/dutch.txt"",
      ""fallback"": ""english""
    }
  ]
}");
            Debug.Log($"[YapYapLanguageAPI] Wrote default languages.json at: {jsonPath}");
        }

        string fileText;
        try
        {
            fileText = File.ReadAllText(jsonPath);
            Debug.Log($"[YapYapLanguageAPI] Read languages.json ({fileText.Length} chars).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[YapYapLanguageAPI] Failed to read languages.json: {ex}");
            Languages = new List<LanguageDef>();
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
                                Languages = parsedList;
                                Debug.Log($"[YapYapLanguageAPI] Fallback parsed {Languages.Count} language(s).");
                            }
                            else
                            {
                                Languages = new List<LanguageDef>();
                                Debug.LogWarning("[YapYapLanguageAPI] Fallback parse found no language objects.");
                            }
                        }
                        else
                        {
                            Languages = new List<LanguageDef>();
                            Debug.LogWarning("[YapYapLanguageAPI] languages array is empty in JSON.");
                        }
                    }
                    else
                    {
                        Languages = new List<LanguageDef>();
                        Debug.LogWarning("[YapYapLanguageAPI] Could not locate languages array brackets.");
                    }
                }
                else
                {
                    Languages = new List<LanguageDef>();
                    Debug.LogWarning("[YapYapLanguageAPI] 'languages' key not found in JSON.");
                }
            }
            catch (Exception ex)
            {
                Languages = new List<LanguageDef>();
                Debug.LogError($"[YapYapLanguageAPI] Exception during fallback parse: {ex}");
            }
        }
        else
        {
            Languages = defs.languages;
            Debug.Log($"[YapYapLanguageAPI] Loaded {Languages.Count} language(s) from languages.json.");
        }

        // If still empty, warn and return
        if (Languages == null || Languages.Count == 0)
        {
            Debug.LogWarning("[YapYapLanguageAPI] No languages parsed from languages.json.");
            // Ensure dictionaries are cleared
            GrammarById.Clear();
            ModelPathById.Clear();
            return;
        }

        GrammarById.Clear();
        ModelPathById.Clear();

        foreach (var lang in Languages)
        {
            string modelPath = Path.Combine(baseDir, lang.modelFolder);
            ModelPathById[lang.id] = modelPath;

            var dict = new Dictionary<string, string[]>();
            string locPath = Path.Combine(baseDir, lang.localisationFile);

            if (File.Exists(locPath))
            {
                foreach (var line in File.ReadAllLines(locPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int idx = line.IndexOf("::", StringComparison.Ordinal);
                    if (idx < 0) continue;

                    string key = line.Substring(0, idx).Trim();
                    string[] words = line.Substring(idx + 2)
                        .Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    dict[key] = words;
                }
            }

            GrammarById[lang.id] = dict;
        }
    }
}
