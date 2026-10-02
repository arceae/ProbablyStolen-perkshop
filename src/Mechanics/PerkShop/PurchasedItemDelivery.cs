using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        public bool IsApplyingPurchasedContent { get; private set; }

        public bool TryGrantFutureTechStarterItems()
        {
            var ids = ResolveFutureTechStarterItemIds();
            if (ids == null || ids.Length == 0)
            {
                PerkShopLog.Warning("FutureTech starter item list was unavailable.");
                return false;
            }

            var applied = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    continue;
                }

                try
                {
                    if (HasOwnedItem(id))
                    {
                        continue;
                    }

                    var item = DirectoryMaster.Item(id, true);
                    if (item == null)
                    {
                        applied = false;
                        continue;
                    }

                    item.DisableTag("TAG_NOT_PURCHASED", true);
                    item.DisableTag("not_purchased", true);
                    if (!RoutePurchasedItemToPlayerOrCounter(item))
                    {
                        applied = false;
                    }
                }
                catch (Exception ex)
                {
                    applied = false;
                    PerkShopLog.Warning("FutureTech item grant failed for " + id + ": " + ex.Message);
                }
            }

            return applied;
        }

        private static string[] ResolveFutureTechStarterItemIds()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var editionType = assembly.GetType("DeepSpacePawnshop.FutureTech.FutureEdition", false);
                    if (editionType == null)
                    {
                        continue;
                    }

                    var field = editionType.GetField("StarterItems", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var ids = field != null ? field.GetValue(null) as string[] : null;
                    if (ids != null && ids.Length > 0)
                    {
                        return ids;
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("FutureTech starter item discovery failed: " + ex.Message);
            }

            return new[] { "dspf_future_purifier", "dspf_future_nuclear_battery" };
        }

        private static bool HasOwnedItem(string identifier)
        {
            try
            {
                var emporium = EmporiumEntry.Instance;
                var items = emporium != null ? emporium.GetAllOwnedItems(false, true) : null;
                if (items != null)
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (item != null && string.Equals(item.identifier, identifier, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }
        private const string FutureTechRepairKey = "Repair.FutureTechStarterItems";

        public void TryRepairLegacyFutureTechItems(PlayerStore store)
        {
            if (!CanUseInRun(store)
                || ReadInt(store, FutureTechRepairKey, 0) != 0
                || ReadInt(store, AppliedContentPrefix + "dspf_future_tech", 0) == 0)
            {
                return;
            }

            if (TryGrantFutureTechStarterItems())
            {
                WriteInt(store, FutureTechRepairKey, 1);
                _savePending = true;
                PerkShopLog.Msg("Repaired legacy Future Tech starter items.");
            }
        }

        private const string ScavengersReignRepairKey = "Repair.ScavengersReignToken";

        public void TryRepairLegacyExtraPerksItems(PlayerStore store)
        {
            if (!CanUseInRun(store) || ReadInt(store, ScavengersReignRepairKey, 0) != 0)
            {
                return;
            }

            var ownsScavengersReign = false;
            try
            {
                var perks = store.startingPerks;
                if (perks != null)
                {
                    for (var i = 0; i < perks.Count; i++)
                    {
                        var id = GetPerkId(perks[i]);
                        if (string.Equals(id.TrimEnd('\0'), "拾荒者之王", StringComparison.Ordinal))
                        {
                            ownsScavengersReign = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            if (!ownsScavengersReign)
            {
                return;
            }

            try
            {
                var emporium = EmporiumEntry.Instance;
                var items = emporium != null ? emporium.GetInvItems() : null;
                if (items != null)
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        var existing = items[i];
                        if (existing != null && string.Equals(existing.identifier, "scav_token", StringComparison.Ordinal))
                        {
                            WriteInt(store, ScavengersReignRepairKey, 1);
                            return;
                        }
                    }
                }

                var token = DirectoryMaster.Item("scav_token", true);
                if (token == null)
                {
                    return;
                }

                token.DisableTag("TAG_NOT_PURCHASED", true);
                token.DisableTag("not_purchased", true);
                if (RoutePurchasedItemToPlayerOrCounter(token))
                {
                    WriteInt(store, ScavengersReignRepairKey, 1);
                    PerkShopLog.Msg("Repaired missing ExtraPerks item: scav_token");
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("TryRepairLegacyExtraPerksItems failed: " + ex.Message);
            }
        }

private bool TryGrantKnownItems(params string[] identifiers)
        {
            var applied = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < identifiers.Length; i++)
            {
                var identifier = identifiers[i];
                if (string.IsNullOrEmpty(identifier) || !seen.Add(identifier))
                {
                    continue;
                }

                try
                {
                    if (HasOwnedItem(identifier))
                    {
                        continue;
                    }

                    var item = DirectoryMaster.Item(identifier, true);
                    if (item == null)
                    {
                        applied = false;
                        PerkShopLog.Warning("已知天赋物品不存在：" + identifier);
                        continue;
                    }

                    item.DisableTag("TAG_NOT_PURCHASED", true);
                    item.DisableTag("not_purchased", true);
                    if (!RoutePurchasedItemToPlayerOrCounter(item))
                    {
                        applied = false;
                        PerkShopLog.Warning("已知天赋物品发放失败：" + identifier);
                    }
                }
                catch (Exception ex)
                {
                    applied = false;
                    PerkShopLog.Warning("已知天赋物品发放异常：" + identifier + "，" + ex.Message);
                }
            }

            return applied;
        }
        public bool RoutePurchasedItemToPlayerOrCounter(GameItem item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                var emporium = EmporiumEntry.Instance;
                if (emporium == null || emporium.invElement == null)
                {
                    PerkShopLog.Warning("玩家背包入口尚未就绪，跳过主背包发放：" + item.identifier);
                }
                else
                {
                    emporium.TryAddToPlayerInv(item);
                    if (item.parentInventory != null)
                    {
                        PerkShopLog.Msg("Purchased item routed to player inventory: " + item.identifier);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("TryAddToPlayerInv failed for purchased item: " + ex.Message);
            }

            try
            {
                var store = GetStore();
                if (CanUseInRun(store))
                {
                    store.AddDirectSellingItemToTable(item, true, false, false, 0);
                    if (item.parentInventory != null)
                    {
                        PerkShopLog.Msg("Purchased item routed to counter: " + item.identifier);
                        return true;
                    }

                    PerkShopLog.Warning("柜台接收后未检测到物品归属：" + item.identifier);
                }
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("Counter fallback failed for purchased item: " + ex);
            }

            return false;
        }
    }

    public sealed partial class PerkShopMod
    {
        private static readonly string[] TapeheadCassetteIds = { "cassette1d", "cassette2d", "cassette3d", "cassette4d", "cassette5d", "cassette6d", "cassette7d" };
        private const string TapeheadRepairKey = "Repair.TapeheadCassettes";
        private const string WagesPerksRepairPrefix = "Repair.WagesPerks.";
        private const string DestinyDicePerkId = "\u547d\u8fd0\u4e4b\u9ab0";
        private const string FrogPowerPerkId = "\u86d9\u54e5\u725b\u903c";

        public bool TryGrantTapeheadCassettes()
        {
            try
            {
                var granted = 0;
                for (var i = 0; i < TapeheadCassetteIds.Length; i++)
                {
                    var identifier = TapeheadCassetteIds[i];
                    if (HasOwnedItem(identifier))
                    {
                        continue;
                    }

                    var cassette = DirectoryMaster.Item(identifier, true);
                    if (cassette == null)
                    {
                        PerkShopLog.Warning("Tapehead cassette item is unavailable: " + identifier);
                        return false;
                    }

                    cassette.DisableTag("TAG_NOT_PURCHASED", true);
                    cassette.DisableTag("not_purchased", true);
                    if (!RoutePurchasedItemToPlayerOrCounter(cassette))
                    {
                        PerkShopLog.Warning("Tapehead cassette could not be placed: " + identifier);
                        return false;
                    }

                    granted++;
                }

                PerkShopLog.Msg("Tapehead cassette set granted directly; new=" + granted + ".");
                return true;
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("Tapehead cassette grant failed: " + ex.Message);
                return false;
            }
        }

        public void TryRepairLegacyTapeheadCassettes(PlayerStore store)
        {
            if (!CanUseInRun(store)
                || ReadInt(store, AppliedContentPrefix + "tapehead", 0) == 0
                || ReadInt(store, TapeheadRepairKey, 0) != 0)
            {
                return;
            }

            try
            {
                if (!HasAllTapeheadCassettes() && !TryGrantTapeheadCassettes())
                {
                    PerkShopLog.Warning("Tapehead repair could not grant all cassette items.");
                    return;
                }

                if (!HasAllTapeheadCassettes())
                {
                    PerkShopLog.Warning("Tapehead repair is waiting for all cassette items to appear.");
                    return;
                }

                WriteInt(store, TapeheadRepairKey, 1);
                _savePending = true;
                PerkShopLog.Msg("Repaired legacy Tapehead cassettes.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("TryRepairLegacyTapeheadCassettes failed: " + ex.Message);
            }
        }

        private static bool HasAllTapeheadCassettes()
        {
            for (var i = 0; i < TapeheadCassetteIds.Length; i++)
            {
                if (!HasOwnedItem(TapeheadCassetteIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryGrantWagesPerksDestinyDice()
        {
            try
            {
                var perkType = FindLoadedType("DestinyDicePerk");
                var invoked = TryInvokeWagesPerksItemGrant(perkType, "_diceGiven", "GiveIfActive");
                if (!invoked)
                {
                    PerkShopLog.Warning("WagesPerks DestinyDicePerk grant method was unavailable.");
                    return false;
                }

                if (!HasOwnedItem("destiny_dice"))
                {
                    var factoryType = FindLoadedType("DestinyDice");
                    var item = InvokeGameItemFactory(factoryType, "CreateDestinyDice");
                    if (item == null)
                    {
                        PerkShopLog.Warning("WagesPerks DestinyDice factory returned null.");
                        return false;
                    }

                    item.DisableTag("TAG_NOT_PURCHASED", true);
                    item.DisableTag("not_purchased", true);
                    if (!RoutePurchasedItemToPlayerOrCounter(item))
                    {
                        return false;
                    }
                }

                PerkShopLog.Msg("WagesPerks item grant invoked: destiny_dice");
                return true;
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("WagesPerks DestinyDice grant failed: " + ex.Message);
                return false;
            }
        }

        public bool TryGrantWagesPerksFrogBox()
        {
            try
            {
                var perkType = FindLoadedType("FrogPowerPerk");
                var invoked = TryInvokeWagesPerksItemGrant(perkType, "_storageBoxGiven", "TryGiveStorageBox");
                if (!invoked)
                {
                    PerkShopLog.Warning("WagesPerks FrogPowerPerk grant method was unavailable.");
                    return false;
                }

                if (!HasOwnedItem("custom_storage_box"))
                {
                    var factoryType = FindLoadedType("CustomStorageContainer");
                    var item = InvokeGameItemFactory(factoryType, "CreateContainer");
                    if (item == null)
                    {
                        PerkShopLog.Warning("WagesPerks CustomStorageContainer factory returned null.");
                        return false;
                    }

                    TryFillFrogStorageBox(factoryType, item);
                    item.DisableTag("TAG_NOT_PURCHASED", true);
                    item.DisableTag("not_purchased", true);
                    if (!RoutePurchasedItemToPlayerOrCounter(item))
                    {
                        return false;
                    }
                }

                PerkShopLog.Msg("WagesPerks item grant invoked: custom_storage_box");
                return true;
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("WagesPerks FrogPowerPerk grant failed: " + ex.Message);
                return false;
            }
        }

        public void TryRepairLegacyWagesPerksItems(PlayerStore store)
        {
            if (!CanUseInRun(store))
            {
                return;
            }

            TryRepairLegacyWagesPerksItem(store, DestinyDicePerkId, "destiny_dice", TryGrantWagesPerksDestinyDice);
            TryRepairLegacyWagesPerksItem(store, FrogPowerPerkId, "custom_storage_box", TryGrantWagesPerksFrogBox);
        }

        private void TryRepairLegacyWagesPerksItem(PlayerStore store, string perkId, string itemId, Func<bool> grant)
        {
            if (ReadInt(store, AppliedContentPrefix + perkId, 0) == 0
                || ReadInt(store, WagesPerksRepairPrefix + perkId, 0) != 0
                || HasOwnedItem(itemId))
            {
                return;
            }

            if (!grant())
            {
                return;
            }

            WriteInt(store, WagesPerksRepairPrefix + perkId, 1);
            _savePending = true;
            PerkShopLog.Msg("Repaired legacy WagesPerks item: " + itemId);
        }

        private static bool TryInvokeWagesPerksItemGrant(Type perkType, string flagName, string methodName)
        {
            if (perkType == null)
            {
                return false;
            }

            var flag = perkType.GetField(flagName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (flag != null && flag.FieldType == typeof(bool))
            {
                flag.SetValue(null, false);
            }

            var method = perkType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            method.Invoke(null, null);
            return true;
        }

        private static GameItem InvokeGameItemFactory(Type type, string methodName)
        {
            if (type == null)
            {
                return null;
            }

            try
            {
                var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return method != null ? method.Invoke(null, null) as GameItem : null;
            }
            catch
            {
                return null;
            }
        }

        private static void TryFillFrogStorageBox(Type factoryType, GameItem item)
        {
            try
            {
                var method = factoryType == null
                    ? null
                    : factoryType.GetMethod("FillWithRandomLockedBoxes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(null, new object[] { item, 1 });
                }
            }
            catch
            {
            }
        }

private static Type? FindLoadedType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            // 只按精确类型名定向查找，不枚举程序集内全部类型。
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool _extraPerksCompatibilityInstalled;
        private static bool _vainPersonCompatibilityInstalled;

        public static void TryInstallOptionalCompatibilityPatches(HarmonyLib.Harmony harmony)
        {
            if (harmony == null)
            {
                return;
            }

            TryInstallExtraPerksCompatibility(harmony);
            TryInstallVainPersonCompatibility(harmony);
        }

        private static void TryInstallExtraPerksCompatibility(HarmonyLib.Harmony harmony)
        {
            if (_extraPerksCompatibilityInstalled)
            {
                return;
            }

            try
            {
                var grantType = FindLoadedType("ExtraPerks.InventoryGrant");
                var target = grantType == null ? null : AccessTools.Method(grantType, "TryGrant", new[] { typeof(string) });
                if (target == null)
                {
                    return;
                }

                var prefix = AccessTools.Method(typeof(ExtraPerksInventoryGrantRedirectPatch), "Prefix", new[] { typeof(string) });
                if (prefix == null)
                {
                    return;
                }

                harmony.Patch(target, new HarmonyMethod(prefix));
                _extraPerksCompatibilityInstalled = true;
                PerkShopLog.Msg("ExtraPerks inventory grant redirect installed.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("ExtraPerks compatibility patch failed: " + ex.Message);
            }
        }

        private static void TryInstallVainPersonCompatibility(HarmonyLib.Harmony harmony)
        {
            if (_vainPersonCompatibilityInstalled)
            {
                return;
            }

            try
            {
                var diagType = FindLoadedType("VainPerson.PerkClickDiagPatch");
                var clickPrefix = diagType == null
                    ? null
                    : AccessTools.Method(diagType, "ClickPrefix", new[] { typeof(StartingPerkElement) });
                var suppressor = AccessTools.Method(typeof(VainPersonClickDiagSuppressor), "Prefix", Type.EmptyTypes);
                if (clickPrefix == null || suppressor == null)
                {
                    return;
                }

                harmony.Patch(clickPrefix, new HarmonyMethod(suppressor));
                _vainPersonCompatibilityInstalled = true;
                PerkShopLog.Msg("VainPerson picker click diagnostics suppressed.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("VainPerson compatibility patch failed: " + ex.Message);
            }
        }
    }

    internal static class VainPersonClickDiagSuppressor
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }
    internal static class ExtraPerksInventoryGrantRedirectPatch
    {
        private static bool Prefix(string identifier)
        {
            var mod = PerkShopMod.Instance;
            if (mod == null || !mod.IsApplyingPurchasedContent || string.IsNullOrEmpty(identifier))
            {
                return true;
            }

            try
            {
                // 框架规则 10：发放物品前先确认容器已就绪，否则交还原逻辑（不在单例未初始化时创建物品）。
                var emporium = EmporiumEntry.Instance;
                if (emporium == null || emporium.invElement == null)
                {
                    return true;
                }

                var item = DirectoryMaster.Item(identifier, true);
                if (item == null)
                {
                    return true;
                }

                item.DisableTag("TAG_NOT_PURCHASED", true);
                item.DisableTag("not_purchased", true);
                return !mod.RoutePurchasedItemToPlayerOrCounter(item);
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning("特性 mod 发放入口重定向失败：" + identifier + "：" + ex.Message);
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(EmporiumEntry), "TryAddToAfterhourInv")]
    internal static class PurchasedItemDeliveryAfterhourPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem gameItem)
        {
            try
            {
                var mod = PerkShopMod.Instance;
                if (mod == null || !mod.IsApplyingPurchasedContent)
                {
                    return true;
                }

                return !mod.RoutePurchasedItemToPlayerOrCounter(gameItem);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("TryAddToAfterhourInv 补丁失败：" + ex);
                return true;
            }
        }
    }
}
