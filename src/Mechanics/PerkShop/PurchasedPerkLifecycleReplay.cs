using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace PerkShopFramework
{
    /// <summary>
    /// Replays provider-owned Harmony patches attached to the original new-game
    /// lifecycle. Effects still run against the isolated current perk only.
    /// </summary>
    internal static class PurchasedPerkLifecycleReplay
    {
        private static readonly HashSet<string> TargetTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Il2Cpp.PlayerStore",
            "Il2Cpp.GameMaster"
        };

        private static readonly HashSet<string> TargetMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "StartNewGame",
            "NewGame",
            "InitialSave",
            "HandleSkipIntro"
        };

        internal static void Apply(PlayerStore store, string assemblyName)
        {
            if (store == null || string.IsNullOrEmpty(assemblyName))
            {
                return;
            }

            foreach (var method in HarmonyLib.Harmony.GetAllPatchedMethods())
            {
                if (!IsTarget(method))
                {
                    continue;
                }

                var info = HarmonyLib.Harmony.GetPatchInfo(method);
                if (info == null)
                {
                    continue;
                }

                InvokePatches(info.Prefixes, method, store, assemblyName);
                InvokePatches(info.Postfixes, method, store, assemblyName);
            }
        }

        internal static void ApplyHandleInitialItemPostfixes(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return;
            }

            var target = AccessTools.Method(typeof(NewGameData), "HandleInitialItem", Type.EmptyTypes);
            if (target == null)
            {
                return;
            }

            var info = HarmonyLib.Harmony.GetPatchInfo(target);
            if (info == null)
            {
                return;
            }

            InvokePatches(info.Prefixes, target, null, assemblyName);
            InvokePatches(info.Postfixes, target, null, assemblyName);
        }
        private static bool IsTarget(MethodBase method)
        {
            var typeName = method.DeclaringType?.FullName ?? string.Empty;
            return TargetTypes.Contains(typeName) && TargetMethods.Contains(method.Name);
        }

        private static void InvokePatches(
            IEnumerable<Patch>? patches,
            MethodBase targetMethod,
            PlayerStore store,
            string assemblyName)
        {
            if (patches == null)
            {
                return;
            }

            foreach (var patch in patches)
            {
                var patchMethod = patch.PatchMethod;
                if (patchMethod == null)
                {
                    continue;
                }

                var ownerAssembly = patchMethod.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
                if (!string.Equals(ownerAssembly, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                // WagesPerks runs the global reset through NotifyNewGame separately.
                // Replaying this postfix would reset all previously purchased WagesPerks state.
                if (string.Equals(assemblyName, "JacksonPerks", StringComparison.Ordinal)
                    && string.Equals(targetMethod.Name, "NewGame", StringComparison.Ordinal)
                    && string.Equals(patchMethod.DeclaringType?.FullName, "JacksonPerks.Patches", StringComparison.Ordinal))
                {
                    continue;
                }

                InvokePatch(patchMethod, targetMethod, store);
            }
        }

        private static void InvokePatch(MethodInfo patchMethod, MethodBase targetMethod, PlayerStore store)
        {
            try
            {
                object? target = null;
                if (!patchMethod.IsStatic)
                {
                    target = Activator.CreateInstance(patchMethod.DeclaringType!, true);
                }

                patchMethod.Invoke(target, BuildArguments(patchMethod, targetMethod, store));
            }
            catch (Exception ex)
            {
                PerkShopLog.Warning(
                    "生命周期补丁重放失败：" + (patchMethod.DeclaringType?.FullName ?? patchMethod.Name)
                    + "." + patchMethod.Name + "，" + ex.Message);
            }
        }

        private static object?[] BuildArguments(MethodInfo patchMethod, MethodBase targetMethod, PlayerStore store)
        {
            var parameters = patchMethod.GetParameters();
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterType = parameter.ParameterType;
                var valueType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
                var name = parameter.Name ?? string.Empty;

                if (string.Equals(name, "__instance", StringComparison.Ordinal))
                {
                    args[i] = ResolveInstance(valueType, store);
                }
                else if (string.Equals(name, "__originalMethod", StringComparison.Ordinal))
                {
                    args[i] = targetMethod;
                }
                else if (valueType == typeof(PlayerStore))
                {
                    args[i] = store;
                }
                else if (valueType == typeof(GameMaster))
                {
                    args[i] = null;
                }
                else if (valueType == typeof(NewGameData))
                {
                    args[i] = NewGameData.Instance;
                }
                else
                {
                    args[i] = CreateDefault(valueType);
                }
            }

            return args;
        }

        private static object? ResolveInstance(Type type, PlayerStore store)
        {
            if (type == typeof(PlayerStore))
            {
                return store;
            }

            if (type == typeof(GameMaster))
            {
                return null;
            }

            if (type == typeof(NewGameData))
            {
                return NewGameData.Instance;
            }

            return CreateDefault(type);
        }

        private static object? CreateDefault(Type type)
        {
            if (type == typeof(void))
            {
                return null;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}