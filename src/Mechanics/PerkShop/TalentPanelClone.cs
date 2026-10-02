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
        private GameObject _talentPanelClone;
        private List<CommissaryElement> _talentCards;
        private Button _talentPageButton;
        private int _talentPage;

        private IEnumerator BuildTalentPanelCloneNextFrame(CommissaryUIManager manager)
        {
            yield return null;
            yield return null;

            try
            {
                if (!_talentMode || manager == null || manager.panel == null)
                {
                    yield break;
                }

                DestroyTalentPanelClone();
                var parent = manager.panel.transform.parent;
                if (parent == null)
                {
                    yield break;
                }

                _talentPanelClone = UnityEngine.Object.Instantiate<GameObject>(manager.panel, parent);
                _talentPanelClone.name = "PerkTalentPanel";
                _talentPanelClone.transform.SetAsLastSibling();

                var sourceRect = manager.panel.GetComponent<RectTransform>();
                var cloneRect = _talentPanelClone.GetComponent<RectTransform>();
                if (sourceRect != null && cloneRect != null)
                {
                    cloneRect.anchorMin = sourceRect.anchorMin;
                    cloneRect.anchorMax = sourceRect.anchorMax;
                    cloneRect.pivot = sourceRect.pivot;
                    cloneRect.anchoredPosition = sourceRect.anchoredPosition;
                    cloneRect.sizeDelta = sourceRect.sizeDelta;
                }

                var sourceManager = _talentPanelClone.GetComponent<CommissaryUIManager>();
                if (sourceManager != null)
                {
                    sourceManager.enabled = false;
                }

                _talentPanelClone.SetActive(true);
                manager.panel.SetActive(false);
                _talentCards = new List<CommissaryElement>(_talentPanelClone.GetComponentsInChildren<CommissaryElement>(true));
                _talentPage = 0;
                PrepareClonedPanelTexts();
                PrepareClonedPanelButtons();
                RefreshClonedTalentPanel();
                PerkShopLog.Msg("Talent panel clone opened. cards=" + (_talentCards != null ? _talentCards.Count : 0));
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("BuildTalentPanelCloneNextFrame failed: " + ex);
            }
        }

        private void PrepareClonedPanelTexts()
        {
            if (_talentPanelClone == null)
            {
                return;
            }

            foreach (var tmp in _talentPanelClone.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (string.IsNullOrEmpty(tmp.text))
                {
                    continue;
                }

                if (tmp.text.Contains("配给站"))
                {
                    tmp.text = "体悟";
                }
                else if (tmp.text.Contains("已兑换") || tmp.text.Contains("票券"))
                {
                    tmp.text = "剩余天赋点：" + _points + "   累计交易额：" + _tradeVolume;
                }
            }
        }

        private void PrepareClonedPanelButtons()
        {
            _talentPageButton = null;
            if (_talentPanelClone == null)
            {
                return;
            }

            var buttons = _talentPanelClone.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var text = GetButtonText(button.transform);
                var name = button.name != null ? button.name.ToLowerInvariant() : string.Empty;
                if (!string.IsNullOrEmpty(text) && (text.Contains("兑换") || text.Contains("交易")))
                {
                    _talentPageButton = button;
                    RebindButton(button.gameObject, "下一页", NextTalentPage);
                }
                else if ((!string.IsNullOrEmpty(text) && (text.Contains("返回") || text.Contains("关闭") || text == "×" || text == "X")) || name.Contains("close"))
                {
                    RebindButton(button.gameObject, "返回", CloseClonedTalentPanel);
                }
            }
        }

        private void NextTalentPage()
        {
            var pageSize = _talentCards != null ? Math.Max(1, _talentCards.Count) : 1;
            var totalPages = Math.Max(1, (_catalog.Count + pageSize - 1) / pageSize);
            if (_talentPage < totalPages - 1)
            {
                _talentPage++;
                RefreshClonedTalentPanel();
            }
            else
            {
                CloseClonedTalentPanel();
            }
        }

        private void CloseClonedTalentPanel()
        {
            _talentMode = false;
            DestroyTalentPanelClone();
            try
            {
                _commissaryManager?.CloseUI();
            }
            catch
            {
            }
        }

        private void DestroyTalentPanelClone()
        {
            if (_talentPanelClone != null)
            {
                UnityEngine.Object.Destroy(_talentPanelClone);
            }

            _talentPanelClone = null;
            _talentCards = null;
            _talentPageButton = null;
            _talentPage = 0;
        }
    }
}
