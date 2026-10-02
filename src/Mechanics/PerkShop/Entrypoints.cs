using HarmonyLib;

namespace PerkShopFramework;

public sealed partial class PerkShopMod
{
    private static HarmonyLib.Harmony? HarmonyInstance { get; set; }

    internal static void Initialize(HarmonyLib.Harmony harmony)
    {
        HarmonyInstance = harmony;
        var instance = new PerkShopMod();
        Instance = instance;
        instance.InitializeInternal();
    }

    internal static void Update() => Instance?.UpdateInternal();

    internal static void Shutdown() => Instance?.ShutdownInternal();
}

