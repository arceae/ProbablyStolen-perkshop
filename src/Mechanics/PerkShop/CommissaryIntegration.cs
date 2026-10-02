using System;
using Il2Cpp;
using MelonLoader;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        public void EnsureCommissaryButton(CommissaryUIManager manager)
        {
            if (manager != null)
            {
                _commissaryManager = manager;
                _nextFoodStationScanTime = 0f;
                ScanFoodStationButton();
            }
        }

        public void QueueTalentMode()
        {
            _talentMode = true;
            var manager = _commissaryManager ?? CommissaryUIManager.Instance;
            if (manager == null || manager.panel == null)
            {
                PerkShopLog.Warning("CommissaryUIManager or panel is null.");
                return;
            }

            // The in-panel button already has a live native panel. Do not call
            // OpenUI() again and do not leave that panel visible for two frames;
            // otherwise the native trade window can appear behind/in front of ours.
            if (!manager.panel.activeInHierarchy)
            {
                manager.OpenUI();
            }

            manager.panel.SetActive(false);
            MelonCoroutines.Start(BuildNativeTalentPanelNextFrame(manager));
        }

        public void OnCommissaryRefreshed()
        {
            // Original panel is never modified.
        }

        public void OnCommissaryClosed()
        {
            ClearCommissaryButton();
            _talentMode = false;
            DestroyNativeTalentPanel();
            DestroyTalentPanelClone();
        }
    }
}
