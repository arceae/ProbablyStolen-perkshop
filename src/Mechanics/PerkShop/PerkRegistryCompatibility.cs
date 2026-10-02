using System;
using System.Collections.Generic;
using Il2Cpp;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        public void CaptureRegistrySnapshot()
        {
            try
            {
                var perks = StartingPerkList.Perks;
                if (perks == null)
                {
                    PerkShopLog.Warning("Registry snapshot skipped because StartingPerkList.Perks is null.");
                    return;
                }

                var captured = new List<StartingPerk>(perks.Count);
                for (var i = 0; i < perks.Count; i++)
                {
                    var perk = perks[i];
                    if (perk != null)
                    {
                        captured.Add(perk);
                    }
                }

                if (captured.Count == 0)
                {
                    PerkShopLog.Warning("Registry snapshot skipped because StartingPerkList.Perks is empty.");
                    return;
                }

                _registryPerks.Clear();
                _registryPerks.AddRange(captured);
                _catalogBuilt = false;
                _catalog.Clear();
                PerkShopLog.Msg("Registry snapshot captured: " + _registryPerks.Count);
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("CaptureRegistrySnapshot failed: " + ex.Message);
            }
        }

        public void InvalidateCatalogOnly()
        {
            CaptureRegistrySnapshot();
        }
    }
}