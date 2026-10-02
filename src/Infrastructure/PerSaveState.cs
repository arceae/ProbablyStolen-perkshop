using System;
using System.Collections.Generic;
using Il2Cpp;

namespace PerkShopFramework;

internal static class PerSaveState
{
    private const string Prefix = "psfw.";

    private static Il2CppSystem.Collections.Generic.Dictionary<string, string> Data(PlayerStore store)
    {
        if (store.modData != null) return store.modData;
        store.modData = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
        return store.modData;
    }

    internal static string? GetString(PlayerStore store, string key)
    {
        try
        {
            var data = Data(store);
            return data.TryGetValue(Prefix + key, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("读取存档状态失败：" + key + "，" + ex.Message);
            return null;
        }
    }

    internal static int GetInt(PlayerStore store, string key, int fallback = 0)
    {
        return int.TryParse(GetString(store, key), out var value) ? value : fallback;
    }

    internal static long GetLong(PlayerStore store, string key, long fallback = 0L)
    {
        return long.TryParse(GetString(store, key), out var value) ? value : fallback;
    }

    internal static void SetString(PlayerStore store, string key, string value)
    {
        try
        {
            Data(store)[Prefix + key] = value ?? string.Empty;
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("写入存档状态失败：" + key + "，" + ex.Message);
        }
    }

    internal static void SetInt(PlayerStore store, string key, int value) =>
        SetString(store, key, value.ToString());

    internal static void SetLong(PlayerStore store, string key, long value) =>
        SetString(store, key, value.ToString());

    internal static void Remove(PlayerStore store, string key)
    {
        try
        {
            Data(store).Remove(Prefix + key);
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("删除存档状态失败：" + key + "，" + ex.Message);
        }
    }

    internal static void RemoveByPrefix(PlayerStore store, string keyPrefix)
    {
        try
        {
            var data = Data(store);
            var keys = new List<string>();
            foreach (var entry in data)
            {
                if (entry.Key.StartsWith(Prefix + keyPrefix, StringComparison.Ordinal))
                {
                    keys.Add(entry.Key);
                }
            }

            for (var i = 0; i < keys.Count; i++)
            {
                data.Remove(keys[i]);
            }
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("按前缀删除存档状态失败：" + keyPrefix + "，" + ex.Message);
        }
    }
}