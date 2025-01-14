﻿global using ExtremeRoles.Module.CustomOption;
global using InfoOverlay = ExtremeRoles.Module.InfoOverlay.Controller;
global using ExRError = ExtremeRoles.Module.ErrorCode<ExtremeRoles.ErrorCode>;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

using HarmonyLib;

using System.Net.Http;

using ExtremeRoles.Compat;
using ExtremeRoles.Module;
using ExtremeRoles.Module.ExtremeShipStatus;
using ExtremeRoles.Module.ApiHandler;
using ExtremeRoles.Resources;
using ExtremeRoles.Module.SystemType;

namespace ExtremeRoles;

[BepInAutoPlugin("me.yukieiji.extremeroles", "Extreme Roles")]
[BepInDependency(
    "gg.reactor.api",
    BepInDependency.DependencyFlags.SoftDependency)] // Reactorとのパッチの兼ね合いで入れておく
[BepInDependency(
    Compat.ModIntegrator.SubmergedIntegrator.Guid,
    BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("Among Us.exe")]
public partial class ExtremeRolesPlugin : BasePlugin
{
    public Harmony Harmony { get; } = new Harmony(Id);

    public static ExtremeRolesPlugin Instance;
    public static ExtremeShipStatus ShipState = new ExtremeShipStatus();

    internal static BepInEx.Logging.ManualLogSource Logger;

    public static ConfigEntry<bool> DebugMode { get; private set; }
    public static ConfigEntry<bool> IgnoreOverrideConsoleDisable { get; private set; }

    public override void Load()
    {
        Helper.Translation.Load();

        Logger = Log;

        DebugMode = Config.Bind("DeBug", "DebugMode", false);
        IgnoreOverrideConsoleDisable = Config.Bind(
            "DeBug", "IgnoreOverrideConsoleDisable", false,
            "If enabled, will ignore force disabling BepInEx.Console");

        Instance = this;

		Harmony.PatchAll();

		CompatModManager.Initialize();

		OptionCreator.Create();

        AddComponent<ExtremeRolePluginBehavior>();
		AddComponent<UnityMainThreadDispatcher>();

        if (BepInExUpdater.IsUpdateRquire())
        {
            AddComponent<BepInExUpdater>();
        }

		ApiServer.Register("/au/chat/"  , HttpMethod.Get , new GetChat());
		ApiServer.Register(PostChat.Path , HttpMethod.Post, new PostChat());
		ApiServer.Register(ChatWebUI.Path, HttpMethod.Get , new OpenChatWebUi());

		Il2CppRegisterAttribute.Registration(
            System.Reflection.Assembly.GetAssembly(this.GetType()));

        Loader.LoadCommonAsset();

		ExtremeSystemTypeManager.ModInitialize();
    }
}
