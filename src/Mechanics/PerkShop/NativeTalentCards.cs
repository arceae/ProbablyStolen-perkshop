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
        private TalentCardUi CreateTalentCard(RectTransform parent, Vector2 position, Vector2 size, int index)
        {
            var root = CreateRect(parent, "TalentCard_" + index, position, size);
            AddImage(root, new Color(0.14f, 0.17f, 0.22f, 1f), new Color(0.45f, 0.78f, 0.88f, 1f), 2f);

            var card = new TalentCardUi();
            card.Root = root.gameObject;
            card.Name = CreateText(root, string.Empty, new Vector2(0f, 82f), new Vector2(138f, 46f), 18, new Color(0.84f, 0.95f, 0.97f, 1f), TextAlignmentOptions.Center);
            card.Description = CreateText(root, string.Empty, new Vector2(0f, 20f), new Vector2(132f, 70f), 13, new Color(0.55f, 0.85f, 0.90f, 1f), TextAlignmentOptions.Center);
            card.Description.enableWordWrapping = true;
            card.Description.enableAutoSizing = true;
            card.Description.fontSizeMin = 8f;
            card.Description.fontSizeMax = 14f;
            card.Description.overflowMode = TextOverflowModes.Ellipsis;
            card.Price = CreateText(root, string.Empty, new Vector2(0f, -38f), new Vector2(138f, 26f), 18, new Color(0.90f, 0.88f, 0.74f, 1f), TextAlignmentOptions.Center);
            card.Stock = CreateText(root, string.Empty, new Vector2(0f, -63f), new Vector2(138f, 24f), 16, new Color(0.62f, 0.75f, 0.79f, 1f), TextAlignmentOptions.Center);
            card.Buy = CreateButton(root, "体悟", new Vector2(0f, -100f), new Vector2(120f, 34f), new Color(0.18f, 0.28f, 0.36f, 1f), new Color(0.82f, 0.94f, 0.97f, 1f), delegate { });
            return card;
        }

        private void RefreshNativeTalentPanel()
        {
            try
            {
                var store = GetStore();
                if (_nativeTalentPanel == null || _nativeTalentCards.Count == 0 || !CanUseInRun(store))
                {
                    return;
                }

                var pageSize = _nativeTalentCards.Count;
                var totalPages = Math.Max(1, (_catalog.Count + pageSize - 1) / pageSize);
                if (_nativeTalentPage >= totalPages)
                {
                    _nativeTalentPage = totalPages - 1;
                }

                if (_nativeTalentPage < 0)
                {
                    _nativeTalentPage = 0;
                }

                if (_nativeTalentPointsText != null)
                {
                    _nativeTalentPointsText.text = "剩余天赋点：" + _points + "   累计交易额：" + _tradeVolume + "   下一阶：" + GetProgressToNextPoint() + "/" + GetNextPointCost();
                }

                for (var i = 0; i < _nativeTalentCards.Count; i++)
                {
                    var card = _nativeTalentCards[i];
                    var index = _nativeTalentPage * pageSize + i;
                    if (index >= _catalog.Count)
                    {
                        card.Root.SetActive(false);
                        continue;
                    }

                    var perk = _catalog[index];
                    var id = GetPerkId(perk);
                    var owned = IsOwned(store, id);
                    var incompatible = !owned && IsIncompatible(store, perk);
                    var cost = Math.Max(0, perk.cost);
                    var canBuy = !owned && !incompatible && _points >= cost;

                    card.Root.SetActive(true);
                    card.Name.text = GetDisplayName(perk);
                    var description = GetDescription(perk);
                    if (description.Length > 120) description = description.Substring(0, 120) + "...";
                    card.Description.text = description;
                    card.Price.text = cost + " 天赋点";
                    card.Stock.text = owned ? "已拥有" : (incompatible ? "不可体悟" : "可体悟");
                    card.Buy.onClick.RemoveAllListeners();
                    var captured = perk;
                    card.Buy.onClick.AddListener((UnityAction)(() =>
                    {
                        TryPurchase(captured);
                        RefreshNativeTalentPanel();
                    }));
                    card.Buy.interactable = canBuy;
                    SetButtonLabel(card.Buy.transform, "体悟");
                }

                if (_nativeTalentPageButton != null)
                {
                    _nativeTalentPageButton.onClick.RemoveAllListeners();
                    if (_nativeTalentPage < totalPages - 1)
                    {
                        _nativeTalentPageButton.onClick.AddListener((UnityAction)(() => NextNativeTalentPage()));
                        SetButtonLabel(_nativeTalentPageButton.transform, "下一页 " + (_nativeTalentPage + 1) + "/" + totalPages);
                    }
                    else
                    {
                        _nativeTalentPageButton.onClick.AddListener((UnityAction)(() => CloseNativeTalentPanel()));
                        SetButtonLabel(_nativeTalentPageButton.transform, "返回交易");
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("RefreshNativeTalentPanel failed: " + ex);
            }
        }

        private void NextNativeTalentPage()
        {
            var pageSize = Math.Max(1, _nativeTalentCards.Count);
            var totalPages = Math.Max(1, (_catalog.Count + pageSize - 1) / pageSize);
            if (_nativeTalentPage < totalPages - 1)
            {
                _nativeTalentPage++;
                RefreshNativeTalentPanel();
            }
            else
            {
                CloseNativeTalentPanel();
            }
        }
    }
}


