using System;
using MelonLoader;

namespace PerkShopFramework;

internal static class PerkShopLog
{
    private const string Prefix = "[PerkShopFramework]";
    private static MelonLogger.Instance? _logger;

    internal static void Initialize(MelonLogger.Instance logger) => _logger = logger;

    internal static void Msg(string message) => Write("Msg", message);
    internal static void Warning(string message) => Write("Warning", message);
    internal static void Error(string message) => Write("Error", message);
    internal static void Debug(string message) => Write("Debug", message);

    private static void Write(string level, string message)
    {
        try
        {
            var line = Prefix + " " + level + ": " + message;
            if (_logger == null)
            {
                MelonLogger.Msg(line);
                return;
            }

            if (level == "Warning") _logger.Warning(line);
            else if (level == "Error") _logger.Error(line);
            else _logger.Msg(line);
        }
        catch
        {
        }
    }
}
