using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace PerkShopFramework
{
    internal static class PurchasedNativeInitialItemReplay
    {
        private static readonly HashSet<string> BaseInitialItemIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "storage_bay",
            "processed_meat",
            "processed_juice",
            "toothpaste",
            "toilet_paper",
            "sign_food",
            "sign_household",
            "dossier",
            "mentor_contract",
            "trashcan"
        };

        private static bool _active;

        internal static void Begin()
        {
            _active = true;
        }

        internal static void End()
        {
            _active = false;
        }

        internal static bool ShouldBlock(GameItem? item)
        {
            if (!_active || item == null)
            {
                return false;
            }

            try
            {
                var identifier = (item.identifier ?? string.Empty).TrimEnd('\0');
                return BaseInitialItemIds.Contains(identifier);
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(GraphUtils), "EmporiumTryAdd", new Type[] { typeof(GraphNodeStorage), typeof(GameItem) })]
    internal static class PurchasedGraphEmporiumTryAddPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem item)
        {
            return !PurchasedNativeInitialItemReplay.ShouldBlock(item);
        }
    }

    [HarmonyPatch(typeof(GraphUtils), "EmporiumTryAdd", new Type[] { typeof(GameItem) })]
    internal static class PurchasedGraphEmporiumTryAddSinglePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem item)
        {
            return !PurchasedNativeInitialItemReplay.ShouldBlock(item);
        }
    }

    [HarmonyPatch(typeof(GraphUtils), "TryAcceptAll", new Type[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) })]
    internal static class PurchasedGraphTryAcceptAllPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem item)
        {
            return !PurchasedNativeInitialItemReplay.ShouldBlock(item);
        }
    }

    [HarmonyPatch(typeof(GraphUtils), "TryAcceptAllOrDestroy", new Type[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(string) })]
    internal static class PurchasedGraphTryAcceptAllOrDestroyPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem item)
        {
            return !PurchasedNativeInitialItemReplay.ShouldBlock(item);
        }
    }

    [HarmonyPatch(typeof(EmporiumEntry), "TryAddToPlayerInv")]
    internal static class PurchasedNativePlayerInventoryPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GameItem gameItem)
        {
            return !PurchasedNativeInitialItemReplay.ShouldBlock(gameItem);
        }
    }
}