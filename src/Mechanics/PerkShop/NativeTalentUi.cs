using System;
using System.Collections;
using System.Collections.Generic;
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
        private GameObject _nativeTalentPanel;
        private TMP_FontAsset _nativeTalentFont;
        private readonly List<TalentCardUi> _nativeTalentCards = new List<TalentCardUi>();
        private Button _nativeTalentPageButton;
        private TextMeshProUGUI _nativeTalentPointsText;
        private int _nativeTalentPage;
        private Sprite _nativeWhiteSprite;

        private IEnumerator BuildNativeTalentPanelNextFrame(CommissaryUIManager manager)
        {
            yield return null;
            yield return null;
            try
            {
                if (!_talentMode || manager == null || manager.panel == null)
                {
                    yield break;
                }

                _catalogBuilt = false;
                EnsureCatalog();
                DestroyNativeTalentPanel();
                var sourceRect = manager.panel.GetComponent<RectTransform>();
                var parent = manager.panel.transform.parent;
                if (sourceRect == null || parent == null)
                {
                    yield break;
                }

                _nativeTalentFont = manager.redeemedCredit != null ? manager.redeemedCredit.font : null;
                if (_nativeTalentFont == null)
                {
                    var anyText = manager.panel.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (anyText != null) _nativeTalentFont = anyText.font;
                }

                var canvas = manager.panel.GetComponentInParent<Canvas>();
                var rootParent = canvas != null ? canvas.transform : parent;
                var root = CreateFullScreenRect("NativePerkTalentPanel", rootParent);
                _nativeTalentPanel = root.gameObject;
                AddImage(root, new Color(0.04f, 0.07f, 0.12f, 0.96f), null, 0f);
                root.SetAsLastSibling();

                var content = CreateRect(root, "Content", Vector2.zero, new Vector2(1200f, 700f));
                var scale = 1f;
                try
                {
                    var canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
                    var available = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
                    if (available.x > 1f && available.y > 1f)
                    {
                        scale = Mathf.Min(1f, Mathf.Min(available.x / 1200f, available.y / 700f));
                    }
                }
                catch
                {
                    scale = 1f;
                }

                content.localScale = Vector3.one * Mathf.Clamp(scale, 0.65f, 2.5f);

                CreateText(content, "体悟", new Vector2(-430f, 255f), new Vector2(200f, 48f), 30, new Color(0.82f, 0.94f, 0.97f, 1f), TextAlignmentOptions.Left);
                _nativeTalentPointsText = CreateText(content, string.Empty, new Vector2(80f, 250f), new Vector2(620f, 42f), 25, new Color(0.82f, 0.94f, 0.97f, 1f), TextAlignmentOptions.Center);
                CreateButton(content, "×", new Vector2(575f, 250f), new Vector2(54f, 48f), new Color(0.18f, 0.28f, 0.36f, 1f), new Color(0.72f, 0.92f, 0.97f, 1f), CloseNativeTalentPanel);

                var cardWidth = 150f;
                var cardHeight = 250f;
                var gap = 14f;
                for (var i = 0; i < 7; i++)
                {
                    var x = -((7f - 1f) * (cardWidth + gap)) / 2f + i * (cardWidth + gap);
                    _nativeTalentCards.Add(CreateTalentCard(content, new Vector2(x, 10f), new Vector2(cardWidth, cardHeight), i));
                }

                _nativeTalentPageButton = CreateButton(content, "下一页", new Vector2(0f, -235f), new Vector2(300f, 48f), new Color(0.16f, 0.25f, 0.33f, 1f), new Color(0.72f, 0.92f, 0.97f, 1f), NextNativeTalentPage);
                manager.panel.SetActive(false);
                RefreshNativeTalentPanel();
                PerkShopLog.Msg("Native talent panel opened.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("BuildNativeTalentPanelNextFrame failed: " + ex);
            }
        }

        private RectTransform CreateRectObject(string name, Transform parent, RectTransform source)
        {
            var obj = new GameObject(name);
            obj.AddComponent<RectTransform>();
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreateFullScreenRect(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.AddComponent<RectTransform>();
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }
        private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.AddComponent<RectTransform>();
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private Sprite GetNativeWhiteSprite()
        {
            if (_nativeWhiteSprite == null)
            {
                _nativeWhiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            }

            return _nativeWhiteSprite;
        }

        private Image AddImage(RectTransform rect, Color color, Color? outline, float outlineDistance)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = GetNativeWhiteSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;
            if (outline.HasValue && outlineDistance > 0f)
            {
                var effect = rect.gameObject.AddComponent<Outline>();
                effect.effectColor = outline.Value;
                effect.effectDistance = new Vector2(outlineDistance, -outlineDistance);
            }

            return image;
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rect = CreateRect(parent, "Text", position, size);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _nativeTalentFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color background, Color textColor, Action action)
        {
            var rect = CreateRect(parent, "Button", position, size);
            var image = AddImage(rect, background, new Color(0.45f, 0.78f, 0.88f, 1f), 2f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener((UnityAction)(() => action()));
            var label = CreateText(rect, text, Vector2.zero, size, 20, textColor, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private void CloseNativeTalentPanel()
        {
            _talentMode = false;
            DestroyNativeTalentPanel();
            try
            {
                _commissaryManager?.CloseUI();
            }
            catch
            {
            }
        }

        private void DestroyNativeTalentPanel()
        {
            if (_nativeTalentPanel != null)
            {
                UnityEngine.Object.Destroy(_nativeTalentPanel);
            }

            _nativeTalentPanel = null;
            _nativeTalentCards.Clear();
            _nativeTalentPageButton = null;
            _nativeTalentPointsText = null;
            _nativeTalentPage = 0;
        }

        private sealed class TalentCardUi
        {
            public GameObject Root;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Description;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI Stock;
            public Button Buy;
        }
    }
}


