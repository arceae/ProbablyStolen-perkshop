using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private void RefreshClonedTalentPanel()
        {
            try
            {
                var store = GetStore();
                if (_talentPanelClone == null || _talentCards == null || _talentCards.Count == 0 || !CanUseInRun(store))
                {
                    return;
                }

                var pageSize = _talentCards.Count;
                var totalPages = Math.Max(1, (_catalog.Count + pageSize - 1) / pageSize);
                if (_talentPage >= totalPages)
                {
                    _talentPage = totalPages - 1;
                }

                if (_talentPage < 0)
                {
                    _talentPage = 0;
                }

                for (var i = 0; i < _talentCards.Count; i++)
                {
                    var element = _talentCards[i];
                    if (element == null)
                    {
                        continue;
                    }

                    var index = _talentPage * pageSize + i;
                    if (index >= _catalog.Count)
                    {
                        element.gameObject.SetActive(false);
                        continue;
                    }

                    var perk = _catalog[index];
                    var id = GetPerkId(perk);
                    var owned = IsOwned(store, id);
                    var incompatible = !owned && IsIncompatible(store, perk);
                    var cost = Math.Max(0, perk.cost);
                    var canBuy = !owned && !incompatible && _points >= cost;

                    element.gameObject.SetActive(true);
                    var oldName = element.productName != null ? element.productName.text : null;
                    var oldPrice = element.price != null ? element.price.text : null;
                    var oldStock = element.stock != null ? element.stock.text : null;
                    var texts = element.gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
                    TextMeshProUGUI nameTarget = null;
                    TextMeshProUGUI priceTarget = null;
                    TextMeshProUGUI stockTarget = null;
                    if (texts != null)
                    {
                        for (var t = 0; t < texts.Length; t++)
                        {
                            if (nameTarget == null && !string.IsNullOrEmpty(oldName) && texts[t].text == oldName)
                            {
                                nameTarget = texts[t];
                            }
                            else if (priceTarget == null && !string.IsNullOrEmpty(oldPrice) && texts[t].text == oldPrice)
                            {
                                priceTarget = texts[t];
                            }
                            else if (stockTarget == null && !string.IsNullOrEmpty(oldStock) && texts[t].text == oldStock)
                            {
                                stockTarget = texts[t];
                            }
                        }

                        if (nameTarget == null && texts.Length > 0) nameTarget = texts[0];
                        if (priceTarget == null && texts.Length > 1) priceTarget = texts[1];
                        if (stockTarget == null && texts.Length > 2) stockTarget = texts[2];
                    }

                    if (nameTarget != null) nameTarget.text = GetDisplayName(perk);
                    if (priceTarget != null) priceTarget.text = cost + " 天赋点";
                    if (stockTarget != null) stockTarget.text = owned ? "已拥有" : (incompatible ? "不可体悟" : "可体悟");

                    var button = element.gameObject.GetComponentInChildren<Button>(true);
                    if (button != null)
                    {
                        var captured = perk;
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener((UnityAction)(() =>
                        {
                            TryPurchase(captured);
                            RefreshClonedTalentPanel();
                        }));
                        button.interactable = canBuy;
                        SetButtonLabel(button.transform, "购买");
                    }
                }

                if (_talentPageButton != null)
                {
                    if (_talentPage < totalPages - 1)
                    {
                        RebindButton(_talentPageButton.gameObject, "下一页 " + (_talentPage + 1) + "/" + totalPages, NextTalentPage);
                    }
                    else
                    {
                        RebindButton(_talentPageButton.gameObject, "返回交易", CloseClonedTalentPanel);
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("RefreshClonedTalentPanel failed: " + ex);
            }
        }
    }
}


