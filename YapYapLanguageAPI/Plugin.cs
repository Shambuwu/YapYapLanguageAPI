using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("yapyap.language.api", "YapYap Language API", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("YapYapLanguageAPI loaded");

        var harmony = new Harmony("yapyap.language.api");
        harmony.PatchAll();
    }
}
