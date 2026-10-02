using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine.EventSystems;

namespace PerkShopFramework
{
    [HarmonyPatch(typeof(StartingPerkElement), "OnPointerClick", new Type[] { typeof(PointerEventData) })]
    internal static class StartingPerkClickFixPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            try
            {
                var ui = PerkUIController.Instance;
                if (ui == null)
                {
                    return;
                }

                var newGame = NewGameData.Instance;
                if (newGame != null && !newGame.isInMainMenu)
                {
                    // 原版起始特性界面在部分加载顺序下会保持 isInMainMenu=false，
                    // 点击处理会走取消/空操作分支。这里只在该界面点击时恢复正确状态。
                    newGame.isInMainMenu = true;
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("StartingPerk click fix failed: " + ex.Message);
            }
        }
    }
}