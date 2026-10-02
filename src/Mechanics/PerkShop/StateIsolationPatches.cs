using System;
using HarmonyLib;
using Il2Cpp;

namespace PerkShopFramework
{
    [HarmonyPatch(typeof(PlayerStore), "LoadGame")]
    internal static class PerkShopLoadGamePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                PerkShopMod.Instance?.InvalidateLoadedState();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("LoadGame 补丁失败：" + ex);
            }
        }
    }
}