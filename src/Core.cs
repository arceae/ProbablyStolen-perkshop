using System;
using HarmonyLib;
using MelonLoader;

[assembly: System.Reflection.AssemblyVersion("0.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.2.0.0")]
[assembly: MelonInfo(typeof(PerkShopFramework.Core), "PerkShop Framework", "0.2.0", "Codex", "https://github.com/arcaeae/PerkShopFramework")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace PerkShopFramework;

public sealed class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        PerkShopLog.Initialize(LoggerInstance);
        try
        {
            PerkShopLog.Msg("开始初始化。");
            PerkShopMod.Initialize(HarmonyInstance);
            PerkShopLog.Msg("初始化完成。");
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("初始化失败：" + ex);
        }
    }

    public override void OnUpdate()
    {
        try
        {
            PerkShopMod.Update();
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("OnUpdate 失败：" + ex);
        }
    }

    public override void OnDeinitializeMelon()
    {
        try
        {
            PerkShopMod.Shutdown();
            PerkShopLog.Msg("已卸载。");
        }
        catch (Exception ex)
        {
            PerkShopLog.Error("卸载失败：" + ex);
        }
    }
}
