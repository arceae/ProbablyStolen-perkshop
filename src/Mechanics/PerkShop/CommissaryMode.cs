using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private void EnterTalentMode()
        {
            try
            {
                var manager = _commissaryManager;
                var store = GetStore();
                if (manager == null || !CanUseInRun(store))
                {
                    return;
                }

                _catalogBuilt = false;
                EnsureCatalog();
                _talentMode = true;
                var tradeButton = FindCommissaryTradeButton(manager);
                if (tradeButton != null)
                {
                    _commissaryTradeButton = tradeButton;
                    RebindButton(tradeButton.gameObject, "返回交易", delegate
                    {
                        try
                        {
                            manager.CloseUI();
                        }
                        catch
                        {
                        }
                    });
                }

                SetTalentTitle(manager, "体悟");
                RefreshTalentCards(manager, store);
                PerkShopLog.Msg("Talent mode opened.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("EnterTalentMode failed: " + ex);
            }
        }

        private Button FindCommissaryTradeButton(CommissaryUIManager manager)
        {
            try
            {
                var buttons = manager.gameObject.GetComponentsInChildren<Button>(true);
                if (buttons != null)
                {
                    for (var i = 0; i < buttons.Length; i++)
                    {
                        var button = buttons[i];
                        if (button == null)
                        {
                            continue;
                        }

                        var text = GetButtonText(button.transform);
                        if (!string.IsNullOrEmpty(text) && (text.Contains("兑换") || text.Contains("交易")))
                        {
                            return button;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}

