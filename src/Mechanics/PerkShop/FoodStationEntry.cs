using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private Button? _foodStationTradeButton;
        private GameObject? _foodStationEnlightenmentButton;
        private Transform? _foodStationRoot;
        private float _nextFoodStationScanTime;

        private void ScanFoodStationButton()
        {
            if (_foodStationEnlightenmentButton != null)
            {
                if (_foodStationEnlightenmentButton.activeInHierarchy)
                {
                    return;
                }

                DestroyInjectedButtons(_foodStationRoot);
                _foodStationEnlightenmentButton = null;
            }

            if (Time.unscaledTime < _nextFoodStationScanTime)
            {
                return;
            }

            _nextFoodStationScanTime = Time.unscaledTime + 1f;

            try
            {
                var manager = _commissaryManager ?? CommissaryUIManager.Instance;
                if (manager == null)
                {
                    return;
                }

                _commissaryManager = manager;
                var root = ResolveCommissaryRoot(manager);
                if (root == null || !root.gameObject.activeInHierarchy)
                {
                    return;
                }

                // 先清理该配给站根节点下所有旧注入，防止旧对象参与下一次查找。
                DestroyInjectedButtons(root);

                var trade = FindCommissaryTradeButton(root);
                if (trade == null || trade.transform.parent == null)
                {
                    return;
                }

                CreateFoodStationButton(root, trade, trade.transform.parent);
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("ScanFoodStationButton failed: " + ex.Message);
            }
        }

        private static Transform? ResolveCommissaryRoot(CommissaryUIManager manager)
        {
            try
            {
                var map = MapUIManager.Instance;
                if (map != null && map.UICommissaryPanel != null && map.UICommissaryPanel.activeInHierarchy)
                {
                    return map.UICommissaryPanel.transform;
                }
            }
            catch
            {
            }

            if (manager?.panel == null || !manager.panel.activeInHierarchy)
            {
                return null;
            }

            var parent = manager.panel.transform.parent;
            if (parent == null)
            {
                return null;
            }

            // 只接受真实配给站根节点。回退到 CanvasPaperUI 会误注入普通讨价还价界面。
            var name = parent.name ?? string.Empty;
            return name.IndexOf("Commissary", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("_UICommissary", StringComparison.OrdinalIgnoreCase) >= 0
                ? parent
                : null;
        }

        private Button? FindCommissaryTradeButton(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons == null)
            {
                return null;
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var label = GetButtonText(button.transform).Trim();
                if (string.Equals(button.name, "TradeButton", StringComparison.Ordinal)
                    || string.Equals(label, "交易", StringComparison.Ordinal)
                    || label.Contains("交易"))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool HasReturnSibling(Transform parent)
        {
            try
            {
                var buttons = parent.GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && string.Equals(GetButtonText(buttons[i].transform).Trim(), "返回", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private void CreateFoodStationButton(Transform root, Button trade, Transform parent)
        {
            try
            {
                _foodStationRoot = root;
                _foodStationTradeButton = trade;

                // 再次去重，避免同一帧或异常恢复路径留下同名对象。
                DestroyInjectedButtons(root);

                _foodStationEnlightenmentButton = UnityEngine.Object.Instantiate<GameObject>(trade.gameObject, parent);
                _foodStationEnlightenmentButton.name = "PerkShopEnlightenmentButton";

                var layout = _foodStationEnlightenmentButton.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = _foodStationEnlightenmentButton.AddComponent<LayoutElement>();
                }

                layout.ignoreLayout = true;
                var cloneRect = _foodStationEnlightenmentButton.GetComponent<RectTransform>();
                var tradeRect = trade.GetComponent<RectTransform>();
                if (cloneRect != null && tradeRect != null)
                {
                    cloneRect.anchorMin = tradeRect.anchorMin;
                    cloneRect.anchorMax = tradeRect.anchorMax;
                    cloneRect.pivot = tradeRect.pivot;
                    cloneRect.sizeDelta = tradeRect.sizeDelta;
                    cloneRect.anchoredPosition = tradeRect.anchoredPosition + new Vector2(0f, Math.Abs(tradeRect.sizeDelta.y) + 12f);
                }

                var button = _foodStationEnlightenmentButton.GetComponent<Button>();
                if (button == null)
                {
                    button = _foodStationEnlightenmentButton.GetComponentInChildren<Button>(true);
                }

                if (button == null)
                {
                    UnityEngine.Object.Destroy(_foodStationEnlightenmentButton);
                    _foodStationEnlightenmentButton = null;
                    return;
                }

                // Clear serialized persistent listeners copied from the native button.
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    button.onClick.SetPersistentListenerState(i, (UnityEventCallState)0);
                }

                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)(() => OpenTalentShopFromExchange()));
                button.interactable = true;
                _foodStationEnlightenmentButton.transform.SetAsLastSibling();
                _foodStationEnlightenmentButton.SetActive(true);
                StartButtonLabelRefresh(_foodStationEnlightenmentButton.transform, "体悟");
                PerkShopLog.Msg("Food station enlightenment button added. root=" + parent.name);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("CreateFoodStationButton failed: " + ex);
            }
        }

        private void DestroyInjectedButtons(Transform? root)
        {
            try
            {
                if (root == null)
                {
                    return;
                }

                var buttons = root.GetComponentsInChildren<Button>(true);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (button == null || !string.Equals(button.name, "PerkShopEnlightenmentButton", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var go = button.gameObject;
                    go.SetActive(false);
                    UnityEngine.Object.Destroy(go);
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("DestroyInjectedButtons failed: " + ex.Message);
            }
        }
        public void ClearCommissaryButton()
        {
            if (_foodStationRoot != null)
            {
                DestroyInjectedButtons(_foodStationRoot);
            }

            if (_foodStationEnlightenmentButton != null)
            {
                _foodStationEnlightenmentButton.SetActive(false);
                UnityEngine.Object.Destroy(_foodStationEnlightenmentButton);
            }

            _foodStationEnlightenmentButton = null;
            _foodStationTradeButton = null;
            _foodStationRoot = null;
            _commissaryTradeButton = null;
        }
    }

}
