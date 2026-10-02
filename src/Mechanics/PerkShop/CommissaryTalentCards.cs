using System;
using System.Collections;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private void RefreshTalentCards(CommissaryUIManager manager, PlayerStore store)
        {
            if (manager == null || manager.commissaryElements == null)
            {
                return;
            }

            var elements = manager.commissaryElements;
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null)
                {
                    continue;
                }

                if (i >= _catalog.Count)
                {
                    element.gameObject.SetActive(false);
                    continue;
                }

                var perk = _catalog[i];
                var id = GetPerkId(perk);
                var owned = IsOwned(store, id);
                var incompatible = !owned && IsIncompatible(store, perk);
                var cost = Math.Max(0, perk.cost);
                var canBuy = !owned && !incompatible && _points >= cost;

                element.gameObject.SetActive(true);
                element.id = id;
                if (element.productName != null) element.productName.text = GetDisplayName(perk);
                if (element.price != null) element.price.text = cost + " 天赋点";
                if (element.stock != null) element.stock.text = owned ? "已拥有" : (incompatible ? "不可体悟" : "可体悟");
                if (element.buyButton != null)
                {
                    var captured = perk;
                    element.buyButton.onClick.RemoveAllListeners();
                    element.buyButton.onClick.AddListener((UnityAction)(() =>
                    {
                        TryPurchase(captured);
                        RefreshTalentCards(manager, GetStore());
                    }));
                    element.buyButton.interactable = canBuy;
                    SetButtonLabel(element.buyButton.transform, "购买");
                }
            }

            if (manager.redeemedCredit != null)
            {
                manager.redeemedCredit.text = "剩余天赋点：" + _points + "   累计交易额：" + _tradeVolume;
            }
        }

        private void RebindButton(GameObject gameObject, string label, Action action)
        {
            if (gameObject == null)
            {
                return;
            }

            var button = gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.GetComponentInChildren<Button>(true);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener((UnityAction)(() => action()));
                button.interactable = true;
            }

            StartButtonLabelRefresh(gameObject.transform, label);
        }

        private static string GetButtonText(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            try
            {
                var tmp = transform.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    return tmp.text ?? string.Empty;
                }

                var text = transform.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    return text.text ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static void SetButtonLabel(Transform root, string label)
        {
            if (root == null)
            {
                return;
            }

            try
            {
                foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    tmp.text = label;
                }

                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    text.text = label;
                }
            }
            catch
            {
            }
        }

        private void StartButtonLabelRefresh(Transform root, string label)
        {
            MelonCoroutines.Start(RefreshButtonLabelRoutine(root, label));
        }

        private static IEnumerator RefreshButtonLabelRoutine(Transform root, string label)
        {
            for (var i = 0; i < 30; i++)
            {
                SetButtonLabel(root, label);
                yield return null;
            }
        }

        private static void SetTalentTitle(CommissaryUIManager manager, string title)
        {
            try
            {
                if (manager == null || manager.panel == null)
                {
                    return;
                }

                foreach (var tmp in manager.panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (!string.IsNullOrEmpty(tmp.text) && (tmp.text.Contains("配给站") || tmp.text.Contains("体悟")))
                    {
                        tmp.text = title;
                    }
                }
            }
            catch
            {
            }
        }
    }
}

