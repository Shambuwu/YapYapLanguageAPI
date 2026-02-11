using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YAPYAP;
using BepInEx;

[HarmonyPatch(typeof(UISettings), "Awake")]
static class Patch_UISettings_Awake
{
    // track added language ids to make the operation idempotent across multiple Awake calls
    private static readonly HashSet<string> s_addedLanguageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static void Postfix(UISettings __instance)
    {
        try
        {
            Debug.Log("[YapYapLanguageAPI] Postfix UISettings.Awake running.");
            if (!Service.Get<VoiceManager>(out var vm))
            {
                Debug.LogWarning("[YapYapLanguageAPI] VoiceManager service not found.");
                return;
            }

            var field = typeof(VoiceManager)
                .GetField("_voskLocalisations", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning("[YapYapLanguageAPI] VoiceManager._voskLocalisations field not found.");
                return;
            }

            var current = field.GetValue(vm) as VoskLocalisation[] ?? new VoskLocalisation[0];

            int added = 0;
            foreach (var def in LanguageRegistry.Languages ?? Enumerable.Empty<LanguageDef>())
            {
                // skip if we've already added this id earlier in this process
                if (!string.IsNullOrEmpty(def?.id) && s_addedLanguageIds.Contains(def.id))
                {
                    Debug.Log($"[YapYapLanguageAPI] Skipping '{def.displayName}' because id '{def.id}' already added.");
                    continue;
                }

                // skip if a matching localisation already present (by Language property or name)
                if (current.Any(x => x != null && IsMatch(x, def)))
                {
                    Debug.Log($"[YapYapLanguageAPI] Skipping '{def.displayName}' because matching VoskLocalisation already exists.");
                    continue;
                }

                // also skip if any existing entry uses the same display name (case-insensitive)
                string defName = def?.displayName ?? string.Empty;
                if (!string.IsNullOrEmpty(defName) &&
                    current.Any(v =>
                    {
                        try
                        {
                            var pn = v.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                            var name = pn?.GetValue(v) as string;
                            return !string.IsNullOrEmpty(name) && string.Equals(name, defName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch { return false; }
                    }))
                {
                    Debug.Log($"[YapYapLanguageAPI] Skipping '{def.displayName}' because an entry with same Name exists.");
                    // mark as added to avoid future attempts
                    if (!string.IsNullOrEmpty(def?.id)) s_addedLanguageIds.Add(def.id);
                    continue;
                }

                // create and populate a VoskLocalisation instance
                var loc = ScriptableObject.CreateInstance<VoskLocalisation>();

                // set language and name
                SetPrivate(loc, "_language", ParseSystemLanguage(def.systemLanguage));
                SetPrivate(loc, "_name", def.displayName);

                // set model path: prefer absolute path recorded in registry, fallback to def.modelFolder
                string modelPath = def != null && !string.IsNullOrEmpty(def.id) && LanguageRegistry.ModelPathById.TryGetValue(def.id, out var abs)
                    ? abs
                    : def?.modelFolder;
                SetPrivate(loc, "_modelPath", modelPath);

                // ensure filename is filename-only to avoid duplication when VoiceManager combines base path
                string filenameOnly = string.IsNullOrEmpty(def?.localisationFile) ? string.Empty : Path.GetFileName(def.localisationFile);
                SetPrivate(loc, "_filename", filenameOnly);
                Debug.Log($"[YapYapLanguageAPI] For language '{def?.id}' set _filename = '{filenameOnly}' (source value: '{def?.localisationFile}')");

                // attempt to copy the plugin-local localisation file to the game's expected localisation folder
                try
                {
                    if (!string.IsNullOrEmpty(filenameOnly) && !string.IsNullOrEmpty(def?.localisationFile) && !string.IsNullOrEmpty(def?.sourcePluginPath))
                    {
                        // Use the sourcePluginPath to build the correct source path
                        string normalizedLocFile = NormalizePath(def.localisationFile);
                        string srcPath = Path.Combine(def.sourcePluginPath, normalizedLocFile);

                        string destDir = Path.Combine(Application.dataPath, "StreamingAssets", "Vosk", "Localisation");
                        string dest = Path.Combine(destDir, filenameOnly);

                        if (File.Exists(srcPath))
                        {
                            Directory.CreateDirectory(destDir);
                            bool doCopy = true;
                            if (File.Exists(dest))
                            {
                                var srcInfo = new FileInfo(srcPath);
                                var dstInfo = new FileInfo(dest);
                                doCopy = srcInfo.Length != dstInfo.Length || srcInfo.LastWriteTimeUtc > dstInfo.LastWriteTimeUtc;
                            }

                            if (doCopy)
                            {
                                File.Copy(srcPath, dest, true);
                                Debug.Log($"[YapYapLanguageAPI] Copied localisation '{filenameOnly}' from '{srcPath}' to '{dest}'");
                            }
                            else
                            {
                                Debug.Log($"[YapYapLanguageAPI] Localisation '{filenameOnly}' already up-to-date at '{dest}'");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[YapYapLanguageAPI] Localisation source not found at: {srcPath}\nExpected dest: {dest}");
                        }
                    }
                }
                catch (Exception exCopy)
                {
                    Debug.LogWarning($"[YapYapLanguageAPI] Failed to copy localisation file for '{def?.id}': {exCopy}");
                }

                current = current.Concat(new[] { loc }).ToArray();
                added++;

                if (!string.IsNullOrEmpty(def?.id))
                    s_addedLanguageIds.Add(def.id);

                Debug.Log($"[YapYapLanguageAPI] Created VoskLocalisation stub: Name='{def.displayName}', id='{def.id}'");
            }

            if (added > 0)
            {
                field.SetValue(vm, current);
                Debug.Log($"[YapYapLanguageAPI] Added {added} localisation(s). New count = {current.Length}");
                try
                {
                    vm.ReloadLocalisation();
                    Debug.Log("[YapYapLanguageAPI] Called VoiceManager.ReloadLocalisation()");
                }
                catch (Exception exReload)
                {
                    Debug.LogError($"[YapYapLanguageAPI] ReloadLocalisation threw: {exReload}");
                }
            }
            else
            {
                Debug.Log("[YapYapLanguageAPI] No new localisations added.");
            }

            // --- Dropdown update (preserve built-ins and append mod languages) ---
            try
            {
                var uiField = typeof(UISettings).GetField("voiceLanguageSetting", BindingFlags.NonPublic | BindingFlags.Instance);
                if (uiField == null)
                {
                    Debug.LogWarning("[YapYapLanguageAPI] UISettings.voiceLanguageSetting field not found; cannot refresh dropdown.");
                    return;
                }

                var dropdown = uiField.GetValue(__instance);
                if (dropdown == null)
                {
                    Debug.LogWarning("[YapYapLanguageAPI] UISettings.voiceLanguageSetting is null; cannot refresh dropdown.");
                    return;
                }

                var dt = dropdown.GetType();
                var methods = dt.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MethodInfo getter = methods.FirstOrDefault(m =>
                {
                    if (m.GetParameters().Length != 0) return false;
                    var rt = m.ReturnType;
                    return typeof(IEnumerable<string>).IsAssignableFrom(rt) || rt == typeof(string[]) || rt == typeof(List<string>);
                });

                MethodInfo setter = methods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        if (ps.Length != 1) return false;
                        var pt = ps[0].ParameterType;
                        return typeof(IEnumerable<string>).IsAssignableFrom(pt) || pt == typeof(string[]);
                    });

                List<string> baseOptions = new List<string>();
                if (getter != null)
                {
                    try
                    {
                        var got = getter.Invoke(dropdown, null);
                        if (got is IEnumerable<string> ie) baseOptions.AddRange(ie);
                        else if (got is string[] sa) baseOptions.AddRange(sa);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[YapYapLanguageAPI] Exception calling dropdown getter: {ex}");
                    }
                }
                else
                {
                    // build baseOptions from VoiceManager.VoskLocalisations (preserve built-in languages)
                    try
                    {
                        var vmList = vm.VoskLocalisations;
                        if (vmList != null)
                        {
                            foreach (var v in vmList)
                            {
                                if (v == null) continue;
                                string name = null;
                                var pn = v.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                                if (pn != null) name = pn.GetValue(v) as string;
                                if (string.IsNullOrEmpty(name))
                                {
                                    var pl = v.GetType().GetProperty("Language", BindingFlags.Public | BindingFlags.Instance)?.GetValue(v);
                                    name = pl?.ToString();
                                }
                                if (string.IsNullOrEmpty(name)) name = v.ToString();
                                if (!baseOptions.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                                    baseOptions.Add(name);
                            }
                        }
                    }
                    catch (Exception exVm)
                    {
                        Debug.LogWarning($"[YapYapLanguageAPI] Failed to read vm.VoskLocalisations: {exVm}");
                    }
                }

                var finalOptions = new List<string>(baseOptions);
                foreach (var ld in LanguageRegistry.Languages)
                {
                    if (string.IsNullOrEmpty(ld?.displayName)) continue;
                    if (!finalOptions.Any(x => string.Equals(x, ld.displayName, StringComparison.OrdinalIgnoreCase)))
                        finalOptions.Add(ld.displayName);
                }

                finalOptions = finalOptions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (setter != null)
                {
                    var paramType = setter.GetParameters()[0].ParameterType;
                    object param = null;
                    if (paramType == typeof(string[])) param = finalOptions.ToArray();
                    else if (paramType.IsAssignableFrom(typeof(List<string>))) param = finalOptions;
                    else if (typeof(IEnumerable<string>).IsAssignableFrom(paramType)) param = finalOptions;

                    if (param != null)
                    {
                        try
                        {
                            setter.Invoke(dropdown, new object[] { param });
                            Debug.Log($"[YapYapLanguageAPI] Invoked dropdown setter to refresh options.");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[YapYapLanguageAPI] Invoking dropdown setter threw: {ex}");
                        }
                    }
                }
            }
            catch (Exception exUi)
            {
                Debug.LogError($"[YapYapLanguageAPI] Exception during dropdown inspection/update: {exUi}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[YapYapLanguageAPI] Patch_UISettings_Awake failed: {ex}");
        }
    }

    private static void SetPrivate(object obj, string field, object value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) f.SetValue(obj, value);
    }

    private static SystemLanguage ParseSystemLanguage(String value)
    {
        if (System.Enum.TryParse<SystemLanguage>(value, true, out var lang))
            return lang;
        return SystemLanguage.English;
    }

    private static bool IsMatch(VoskLocalisation loc, LanguageDef def)
    {
        try
        {
            var p = loc.GetType().GetProperty("Language", BindingFlags.Public | BindingFlags.Instance);
            if (p != null)
            {
                var v = p.GetValue(loc);
                if (v is SystemLanguage sl)
                    return string.Equals(sl.ToString(), def.systemLanguage, StringComparison.OrdinalIgnoreCase);
            }

            var pn = loc.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (pn != null)
            {
                var name = pn.GetValue(loc) as string;
                if (!string.IsNullOrEmpty(name) && string.Equals(name, def.displayName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* swallow */ }

        return false;
    }

    /// <summary>
    /// Normalize path separators to platform-specific format
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace forward slashes with platform's directory separator
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }
}
