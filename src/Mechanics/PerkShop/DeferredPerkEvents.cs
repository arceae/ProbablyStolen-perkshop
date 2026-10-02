using System;
using Il2Cpp;
using MelonLoader;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private const string PendingSawyerCrewDayKey = "PendingSawyerCrewDay";
        private const string SawyerCrewHandledKey = "SawyerCrewHandled";
        private int _pendingSawyerCrewDay;

        private void UpdateDeferredPerkEvents(PlayerStore store)
        {
            if (!CanUseInRun(store) || store.storeClientManager == null)
            {
                return;
            }

            if (_pendingSawyerCrewDay <= 0 || GetCurrentDaySafe() < _pendingSawyerCrewDay)
            {
                return;
            }

            try
            {
                var manager = store.storeClientManager;
                var client = StoreClientListStory.CreateJanitorUndergroundExchangeIntroductionFence();
                if (client == null)
                {
                    return;
                }

                var stack = manager.clientStack;
                if (stack != null)
                {
                    for (var i = 0; i < stack.Count; i++)
                    {
                        var existing = stack[i];
                        if (existing != null && string.Equals(existing.identifier, client.identifier, StringComparison.Ordinal))
                        {
                            CompletePendingSawyerCrewVisit(store);
                            return;
                        }
                    }
                }

                manager.AddNextClient(client);
                CompletePendingSawyerCrewVisit(store);
                PerkShopLog.Msg("Queued deferred Sawyer Crew visit for day " + GetCurrentDaySafe() + ".");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("UpdateDeferredPerkEvents failed: " + ex);
            }
        }

        public void TryRepairLegacyDeferredEvents(PlayerStore store)
        {
            if (!CanUseInRun(store)
                || ReadInt(store, SawyerCrewHandledKey, 0) != 0
                || ReadInt(store, AppliedContentPrefix + "sawyer_crew", 0) == 0
                || _pendingSawyerCrewDay > 0)
            {
                return;
            }

            ScheduleSawyerCrewNextDay(store);
            PerkShopLog.Msg("Legacy Sawyer Crew purchase detected; deferred visit scheduled.");
        }

        private void ScheduleSawyerCrewNextDay(PlayerStore store)
        {
            if (ReadInt(store, SawyerCrewHandledKey, 0) != 0)
            {
                return;
            }

            var targetDay = GetCurrentDaySafe() + 1;
            if (targetDay <= 1)
            {
                targetDay = 2;
            }

            _pendingSawyerCrewDay = Math.Max(_pendingSawyerCrewDay, targetDay);
            WriteInt(store, PendingSawyerCrewDayKey, _pendingSawyerCrewDay);
            _savePending = true;
            PerkShopLog.Msg("Deferred Sawyer Crew visit scheduled for day " + _pendingSawyerCrewDay + ".");
        }

        private void CompletePendingSawyerCrewVisit(PlayerStore store)
        {
            _pendingSawyerCrewDay = 0;
            WriteInt(store, PendingSawyerCrewDayKey, 0);
            WriteInt(store, SawyerCrewHandledKey, 1);
            _savePending = true;
        }
    }
}