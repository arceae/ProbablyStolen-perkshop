using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        internal static bool CanApplyPurchasedContent()
        {
            try
            {
                return Instance != null && !Instance.IsApplyingPurchasedContent;
            }
            catch
            {
                return false;
            }
        }

        internal static PlayerStore? GetCurrentStoreForDispatcher()
        {
            try
            {
                return Instance?.GetStore();
            }
            catch
            {
                return null;
            }
        }

        internal void SetApplyingPurchasedContent(bool value)
        {
            IsApplyingPurchasedContent = value;
        }

        internal void MarkSavePending()
        {
            _savePending = true;
        }

        internal void ScheduleSawyerAfterPurchase(PlayerStore store)
        {
            ScheduleSawyerCrewNextDay(store);
        }
    }

    internal static class PurchasedPerkContentDispatcher
    {
        private const string PendingPrefix = "PendingContent.";
        private const string AppliedPrefix = "AppliedContent.";

        private static readonly object Gate = new object();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly HashSet<string> PendingSet = new HashSet<string>(StringComparer.Ordinal);
        private static int _queuedFrame = -1;

        internal static void Queue(PlayerStore store, StartingPerk perk)
        {
            try
            {
                var id = NormalizeId(perk?.id);
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }

                lock (Gate)
                {
                    if (PendingSet.Add(id))
                    {
                        Pending.Enqueue(id);
                    }

                    _queuedFrame = Time.frameCount;
                    PerSaveState.SetInt(store, PendingPrefix + id, 1);
                    PerkShopLog.Msg("已排队购买天赋内容：" + id);
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("排队购买天赋内容失败：" + ex);
            }
        }

        internal static void ResetStateForLoad()
        {
            lock (Gate)
            {
                Pending.Clear();
                PendingSet.Clear();
                _queuedFrame = -1;
            }

            PerkShopMod.Instance?.SetApplyingPurchasedContent(false);
        }

        internal static void Tick()
        {
            if (Pending.Count == 0 || _queuedFrame < 0 || Time.frameCount - _queuedFrame < 2)
            {
                return;
            }

            if (!PerkShopMod.CanApplyPurchasedContent())
            {
                return;
            }

            var store = PerkShopMod.GetCurrentStoreForDispatcher();
            if (store == null || store.startingPerks == null)
            {
                return;
            }

            string id;
            lock (Gate)
            {
                if (Pending.Count == 0)
                {
                    return;
                }

                id = Pending.Peek();
            }

            var perk = FindPerk(store, id);
            if (perk == null)
            {
                lock (Gate)
                {
                    Pending.Dequeue();
                    PendingSet.Remove(id);
                    PerSaveState.SetInt(store, PendingPrefix + id, 0);
                }

                PerkShopLog.Warning("待处理天赋不在 startingPerks 中，已移除队列：" + id);
                return;
            }

            if (!ApplyOne(store, perk, id))
            {
                return;
            }

            lock (Gate)
            {
                Pending.Dequeue();
                PendingSet.Remove(id);
                PerSaveState.SetInt(store, PendingPrefix + id, 0);
                PerSaveState.SetInt(store, AppliedPrefix + id, 1);
                _queuedFrame = Time.frameCount;
            }

            PerkShopMod.Instance?.MarkSavePending();
            PerkShopLog.Msg("已应用购买天赋内容：" + id);
        }

        private static bool ApplyOne(PlayerStore store, StartingPerk perk, string id)
        {
            var originalStorePerks = store.startingPerks;
            if (originalStorePerks == null)
            {
                return false;
            }

            var newGameData = NewGameData.Instance;
            var originalNewGamePerks = newGameData?.startingPerks;
            var isolated = new Il2CppSystem.Collections.Generic.List<StartingPerk>();
            isolated.Add(perk);

            store.startingPerks = isolated;
            if (newGameData != null)
            {
                newGameData.startingPerks = isolated;
            }

            PerkShopMod.Instance?.SetApplyingPurchasedContent(true);
            try
            {
                var provider = PurchasedPerkProviderRegistry.Resolve(id);
                provider.ApplyBeforeInitial(store, perk, id);

                // 不重放第三方 StartNewGame/NewGame 生命周期补丁：
                // 这些补丁可能重置顾客队列等全局运行时状态。
                // 已知 Provider 走 HandleNewGamePerk、显式 NotifyNewGame 或 Direct 方法。
                // Run the original initial-item handler so native starter items and
                // every provider postfix attached to it execute in the normal order.
                RunInitialItemPostfixReplay(provider.AssemblyName);

                // This is the original unified entry point for all perks that patch
                // it (native, ExtraPerks, PPerkPack, XIAOWOTradePerks and any future mod).
                StartingPerkContent.HandleNewGamePerk();

                provider.ApplyAfterInitial(store, perk, id);

                if (string.Equals(id, "sawyer_crew", StringComparison.Ordinal))
                {
                    PerkShopMod.Instance?.ScheduleSawyerAfterPurchase(store);
                }

                return true;
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("应用购买天赋内容失败：" + id + "，" + ex);
                return false;
            }
            finally
            {
                PerkShopMod.Instance?.SetApplyingPurchasedContent(false);
                store.startingPerks = originalStorePerks;
                if (newGameData != null)
                {
                    newGameData.startingPerks = originalNewGamePerks;
                }
            }
        }

        private static void RunInitialItemPostfixReplay(string assemblyName)
        {
            // Do NOT call NewGameData.HandleInitialItem() itself. Its native body
            // re-runs the selected start-type loadout (water merchant, etc.) on
            // every purchase. Replaying only provider-owned Harmony patches keeps
            // Wine/FutureTech/WagesPerks initialization without that native reset.
            PurchasedPerkLifecycleReplay.ApplyHandleInitialItemPostfixes(assemblyName);
        }
        private static StartingPerk? FindPerk(PlayerStore store, string id)
        {
            try
            {
                var perks = store.startingPerks;
                if (perks == null)
                {
                    return null;
                }

                for (var i = 0; i < perks.Count; i++)
                {
                    var perk = perks[i];
                    if (perk != null && string.Equals(NormalizeId(perk.id), id, StringComparison.Ordinal))
                    {
                        return perk;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string NormalizeId(string? id) => (id ?? string.Empty).TrimEnd('\0');
    }

    internal sealed class PurchasedPerkProviderBinding
    {
        internal string AssemblyName { get; }
        internal Type? ProviderType { get; }
        internal MethodInfo? Method { get; }
        internal ProviderBehavior Behavior { get; }

        internal PurchasedPerkProviderBinding(string assemblyName, Type? providerType, MethodInfo? method, ProviderBehavior behavior)
        {
            AssemblyName = assemblyName;
            ProviderType = providerType;
            Method = method;
            Behavior = behavior;
        }

        internal void ApplyBeforeInitial(PlayerStore store, StartingPerk perk, string id)
        {
            switch (Behavior)
            {
                case ProviderBehavior.ExternalNotifier:
                    if (!PurchasedPerkProviderRegistry.TryInvokeExternalNotifier(AssemblyName))
                    {
                        PerkShopLog.Warning("外部特性 Notifier 未找到，已继续通过生命周期补丁重放。" + AssemblyName);
                    }
                    break;
                case ProviderBehavior.Direct:
                    InvokeDirect();
                    break;
            }
        }

        internal void ApplyAfterInitial(PlayerStore store, StartingPerk perk, string id)
        {
            switch (Behavior)
            {
                case ProviderBehavior.FutureTech:
                    if (PerkShopMod.Instance == null || !PerkShopMod.Instance.TryGrantFutureTechStarterItems())
                    {
                        throw new InvalidOperationException("FutureTech provider failed.");
                    }
                    break;
            }

            if (string.Equals(id, "xiaowo_trade_precision_bay_expansion", StringComparison.Ordinal))
            {
                TryEnableXiaowoPrecisionBay();
            }
        }

        private static void TryEnableXiaowoPrecisionBay()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("XIAOWOTradePerks.PrecisionBayMode", false);
                    if (type == null)
                    {
                        continue;
                    }

                    var method = type.GetMethod(
                        "SetForCurrentSave",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(bool), typeof(bool) },
                        null);
                    if (method == null)
                    {
                        return;
                    }

                    var result = method.Invoke(null, new object[] { true, false });
                    if (result is bool enabled && !enabled)
                    {
                        PerkShopLog.Warning("XIAOWO 精工扩舱状态启用失败。");
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("XIAOWO 精工扩舱入口调用失败：" + ex.Message);
            }
        }
        private void InvokeDirect()
        {
            if (Method == null)
            {
                StartingPerkContent.HandleNewGamePerk();
                return;
            }

            object? target = null;
            if (!Method.IsStatic)
            {
                target = Activator.CreateInstance(ProviderType!, true);
            }

            Method.Invoke(target, null);
        }
    }

    internal enum ProviderBehavior
    {
        NativeUnified,

        /// <summary>
        /// 外部 mod 自带「统一 NotifyNewGame()」入口的形态。
        /// 旧 JacksonPerks / 新 WagePerks 都属于此类，但不以程序集名判定。
        /// </summary>
        ExternalNotifier,

        FutureTech,
        Direct,
        ActiveOnly
    }

    internal static class PurchasedPerkProviderRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, PurchasedPerkProviderBinding> Bindings =
            new Dictionary<string, PurchasedPerkProviderBinding>(StringComparer.Ordinal);

        // ★契约式发现的「外部 Notifier」表：程序集名 -> static NotifyNewGame()。
        // 不硬编码任何 mod 名，因此改名前后（JacksonPerks → WagePerks → 将来再改）都能命中。
        private static readonly Dictionary<string, MethodInfo> ExternalNotifiers =
            new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        private static string _primaryExternalNotifierAssembly = string.Empty;
        private static bool _scanned;
        private static int _scannedAssemblyCount = -1;

        internal static PurchasedPerkProviderBinding Resolve(string id)
        {
            EnsureScanned();
            lock (Gate)
            {
                if (Bindings.TryGetValue(id, out var binding))
                {
                    return binding;
                }
            }

            return new PurchasedPerkProviderBinding(InferAssemblyName(id), null, null, ProviderBehavior.NativeUnified);
        }

        internal static bool TryInvokeExternalNotifier(string assemblyName)
        {
            EnsureScanned();
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            MethodInfo? notifier;
            lock (Gate)
            {
                ExternalNotifiers.TryGetValue(assemblyName, out notifier);
            }

            if (notifier == null)
            {
                return false;
            }

            notifier.Invoke(null, null);
            return true;
        }

        /// <summary>该程序集是否被判定为「带统一 Notifier 的外部特性 mod」（供生命周期重放等外部逻辑复用）。</summary>
        internal static bool IsExternalNotifierAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            EnsureScanned();
            lock (Gate)
            {
                return ExternalNotifiers.ContainsKey(assemblyName);
            }
        }

        private static void EnsureScanned()
        {
            // 程序集数量增长时允许增量重扫：避免"扫描发生得比某个特性 mod 的加载更早"
            // 导致该 mod 被永久漏掉（_scanned 一旦置位就不再回头是旧实现的隐患）。
            if (_scanned && AppDomain.CurrentDomain.GetAssemblies().Length <= _scannedAssemblyCount)
            {
                return;
            }

            lock (Gate)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                if (_scanned && assemblies.Length <= _scannedAssemblyCount)
                {
                    return;
                }

                _scanned = true;
                _scannedAssemblyCount = assemblies.Length;
                // 重扫时清掉能力探测缓存：Harmony 补丁可能在本次扫描之后才挂上，缓存必须跟着失效。
                NativeContentPathCache.Clear();
                try
                {
                    for (var i = 0; i < assemblies.Length; i++)
                    {
                        var assembly = assemblies[i];
                        var assemblyName = SafeAssemblyName(assembly);
                        if (string.IsNullOrEmpty(assemblyName) || ShouldSkipAssembly(assemblyName))
                        {
                            continue;
                        }

                        var types = GetScannableTypes(assembly);
                        if (types.Length == 0)
                        {
                            continue;
                        }

                        // ★契约式发现：不再按程序集名硬编码（旧 JacksonPerks / 新 WagePerks / 将来任何改名都覆盖）。
                        // 约定 = 「任意命名空间下的 CustomStartingPerks 类型，带 static 零参 NotifyNewGame()」。
                        BindExternalNotifier(assemblyName, types);

                        for (var j = 0; j < types.Length; j++)
                        {
                            var type = types[j];
                            if (type == null)
                            {
                                continue;
                            }

                            var ids = ExtractPerkIds(type);
                            AddInstancePerkIds(type, ids);
                            if (ids.Count == 0)
                            {
                                continue;
                            }

                            var behavior = ResolveBehavior(assemblyName, type);
                            var method = FindProviderMethod(type, behavior);
                            var binding = new PurchasedPerkProviderBinding(assemblyName, type, method, behavior);

                            lock (Gate)
                            {
                                for (var k = 0; k < ids.Count; k++)
                                {
                                    Bindings[NormalizeId(ids[k])] = binding;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PerkShopLog.Warning("Provider 注册失败：" + ex.Message);
                }
            }
        }

        private static ProviderBehavior ResolveBehavior(string assemblyName, Type type)
        {
            if (assemblyName == "FutureTech")
            {
                return ProviderBehavior.FutureTech;
            }

            // ★能力探测（不认 mod 名）——顺序即语义，别调换：
            // ① 该 mod 自己挂在原生「应用特性内容」调用链上（StartingPerkContent.*，典型是 HandleNewGamePerk）。
            //    我们 ApplyOne 里本来就会调原生 StartingPerkContent.HandleNewGamePerk()，
            //    所以它已经会被驱动 → 返回 NativeUnified（**绝不额外再补一次**，
            //    否则 OnNewGame 触发两遍：丑陋店铺的 -150 吸引力、卡车上掉的货刷箱都会翻倍）。
            //    典型：ExtraPerks、XIAOWOTradePerks。
            if (SelfDrivesOnNativeContentPath(assemblyName))
            {
                return ProviderBehavior.NativeUnified;
            }

            // ② 暴露统一入口 <任意命名空间>.CustomStartingPerks.NotifyNewGame()，但不挂原生链
            //    → 需要我们显式驱动。典型：旧 JacksonPerks / 新 WagePerks。
            if (IsExternalNotifierAssembly(assemblyName))
            {
                return ProviderBehavior.ExternalNotifier;
            }

            if (FindZeroArgMethod(type, "OnNewGame") != null || FindZeroArgMethod(type, "TryGrant") != null)
            {
                return ProviderBehavior.Direct;
            }

            return ProviderBehavior.ActiveOnly;
        }

        private static readonly Dictionary<string, bool> NativeContentPathCache =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// 探测该程序集是否自己挂在原生 StartingPerkContent.* 上（能力探测，与 mod 名无关）。
        /// 命中 = 原生调用链已经会驱动它 → NativeUnified（不额外补触发，避免二次发放）。
        /// </summary>
        private static bool SelfDrivesOnNativeContentPath(string assemblyName)
        {
            lock (Gate)
            {
                if (NativeContentPathCache.TryGetValue(assemblyName, out var cached))
                {
                    return cached;
                }
            }

            var result = false;
            try
            {
                foreach (var method in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    var declaringType = method.DeclaringType;
                    if (declaringType == null)
                    {
                        continue;
                    }

                    if (!string.Equals(declaringType.Name, "StartingPerkContent", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var info = HarmonyLib.Harmony.GetPatchInfo(method);
                    if (info == null)
                    {
                        continue;
                    }

                    result = OwnedBy(info.Prefixes, assemblyName)
                        || OwnedBy(info.Postfixes, assemblyName)
                        || OwnedBy(info.Finalizers, assemblyName);
                    if (result)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("原生特性内容挂点探测失败：" + ex.Message);
            }

            lock (Gate)
            {
                NativeContentPathCache[assemblyName] = result;
            }

            return result;
        }

        private static bool OwnedBy(IEnumerable<Patch>? patches, string assemblyName)
        {
            if (patches == null)
            {
                return false;
            }

            foreach (var patch in patches)
            {
                var owner = patch.PatchMethod?.DeclaringType?.Assembly.GetName().Name;
                if (string.Equals(owner, assemblyName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo? FindProviderMethod(Type type, ProviderBehavior behavior)
        {
            if (behavior == ProviderBehavior.Direct)
            {
                return FindZeroArgMethod(type, "OnNewGame")
                    ?? FindZeroArgMethod(type, "TryGrant")
                    ?? FindZeroArgMethod(type, "GiveStartingGoods")
                    ?? FindZeroArgMethod(type, "GiveToBackpack");
            }

            return null;
        }

        private static MethodInfo? FindZeroArgMethod(Type type, string name)
        {
            try
            {
                return type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == 0);
            }
            catch
            {
                return null;
            }
        }

        private static void AddInstancePerkIds(Type type, List<string> ids)
        {
            if (type.IsAbstract || type.Name.IndexOf("Perk", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(type, true);
            }
            catch
            {
                return;
            }

            if (instance == null)
            {
                return;
            }

            try
            {
                string? value = null;
                for (var current = type; current != null; current = current.BaseType)
                {
                    var properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (var i = 0; i < properties.Length; i++)
                    {
                        if (string.Equals(properties[i].Name, "Id", StringComparison.Ordinal))
                        {
                            value = properties[i].GetValue(instance) as string;
                            break;
                        }
                    }

                    if (value != null)
                    {
                        break;
                    }

                    var fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (var i = 0; i < fields.Length; i++)
                    {
                        if (string.Equals(fields[i].Name, "Id", StringComparison.Ordinal))
                        {
                            value = fields[i].GetValue(instance) as string;
                            break;
                        }
                    }

                    if (value != null)
                    {
                        break;
                    }
                }

                value = (value ?? string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(value) && !ids.Contains(value))
                {
                    ids.Add(value);
                }
            }
            catch
            {
            }
        }
        private static List<string> ExtractPerkIds(Type type)
        {
            var result = new List<string>();
            try
            {
                var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (!field.IsLiteral || field.FieldType != typeof(string))
                    {
                        continue;
                    }

                    if (!(field.Name == "Id" || field.Name == "PERK_ID"
                        || field.Name.IndexOf("PerkId", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    var value = field.GetRawConstantValue() as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value);
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static string InferAssemblyName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "Assembly-CSharp";
            }

            if (id.StartsWith("xiaowo_trade_", StringComparison.Ordinal))
            {
                return "XIAOWOTradePerks";
            }

            if (id.StartsWith("dspf_", StringComparison.Ordinal))
            {
                return "FutureTech";
            }

            if (id.StartsWith("moddesign_", StringComparison.Ordinal))
            {
                return "WineAppraisalMaster";
            }

            if (id.StartsWith("ratlin_", StringComparison.Ordinal))
            {
                return "Ratlin";
            }

            if (id.StartsWith("vain_", StringComparison.Ordinal))
            {
                return "VainPerson";
            }

            // 无前缀可依的 id —— Wage's Perks 系列用的就是中文特性 id（旧 JacksonPerks / 新 WagePerks）。
            // 既然已发现外部 Notifier，就归给它；退化成 Assembly-CSharp 等于整条链路静默失效。
            if (!string.IsNullOrEmpty(_primaryExternalNotifierAssembly) && HasNonAscii(id))
            {
                return _primaryExternalNotifierAssembly;
            }

            return "Assembly-CSharp";
        }

        private static bool HasNonAscii(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] > 0x7F)
                {
                    return true;
                }
            }

            return false;
        }

        private static Type[] GetScannableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null).ToArray()!;
            }
            catch
            {
                return Type.EmptyTypes;
            }
        }

        /// <summary>
        /// 绑定外部特性 Notifier。★弱契约：只认「类型简名 CustomStartingPerks + static 零参 NotifyNewGame()」，
        /// 不认命名空间、不认程序集名 —— 从而与具体 mod 名字解耦，
        /// JacksonPerks、WagePerks 以及将来任何第三次改名都能自动命中。
        /// </summary>
        private static void BindExternalNotifier(string assemblyName, Type[] types)
        {
            lock (Gate)
            {
                if (ExternalNotifiers.ContainsKey(assemblyName))
                {
                    return;
                }
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || !string.Equals(type.Name, "CustomStartingPerks", StringComparison.Ordinal))
                {
                    continue;
                }

                var notifier = FindZeroArgMethod(type, "NotifyNewGame");
                if (notifier == null || !notifier.IsStatic)
                {
                    continue;
                }

                lock (Gate)
                {
                    ExternalNotifiers[assemblyName] = notifier;
                    if (string.IsNullOrEmpty(_primaryExternalNotifierAssembly))
                    {
                        _primaryExternalNotifierAssembly = assemblyName;
                    }
                }

                PerkShopLog.Msg("已绑定外部特性 Notifier：" + assemblyName + " → "
                    + (notifier.DeclaringType?.FullName ?? type.Name) + ".NotifyNewGame");
                return;
            }
        }

        private static string SafeAssemblyName(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ShouldSkipAssembly(string name)
        {
            return name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase)
                || name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
                || name.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
                || name.Equals("0Harmony", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MelonLoader", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string? id) => (id ?? string.Empty).TrimEnd('\0');
    }
}
