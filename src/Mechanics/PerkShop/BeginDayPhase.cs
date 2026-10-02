using System;
using HarmonyLib;
using Il2Cpp;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private const string BeginDayPendingKey = "BeginDayPending";
        private bool _deferredRepairsPending;

        internal void HandleEndNight()
        {
            try
            {
                var store = GetStore();
                if (!CanUseInRun(store))
                {
                    return;
                }

                EnsureLoaded(store);
                WriteInt(store, BeginDayPendingKey, 1);
                _savePending = true;
                PerkShopLog.Debug("已登记下一次 BeginDay 处理。");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("EndNight 登记失败：" + ex);
            }
        }

        internal void HandleBeginDay()
        {
            try
            {
                var store = GetStore();
                if (!CanUseInRun(store))
                {
                    return;
                }

                EnsureLoaded(store);

                if (_deferredRepairsPending)
                {
                    RunDeferredRepairs(store);
                    _deferredRepairsPending = false;
                }

                UpdateDeferredPerkEvents(store);
                UpdateMentorEvents(store, true);
                WriteInt(store, BeginDayPendingKey, 0);
                _savePending = true;
                PerkShopLog.Debug("BeginDay 阶段处理完成。day=" + GetCurrentDaySafe());
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("BeginDay 执行失败：" + ex);
            }
        }

        private void RunDeferredRepairs(PlayerStore store)
        {
            TryRepairLegacyExtraPerksItems(store);
            TryRepairLegacyDeferredEvents(store);
            TryRepairLegacyFutureTechItems(store);
            TryRepairLegacyTapeheadCassettes(store);
            TryRepairLegacyWagesPerksItems(store);
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "EndNight")]
    internal static class PerkShopEndNightPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                PerkShopMod.Instance?.HandleEndNight();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("EndNight 补丁失败：" + ex);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "BeginDay")]
    internal static class PerkShopBeginDayPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                PerkShopMod.Instance?.HandleBeginDay();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("BeginDay 补丁失败：" + ex);
            }
        }
    }
}