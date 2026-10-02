using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppTMPro;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, string>;
using Il2CppPerkList = Il2CppSystem.Collections.Generic.List<Il2Cpp.StartingPerk>;


namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        public static PerkShopMod Instance { get; private set; }

        private const string VolumeKey = "TradeVolume";
        private const string PointsKey = "Points";
        private const string VolumeAtLastPointKey = "VolumeAtLastPoint";
        private const string PointsEarnedKey = "PointsEarned";
        private const string StateRunIdKey = "StateRunId";
        private const string StateVersionKey = "StateVersion";
        private const string AppliedContentPrefix = "AppliedContent.";

        private MelonPreferences_Entry<int> _creditsPerPoint;
        private MelonPreferences_Entry<float> _pointCostGrowth;
        private MelonPreferences_Entry<float> _latePointCostGrowth;
        private MelonPreferences_Entry<int> _costCurveVersion;
        private MelonPreferences_Entry<bool> _includeNegativePerks;

        private HarmonyLib.Harmony _harmony;

        private readonly List<StartingPerk> _catalog = new List<StartingPerk>();
        private readonly List<StartingPerk> _registryPerks = new List<StartingPerk>();
        private bool _catalogBuilt;
        private bool _menuOpen;
        private bool _savePending;
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(100f, 60f, 720f, 560f);
        private int _page;
        private const int PageSize = 7;
        private Font _uiFont;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _windowStyle;
        private bool _stylesReady;
        private readonly List<ClickTarget> _clickTargets = new List<ClickTarget>();
        private int _lastClickFrame = -1;
        private string _loadedRunId;
        private long _tradeVolume;
        private long _volumeAtLastPoint;
        private int _points;
        private int _pointsEarned;
        private GameObject _nativeButton;
        private MapUIManager _nativeButtonOwner;
        private CommissaryUIManager _commissaryManager;
        private GameObject _commissaryEnlightenmentButton;
        private Button _commissaryTradeButton;
        private bool _talentMode;
        private Texture2D _frameTexture;
        private Texture2D _panelTexture;
        private Texture2D _borderTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;

        internal void InitializeInternal()
        {
            Instance = this;
            var category = MelonPreferences.CreateCategory("PerkShopFramework", "PerkShop Framework");
            _creditsPerPoint = category.CreateEntry<int>("CreditsPerPoint", 250, "Base credits per point", "First perk point cost.");
            _pointCostGrowth = category.CreateEntry<float>("PointCostGrowth", 1.5f, "Point cost growth", "Growth for the first four perk point costs.");
            _latePointCostGrowth = category.CreateEntry<float>("LatePointCostGrowth", 1.5f, "Late point cost growth", "Growth after the fourth perk point.");
            _costCurveVersion = category.CreateEntry<int>("CostCurveVersion", 0, "Cost curve version", "Internal migration marker.");
            if (_costCurveVersion.Value < 3)
            {
                _creditsPerPoint.Value = 777;
                _pointCostGrowth.Value = 3.75f;
                _latePointCostGrowth.Value = 1.5f;
                _costCurveVersion.Value = 3;
            }
            _includeNegativePerks = category.CreateEntry<bool>("IncludeNegativePerks", false, "Include negative perks", "Allow negative starting perks in the shop.");

            MelonPreferences.Save();

            _harmony = HarmonyInstance;

            TryInstallOptionalCompatibilityPatches(_harmony);
            MelonEvents.OnGUI.Subscribe(DrawGui);
            PerkShopLog.Msg("PerkShopFramework loaded. Native map button enabled.");
        }

internal void UpdateInternal()
        {
            var store = GetStore();
            if (!CanUseInRun(store) || !IsStoreUiReady(store))
            {
                return;
            }

            try
            {
                EnsureLoaded(store);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("状态加载失败：" + ex);
                return;
            }

            try
            {
                UpdateMentorEvents(store, false);
                if (IsPerkShopMentorCurrent(store)
                    && Time.frameCount != _mentorAdvanceQueuedFrame
                    && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                {
                    var dialogUi = DialogUIManager.Instance;
                    if (dialogUi != null && dialogUi.isWaitingForManualAdvance)
                    {
                        _mentorAdvanceQueuedFrame = Time.frameCount;
                        MelonCoroutines.Start(TryAdvanceMentorAfterFrame(dialogUi));
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("导师对话推进失败：" + ex);
            }

            try
            {
                if (_menuOpen && Input.GetMouseButtonDown(0) && Time.frameCount != _lastClickFrame)
                {
                    _lastClickFrame = Time.frameCount;
                    ProcessClicksFromInput();
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("菜单点击处理失败：" + ex);
            }

            try
            {
                ScanFoodStationButton();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("配给站按钮扫描失败：" + ex);
            }

            try
            {
                PurchasedPerkContentDispatcher.Tick();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("购买天赋内容调度失败：" + ex);
            }

            try
            {
                // 不要把保存放在顾客对话、交易、逮捕或枪战过程中。
                // AnyTimeSave 等运行时快照若抓到这种瞬态，会给 SL 留下残缺顾客队列。
                if (_savePending && CanSaveNow(store))
                {
                    SaveNow(store, "pending");
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("延迟保存失败：" + ex);
            }
        }

        internal void ShutdownInternal()
        {
            try
            {
                MelonEvents.OnGUI.Unsubscribe(DrawGui);
            }
            catch
            {
            }

            try
            {
                _harmony?.UnpatchSelf();
                HarmonyInstance?.UnpatchSelf();
            }
            catch
            {
            }
        }

        internal static bool IsTrackableTrade(GameItem item, int cost)
        {
            // 这是只读交易计量，不修改目标物品；仍需确保当前 MOD 和游戏对象处于有效运行状态。
            return item != null && cost > 0 && Instance != null;
        }

        internal static void AddPurchaseVolume(GameItem item, int cost)
        {
            if (!IsTrackableTrade(item, cost)) return;
            Instance?.AddTradeVolume(Math.Abs(cost), "buy");
        }

        internal static void AddSaleVolume(GameItem item, int cost)
        {
            if (!IsTrackableTrade(item, cost)) return;
            Instance?.AddTradeVolume(Math.Abs(cost), "sell");
        }

        private static bool CanSaveNow(PlayerStore store)
        {
            try
            {
                if (store == null)
                {
                    return false;
                }

                if (store.currentClientInstance != null
                    || store.isClientArrived
                    || store.currentNegociatedItem != null
                    || store.isClientBeingArrested
                    || store.isHandlingShootout)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool IsStoreUiReady(PlayerStore store)
        {
            try
            {
                // 建档/开局选择阶段不要运行本 MOD 的商店和导师逻辑。
                return store != null && store.storeClientManager != null && StoreUIManager.Instance != null;
            }
            catch
            {
                return false;
            }
        }

        private PlayerStore GetStore()
        {
            try
            {
                return PlayerStore.Instance;
            }
            catch
            {
                return null;
            }
        }

        private static bool CanUseInRun(PlayerStore store)
        {
            return store != null && store.saveSlotId >= 0 && !string.IsNullOrEmpty(store.runID);
        }

        private void EnsureLoaded(PlayerStore store)
        {
            if (!CanUseInRun(store) || store.runID == _loadedRunId)
            {
                return;
            }

            _loadedRunId = store.runID;

            var stateVersion = ReadInt(store, StateVersionKey, 0);
            var savedRunId = ReadString(store, StateRunIdKey, string.Empty);
            var trusted = stateVersion >= 2
                && string.Equals(savedRunId, store.runID, StringComparison.Ordinal);

            if (!trusted)
            {
                ClearPerkShopState(store);
                _tradeVolume = 0L;
                _volumeAtLastPoint = 0L;
                _points = 0;
                _pointsEarned = 0;
                _mentorIntroSeen = false;
                _mentorPromptedLevel = 0;
                _mentorNextVisitDay = -1;
                _mentorVisitCount = 0;
                _mentorNativeTutorialDone = false;
                _mentorVisitQueued = false;
                _pendingSawyerCrewDay = 0;
                PerkShopLog.Warning("PerkShop state was reset because run/save identity did not match. run=" + store.runID + " slot=" + store.saveSlotId);
            }
            else
            {
                _tradeVolume = ReadLong(store, VolumeKey, 0L);
                _points = ReadInt(store, PointsKey, 0);
                _pointsEarned = ReadInt(store, PointsEarnedKey, -1);
                _volumeAtLastPoint = ReadLong(store, VolumeAtLastPointKey, -1L);
                var expectedLevel = EstimatePointsEarned(_tradeVolume);
                if (_pointsEarned < 0)
                {
                    _pointsEarned = expectedLevel;
                }
                else if (expectedLevel > _pointsEarned)
                {
                    var restoredPoints = expectedLevel - _pointsEarned;
                    _points += restoredPoints;
                    _pointsEarned = expectedLevel;
                    PerkShopLog.Warning("Restored previously missed perk points: " + restoredPoints);
                }

                _volumeAtLastPoint = GetTotalCostForLevels(_pointsEarned);

                _mentorIntroSeen = ReadInt(store, MentorIntroSeenKey, 0) != 0;
                _mentorPromptedLevel = ReadInt(store, MentorPromptedLevelKey, _pointsEarned);
                _mentorNextVisitDay = ReadInt(store, MentorNextVisitDayKey, -1);
                _mentorVisitCount = ReadInt(store, MentorVisitCountKey, _mentorIntroSeen ? 1 + Math.Max(0, _mentorPromptedLevel) : 0);
                _mentorNativeTutorialDone = ReadInt(store, MentorNativeTutorialDoneKey, 0) != 0;
                _pendingSawyerCrewDay = ReadInt(store, PendingSawyerCrewDayKey, 0);
                }

            WriteString(store, StateRunIdKey, store.runID);
            WriteInt(store, StateVersionKey, 2);
            _volumeAtLastPoint = GetTotalCostForLevels(_pointsEarned);
            WriteLong(store, VolumeKey, _tradeVolume);
            WriteLong(store, VolumeAtLastPointKey, _volumeAtLastPoint);
            WriteInt(store, PointsEarnedKey, _pointsEarned);
            WriteInt(store, PointsKey, _points);
            WriteInt(store, MentorIntroSeenKey, _mentorIntroSeen ? 1 : 0);
            WriteInt(store, MentorPromptedLevelKey, _mentorPromptedLevel);
            WriteInt(store, MentorNextVisitDayKey, _mentorNextVisitDay);
            WriteInt(store, MentorVisitCountKey, _mentorVisitCount);
            WriteInt(store, MentorNativeTutorialDoneKey, _mentorNativeTutorialDone ? 1 : 0);
            WriteInt(store, PendingSawyerCrewDayKey, _pendingSawyerCrewDay);
            _catalogBuilt = false;
            _deferredRepairsPending = true;
            PerkShopLog.Msg("State loaded. run=" + store.runID + " slot=" + store.saveSlotId + " volume=" + _tradeVolume + " points=" + _points + " pointLevel=" + _pointsEarned + " nextCost=" + GetPointCost(_pointsEarned));
        }

        private void AddTradeVolume(int amount, string source)
        {
            if (amount <= 0)
            {
                return;
            }

            var store = GetStore();
            EnsureLoaded(store);
            if (!CanUseInRun(store))
            {
                return;
            }

            _tradeVolume += amount;
            var gained = 0;
            while (_pointsEarned < 10000)
            {
                var nextCost = GetPointCost(_pointsEarned);
                if (_tradeVolume - _volumeAtLastPoint < nextCost)
                {
                    break;
                }
                _pointsEarned++;
                _points++;
                gained++;
            }
            _volumeAtLastPoint = GetTotalCostForLevels(_pointsEarned);

            WriteLong(store, VolumeKey, _tradeVolume);
            WriteLong(store, VolumeAtLastPointKey, _volumeAtLastPoint);
            WriteInt(store, PointsEarnedKey, _pointsEarned);
            WriteInt(store, PointsKey, _points);
            if (gained > 0)
            {
                ScheduleMentorForPoint(store);
                // 只有真正获得天赋点时才请求保存；普通交易额继续留在
                // PlayerStore.modData 中，等待游戏自身的正常存档点。
                _savePending = true;
            }

            PerkShopLog.Msg(source + " volume+" + amount + " total=" + _tradeVolume + " points=" + _points + " level=" + _pointsEarned + " nextCost=" + GetPointCost(_pointsEarned) + (gained > 0 ? " gained=" + gained : ""));
        }

        private long GetPointCost(int level)
        {
            if (level < 0)
            {
                level = 0;
            }

            var baseCost = Math.Max(1, _creditsPerPoint != null ? _creditsPerPoint.Value : 777);
            var growth = Math.Max(1.01f, _pointCostGrowth != null ? _pointCostGrowth.Value : 3.75f);
            var lateGrowth = Math.Max(1.01f, _latePointCostGrowth != null ? _latePointCostGrowth.Value : 1.5f);

            double value;
            if (level <= 3)
            {
                value = baseCost * Math.Pow(growth, level);
            }
            else
            {
                var fourthCost = baseCost * Math.Pow(growth, 3);
                value = fourthCost * Math.Pow(lateGrowth, level - 3);
            }

            if (value >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return Math.Max(1L, (long)Math.Floor(value));
        }
        private long GetTotalCostForLevels(int levels)
        {
            var total = 0L;
            for (var level = 0; level < levels && level < 10000; level++)
            {
                var cost = GetPointCost(level);
                if (cost > long.MaxValue - total)
                {
                    return long.MaxValue;
                }

                total += cost;
            }

            return total;
        }

        private int EstimatePointsEarned(long volume)
        {
            var earned = 0;
            long spent = 0L;
            while (earned < 10000)
            {
                var cost = GetPointCost(earned);
                if (spent > volume - cost)
                {
                    break;
                }

                spent += cost;
                earned++;
            }

            return earned;
        }

        private long GetNextPointCost()
        {
            return GetPointCost(_pointsEarned);
        }

        private long GetProgressToNextPoint()
        {
            return Math.Max(0L, _tradeVolume - _volumeAtLastPoint);
        }
        private void SaveNow(PlayerStore store, string reason)
        {
            try
            {
                store.SaveGame();
                _savePending = false;
                PerkShopLog.Msg("Saved after " + reason + ". volume=" + _tradeVolume + " points=" + _points);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("SaveGame failed: " + ex);
            }
        }

private static long ReadLong(PlayerStore store, string key, long fallback)
        {
            return PerSaveState.GetLong(store, key, fallback);
        }

        private static int ReadInt(PlayerStore store, string key, int fallback)
        {
            var value = ReadLong(store, key, fallback);
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }

        private static void WriteLong(PlayerStore store, string key, long value)
        {
            PerSaveState.SetLong(store, key, value);
        }

        private static string ReadString(PlayerStore store, string key, string fallback)
        {
            return PerSaveState.GetString(store, key) ?? fallback;
        }

        private static void WriteString(PlayerStore store, string key, string value)
        {
            PerSaveState.SetString(store, key, value);
        }

private static void ClearPerkShopState(PlayerStore store)
        {
            foreach (var key in new[] { VolumeKey, PointsKey, VolumeAtLastPointKey, PointsEarnedKey, StateRunIdKey, StateVersionKey, MentorIntroSeenKey, MentorPromptedLevelKey, MentorNextVisitDayKey, MentorVisitCountKey, MentorNativeTutorialDoneKey, PendingSawyerCrewDayKey, SawyerCrewHandledKey, BeginDayPendingKey })
            {
                PerSaveState.Remove(store, key);
            }

            PerSaveState.RemoveByPrefix(store, AppliedContentPrefix);
            PerSaveState.RemoveByPrefix(store, "PendingContent.");
            PerSaveState.RemoveByPrefix(store, "Repair.");
        }

        public void InvalidateLoadedState()
        {
            _loadedRunId = null;
            _tradeVolume = 0L;
            _volumeAtLastPoint = 0L;
            _points = 0;
            _pointsEarned = 0;
            _mentorIntroSeen = false;
            _mentorPromptedLevel = 0;
            _mentorNextVisitDay = -1;
            _mentorVisitCount = 0;
            _mentorNativeTutorialDone = false;
            _pendingSawyerCrewDay = 0;
            _mentorVisitQueued = false;
            _savePending = false;
            _nextMentorAttemptTime = 0f;
            _mentorAdvanceQueuedFrame = -1;
            _catalogBuilt = false;
            PurchasedPerkContentDispatcher.ResetStateForLoad();
        }

        public void ResetForNewRun()
        {
            InvalidateLoadedState();
            var store = GetStore();
            if (!CanUseInRun(store))
            {
                return;
            }

            ClearPerkShopState(store);
            WriteString(store, StateRunIdKey, store.runID);
            WriteInt(store, StateVersionKey, 2);
            WriteLong(store, VolumeKey, 0L);
            WriteLong(store, VolumeAtLastPointKey, 0L);
            WriteInt(store, PointsEarnedKey, 0);
            WriteInt(store, PointsKey, 0);
            WriteInt(store, MentorIntroSeenKey, 0);
            WriteInt(store, MentorPromptedLevelKey, 0);
            WriteInt(store, MentorNextVisitDayKey, -1);
            WriteInt(store, MentorVisitCountKey, 0);
            WriteInt(store, MentorNativeTutorialDoneKey, 0);
            WriteInt(store, PendingSawyerCrewDayKey, 0);
            WriteInt(store, SawyerCrewHandledKey, 0);
        }
        private static void WriteInt(PlayerStore store, string key, int value)
        {
            WriteLong(store, key, value);
        }

        private void EnsureCatalog()
        {
            if (_catalogBuilt && _catalog.Count > 0)
            {
                return;
            }

            if (!CanUseInRun(GetStore()))
            {
                return;
            }

            // 原版建档界面不再被本 MOD 挂 Harmony 补丁。
            // 进入配给站、真正需要目录时，才只读抓取一次 StartingPerkList。
            if (_registryPerks.Count == 0)
            {
                CaptureRegistrySnapshot();
            }

            BuildCatalog();
        }

        private void BuildCatalog()
        {
            _catalog.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var snapshotCount = 0;
            var listCount = 0;

            try
            {
                for (var i = 0; i < _registryPerks.Count; i++)
                {
                    snapshotCount++;
                    AddCatalogPerk(_registryPerks[i], seen);
                }

                var perks = StartingPerkList.Perks;
                if (perks != null)
                {
                    for (var i = 0; i < perks.Count; i++)
                    {
                        listCount++;
                        AddCatalogPerk(perks[i], seen);
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("StartingPerkList.Perks scan failed: " + ex.Message);
            }

            if (_catalog.Count == 0)
            {
                // 不手动调用 StartingPerkList.InitStartingPerk()。
                // 原版建档特性列表的初始化由游戏负责，本 MOD 只读取快照。
                PerkShopLog.Warning("StartingPerkList.Perks is empty; catalog will wait for the native registry snapshot.");
            }

                        _catalog.Sort((a, b) =>
            {
                var idCompare = string.Compare(GetPerkId(a), GetPerkId(b), StringComparison.Ordinal);
                return idCompare;
            });

            _catalogBuilt = true;
            PerkShopLog.Msg("Catalog built: snapshot=" + snapshotCount + " list=" + listCount + " usable=" + _catalog.Count);
        }

        private void AddCatalogPerk(StartingPerk perk, HashSet<string> seen)
        {
            if (perk == null)
            {
                return;
            }

            var id = GetPerkId(perk);
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
            {
                return;
            }

            try
            {
                if (!_includeNegativePerks.Value && perk.type == StartingPerk.StartingPerkType.NEGATIVE)
                {
                    return;
                }
            }
            catch
            {
            }

            _catalog.Add(perk);
        }

        private static string GetPerkId(StartingPerk perk)
        {
            try
            {
                return (perk != null ? perk.id : null)?.TrimEnd('\0') ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool IsOwned(PlayerStore store, string id)
        {
            try
            {
                var active = store.startingPerks;
                if (active == null)
                {
                    return false;
                }

                for (var i = 0; i < active.Count; i++)
                {
                    if (string.Equals(GetPerkId(active[i]), id, StringComparison.Ordinal))
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

        private bool IsIncompatible(PlayerStore store, StartingPerk candidate)
        {
            var candidateId = GetPerkId(candidate);
            try
            {
                var active = store.startingPerks;
                if (active == null)
                {
                    return false;
                }

                for (var i = 0; i < active.Count; i++)
                {
                    var activePerk = active[i];
                    if (activePerk == null)
                    {
                        continue;
                    }

                    if (IncompatibleContains(activePerk, candidateId) || IncompatibleContains(candidate, GetPerkId(activePerk)))
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

        private static bool IncompatibleContains(StartingPerk perk, string id)
        {
            if (perk == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            try
            {
                var list = perk.incompatiblePerks;
                if (list == null)
                {
                    return false;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    var incompatible = (list[i] ?? string.Empty).TrimEnd('\0');
                    if (string.Equals(incompatible, id, StringComparison.Ordinal))
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

        private bool ApplyPerkContentOnPurchase(PlayerStore store, StartingPerk perk)
        {
            TryInstallOptionalCompatibilityPatches(_harmony);
            var id = GetPerkId(perk);
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (ReadInt(store, AppliedContentPrefix + id, 0) != 0)
            {
                return true;
            }

            PurchasedPerkContentDispatcher.Queue(store, perk);
            return true;
        }

        private void TryPurchase(StartingPerk perk)
        {
            try
            {
                if (perk == null)
                {
                    return;
                }

                var store = GetStore();
                if (!CanUseInRun(store))
                {
                    return;
                }

                var id = GetPerkId(perk);
                if (IsOwned(store, id))
                {
                    PerkShopLog.Warning("Perk already owned: " + id);
                    return;
                }

                if (IsIncompatible(store, perk))
                {
                    PerkShopLog.Warning("Perk is incompatible: " + id);
                    return;
                }

                var cost = Math.Max(0, perk.cost);
                if (_points < cost)
                {
                    return;
                }

                if (store.startingPerks == null)
                {
                    PerkShopLog.Error("startingPerks list is null.");
                    return;
                }
                var contentApplied = ApplyPerkContentOnPurchase(store, perk);
                if (!contentApplied)
                {
                    PerkShopLog.Warning("Purchase content did not apply; purchase was not committed. perk=" + id);
                    return;
                }

                store.startingPerks.Add(perk);
                _points -= cost;
                WriteInt(store, PointsKey, _points);
                SaveNow(store, "perk_purchase:" + id);
                var enginePerkId = string.IsNullOrEmpty(perk.id) ? id : perk.id; PerkShopLog.Msg("Purchased perk=" + id + " cost=" + cost + " remainingPoints=" + _points + " contentApplied=" + contentApplied + " active=" + StartingPerk.IsPerkActive(enginePerkId));
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("TryPurchase failed: " + ex);
            }
        }

        private string GetDisplayName(StartingPerk perk)
        {
            try
            {
                var name = perk.GetLocalizedDisplayName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name.TrimEnd('\0');
                }
            }
            catch
            {
            }

            return GetPerkId(perk);
        }

        private string GetDescription(StartingPerk perk)
        {
            try
            {
                return (perk.GetLocalizedDescription() ?? string.Empty).TrimEnd('\0');
            }
            catch
            {
                return string.Empty;
            }
        }

        public void EnsureNativeButton(MapUIManager manager)
        {
            if (manager == null || manager.upgradeMerchantButton == null)
            {
                return;
            }

            if (_nativeButton != null && _nativeButtonOwner == manager)
            {
                _nativeButton.SetActive(true);
                return;
            }

            try
            {
                if (_nativeButton != null)
                {
                    UnityEngine.Object.Destroy(_nativeButton);
                    _nativeButton = null;
                }

                var template = manager.upgradeMerchantButton;
                var parent = template.transform.parent;
                if (parent == null)
                {
                    return;
                }

                var clone = UnityEngine.Object.Instantiate<GameObject>(template, parent);
                clone.name = "PerkShopMapButton";
                var layout = clone.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = clone.AddComponent<LayoutElement>();
                }

                layout.ignoreLayout = true;

                var cloneRect = clone.GetComponent<RectTransform>();
                var templateRect = template.GetComponent<RectTransform>();
                if (cloneRect != null && templateRect != null)
                {
                    cloneRect.anchorMin = templateRect.anchorMin;
                    cloneRect.anchorMax = templateRect.anchorMax;
                    cloneRect.pivot = templateRect.pivot;
                    cloneRect.sizeDelta = templateRect.sizeDelta;

                    var x = templateRect.anchoredPosition.x;
                    var y = templateRect.anchoredPosition.y;
                    var dumpingRect = manager.dumpingGroundButton != null ? manager.dumpingGroundButton.GetComponent<RectTransform>() : null;
                    var bazarRect = manager.bazarButton != null ? manager.bazarButton.GetComponent<RectTransform>() : null;
                    if (dumpingRect != null && bazarRect != null)
                    {
                        x = (dumpingRect.anchoredPosition.x + bazarRect.anchoredPosition.x + templateRect.anchoredPosition.x) / 3f;
                        y = Math.Min(dumpingRect.anchoredPosition.y, Math.Min(bazarRect.anchoredPosition.y, templateRect.anchoredPosition.y)) - Math.Abs(templateRect.sizeDelta.y) - 8f;
                    }

                    cloneRect.anchoredPosition = new Vector2(x, y);
                }

                foreach (var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    tmp.text = "天赋升级";
                }

                foreach (var text in clone.GetComponentsInChildren<Text>(true))
                {
                    text.text = "天赋升级";
                }

                var button = clone.GetComponent<Button>();
                if (button == null)
                {
                    button = clone.GetComponentInChildren<Button>(true);
                }

                if (button == null)
                {
                    UnityEngine.Object.Destroy(clone);
                    PerkShopLog.Warning("Map button template had no Button component.");
                    return;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener((UnityAction)(() => OpenShopFromNativeButton()));
                button.interactable = true;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                }

                clone.transform.SetAsLastSibling();
                clone.SetActive(true);
                _nativeButton = clone;
                _nativeButtonOwner = manager;
                PerkShopLog.Msg("Native map button added.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("EnsureNativeButton failed: " + ex);
            }
        }

        private void OpenShopFromNativeButton()
        {
            try
            {
                _catalogBuilt = false;
                _page = 0;
                EnsureCatalog();
                _menuOpen = true;
                try
                {
                    _nativeButtonOwner?.CloseUI();
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("OpenShopFromNativeButton failed: " + ex);
            }
        }
        public void RefreshCatalogFromUi()
        {
            try
            {
                _catalogBuilt = false;
                EnsureCatalog();
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("RefreshCatalogFromUi failed: " + ex.Message);
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }

            try
            {
                _uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);
            }
            catch
            {
                _uiFont = null;
            }

            _labelStyle = new GUIStyle(GUI.skin.label);
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _boxStyle = new GUIStyle(GUI.skin.box);
            _windowStyle = new GUIStyle(GUI.skin.window);
            if (_uiFont != null)
            {
                _labelStyle.font = _uiFont;
                _buttonStyle.font = _uiFont;
                _boxStyle.font = _uiFont;
                _windowStyle.font = _uiFont;
            }

            _labelStyle.fontSize = 16;
            _buttonStyle.fontSize = 15;
            _windowStyle.fontSize = 16;
            _labelStyle.wordWrap = false;
            _windowStyle.normal.textColor = new Color(0.96f, 0.91f, 0.75f, 1f);
            _stylesReady = true;
        }

        private void DrawGui()
        {
            if (!_menuOpen)
            {
                return;
            }

            var evt = Event.current;
            var oldDepth = GUI.depth;
            GUI.depth = 33000;
            try
            {
                EnsureStyles();
                if (evt != null && evt.type == EventType.Repaint)
                {
                    _clickTargets.Clear();
                    _windowRect = GUI.Window(73031, _windowRect, (GUI.WindowFunction)(Action<int>)DrawWindow, "天赋升级", _windowStyle);
                }

                ConsumePointerEvents(evt);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("GUI failed: " + ex);
            }
            finally
            {
                GUI.depth = oldDepth;
            }
        }

        private void DrawWindow(int windowId)
        {
            var store = GetStore();
            if (!CanUseInRun(store))
            {
                DrawLabel(new Rect(14f, 32f, 520f, 24f), "请先进入一个已激活的存档。");
                DrawButton(new Rect(620f, 28f, 80f, 28f), "关闭", true, delegate { _menuOpen = false; });
                return;
            }

            var nextCost = GetNextPointCost();
            var progress = GetProgressToNextPoint();
            DrawLabel(new Rect(14f, 26f, 430f, 24f), "累计交易额：" + _tradeVolume + "  天赋点：" + _points + "  下一阶：" + nextCost);
            DrawButton(new Rect(430f, 24f, 70f, 28f), "刷新", true, delegate
            {
                _catalogBuilt = false;
                _page = 0;
                EnsureCatalog();
            });
            DrawButton(new Rect(508f, 24f, 70f, 28f), "关闭", true, delegate { _menuOpen = false; });
            DrawLabel(new Rect(14f, 54f, 500f, 24f), "当前阶进度：" + progress + " / " + nextCost);

            var available = new List<StartingPerk>();
            for (var i = 0; i < _catalog.Count; i++)
            {
                var perk = _catalog[i];
                if (perk != null && !IsOwned(store, GetPerkId(perk)))
                {
                    available.Add(perk);
                }
            }

            if (available.Count == 0)
            {
                DrawLabel(new Rect(14f, 100f, 600f, 28f), "没有可购买天赋。");
                return;
            }

            var totalPages = Math.Max(1, (available.Count + PageSize - 1) / PageSize);
            if (_page >= totalPages)
            {
                _page = totalPages - 1;
            }

            if (_page < 0)
            {
                _page = 0;
            }

            var startIndex = _page * PageSize;
            var count = Math.Min(PageSize, available.Count - startIndex);
            for (var i = 0; i < count; i++)
            {
                DrawPerkRow(store, available[startIndex + i], new Rect(6f, 84f + i * 62f, 690f, 58f));
            }

            DrawLabel(new Rect(14f, 524f, 180f, 24f), "第 " + (_page + 1) + " / " + totalPages + " 页");
            DrawButton(new Rect(500f, 518f, 90f, 28f), "上一页", _page > 0, delegate { _page--; });
            DrawButton(new Rect(600f, 518f, 90f, 28f), "下一页", _page < totalPages - 1, delegate { _page++; });
        }

        private void DrawPerkRow(PlayerStore store, StartingPerk perk, Rect row)
        {
            if (perk == null)
            {
                return;
            }

            var id = GetPerkId(perk);
            var incompatible = IsIncompatible(store, perk);
            var cost = Math.Max(0, perk.cost);
            var canBuy = !incompatible && _points >= cost;

            DrawBox(row);
            DrawLabel(new Rect(row.x + 8f, row.y + 4f, 420f, 22f), GetDisplayName(perk) + "  [" + cost + " 天赋点]");
            DrawLabel(new Rect(row.x + 428f, row.y + 4f, 105f, 22f), incompatible ? "互斥" : string.Empty);
            DrawButton(new Rect(row.x + 576f, row.y + 15f, 96f, 28f), "购买", canBuy, delegate { TryPurchase(perk); });

            var description = GetDescription(perk);
            if (description.Length > 72)
            {
                description = description.Substring(0, 72) + "...";
            }

            DrawLabel(new Rect(row.x + 8f, row.y + 26f, 650f, 20f), description);
            DrawLabel(new Rect(row.x + 8f, row.y + 43f, 650f, 18f), "ID：" + id);
        }

        private void DrawLabel(Rect rect, string text)
        {
            var oldColor = GUI.contentColor;
            GUI.contentColor = new Color(0.61f, 0.25f, 0.20f, 1f);
            _labelStyle.Draw(rect, new GUIContent(text), false, false, false, false);
            GUI.contentColor = oldColor;
        }

        private void DrawBox(Rect rect)
        {
            DrawRect(rect, new Color(0.91f, 0.84f, 0.64f, 1f));
            DrawBorder(rect, new Color(0.82f, 0.40f, 0.28f, 1f), 2f);
        }

        private void DrawButton(Rect rect, string text, bool enabled, Action action)
        {
            var hover = enabled && rect.Contains(GuiMouse());
            DrawRect(rect, hover ? new Color(0.92f, 0.50f, 0.34f, 1f) : new Color(0.82f, 0.40f, 0.28f, 1f));
            DrawBorder(rect, new Color(0.62f, 0.28f, 0.23f, 1f), 2f);
            var oldColor = GUI.contentColor;
            GUI.contentColor = enabled ? new Color(0.97f, 0.91f, 0.75f, 1f) : new Color(0.55f, 0.50f, 0.43f, 1f);
            _buttonStyle.Draw(rect, new GUIContent(text), hover, hover && Input.GetMouseButton(0), false, false);
            GUI.contentColor = oldColor;
            if (enabled)
            {
                RegisterClick(rect, action);
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
        private void RegisterClick(Rect localRect, Action action)
        {
            _clickTargets.Add(new ClickTarget
            {
                ScreenRect = new Rect(_windowRect.x + localRect.x, _windowRect.y + localRect.y, localRect.width, localRect.height),
                Action = action
            });
        }

        private void ProcessClicksFromInput()
        {
            var mouse = GuiMouse();
            for (var i = _clickTargets.Count - 1; i >= 0; i--)
            {
                var target = _clickTargets[i];
                if (target.ScreenRect.Contains(mouse))
                {
                    target.Action();
                    break;
                }
            }
        }

        private static Vector2 GuiMouse()
        {
            var mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        private static void ConsumePointerEvents(Event evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.type != EventType.MouseDown && evt.type != EventType.MouseUp && evt.type != EventType.MouseDrag && evt.type != EventType.ScrollWheel)
            {
                return;
            }

            var mouse = evt.mousePosition;
            mouse.y = Screen.height - mouse.y;
            if (new Rect(0f, 0f, Screen.width, Screen.height).Contains(mouse))
            {
                evt.Use();
            }
        }

        private sealed class ClickTarget
        {
            public Rect ScreenRect;
            public Action Action;
        }
    }
    [HarmonyPatch(typeof(PlayerStore), "OnItemBought", new Type[] { typeof(GameItem), typeof(int) })]
    internal static class PlayerPurchasePatch
    {
        private static void Postfix(GameItem gameItem, int cost)
        {
            try { PerkShopMod.AddPurchaseVolume(gameItem, cost); }
            catch (Exception ex) { PerkShopLog.Error("PlayerPurchasePatch 失败：" + ex); }
        }
    }

    [HarmonyPatch(typeof(StoreClient), "OnItemBought", new Type[] { typeof(GameItem), typeof(int) })]
    internal static class CustomerPurchasePatch
    {
        private static void Postfix(GameItem item, int cost)
        {
            try { PerkShopMod.AddSaleVolume(item, cost); }
            catch (Exception ex) { PerkShopLog.Error("CustomerPurchasePatch 失败：" + ex); }
        }
    }

    [HarmonyPatch(typeof(CommissaryUIManager), "OpenUI")]
    internal static class PerkShopCommissaryButtonPatch
    {
        private static void Postfix(CommissaryUIManager __instance)
        {
            try { PerkShopMod.Instance?.EnsureCommissaryButton(__instance); }
            catch (Exception ex) { PerkShopLog.Error("Commissary OpenUI 补丁失败：" + ex); }
        }
    }

    [HarmonyPatch(typeof(MapUIManager), "CommissaryTradeButton")]
    internal static class PerkShopMapCommissaryButtonPatch
    {
        private static void Postfix()
        {
            try { PerkShopMod.Instance?.EnsureCommissaryButton(CommissaryUIManager.Instance); }
            catch (Exception ex) { PerkShopLog.Error("CommissaryTradeButton 补丁失败：" + ex); }
        }
    }

    [HarmonyPatch(typeof(MapUIManager), "Bazar")]
    internal static class PerkShopMapBazarPatch
    {
        private static void Postfix()
        {
            try { PerkShopMod.Instance?.EnsureCommissaryButton(CommissaryUIManager.Instance); }
            catch (Exception ex) { PerkShopLog.Error("Bazar 补丁失败：" + ex); }
        }
    }
}