using System;
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private ExchangeUIManager _exchangeManager;
        private GameObject _exchangeEnlightenmentButton;

        public void EnsureExchangeEnlightenmentButton(ExchangeUIManager manager)
        {
            if (manager == null)
            {
                return;
            }

            _exchangeManager = manager;
            MelonCoroutines.Start(SetupExchangeButtonNextFrame(manager));
        }

        private IEnumerator SetupExchangeButtonNextFrame(ExchangeUIManager manager)
        {
            yield return null;
            yield return null;
            try
            {
                if (manager == null || manager.panel == null)
                {
                    yield break;
                }

                var tradeButton = FindExchangeTradeButton(manager);
                if (tradeButton == null)
                {
                    PerkShopLog.Warning("Exchange trade button not found.");
                    yield break;
                }

                if (_exchangeEnlightenmentButton == null)
                {
                    var parent = tradeButton.transform.parent;
                    if (parent == null)
                    {
                        yield break;
                    }

                    _exchangeEnlightenmentButton = UnityEngine.Object.Instantiate<GameObject>(tradeButton.gameObject, parent);
                    _exchangeEnlightenmentButton.name = "PerkShopEnlightenmentButton";
                    var layout = _exchangeEnlightenmentButton.GetComponent<LayoutElement>();
                    if (layout == null)
                    {
                        layout = _exchangeEnlightenmentButton.AddComponent<LayoutElement>();
                    }

                    layout.ignoreLayout = true;
                    var cloneRect = _exchangeEnlightenmentButton.GetComponent<RectTransform>();
                    var tradeRect = tradeButton.GetComponent<RectTransform>();
                    if (cloneRect != null && tradeRect != null)
                    {
                        cloneRect.anchorMin = tradeRect.anchorMin;
                        cloneRect.anchorMax = tradeRect.anchorMax;
                        cloneRect.pivot = tradeRect.pivot;
                        cloneRect.sizeDelta = tradeRect.sizeDelta;
                        cloneRect.anchoredPosition = tradeRect.anchoredPosition + new Vector2(0f, Math.Abs(tradeRect.sizeDelta.y) + 12f);
                    }

                    var button = _exchangeEnlightenmentButton.GetComponent<Button>();
                    if (button == null)
                    {
                        button = _exchangeEnlightenmentButton.GetComponentInChildren<Button>(true);
                    }

                    if (button == null)
                    {
                        UnityEngine.Object.Destroy(_exchangeEnlightenmentButton);
                        _exchangeEnlightenmentButton = null;
                        yield break;
                    }

                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener((UnityAction)(() => OpenTalentShopFromExchange()));
                    button.interactable = true;
                    _exchangeEnlightenmentButton.transform.SetAsLastSibling();
                    _exchangeEnlightenmentButton.SetActive(true);
                    StartButtonLabelRefresh(_exchangeEnlightenmentButton.transform, "体悟");
                }
                else
                {
                    _exchangeEnlightenmentButton.SetActive(true);
                    SetButtonLabel(_exchangeEnlightenmentButton.transform, "体悟");
                }

                PerkShopLog.Msg("Exchange enlightenment button added.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("SetupExchangeButtonNextFrame failed: " + ex);
            }
        }

        private Button FindExchangeTradeButton(ExchangeUIManager manager)
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
                        if (!string.IsNullOrEmpty(text) && text.Contains("交易"))
                        {
                            return button;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("FindExchangeTradeButton failed: " + ex.Message);
            }

            return null;
        }

        private void OpenTalentShopFromExchange()
        {
            try
            {
                try
                {
                    _exchangeManager?.ClosePanel();
                }
                catch
                {
                }

                QueueTalentMode();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("OpenTalentShopFromExchange failed: " + ex);
            }
        }
    }


    [HarmonyPatch(typeof(CommissaryUIManager), "CloseUI")]
    internal static class PerkShopCommissaryClosePatch
    {
        private static void Postfix()
        {
            try { PerkShopMod.Instance?.OnCommissaryClosed(); }
            catch (Exception ex) { PerkShopLog.Error("Commissary CloseUI 补丁失败：" + ex); }
        }
    }
}


