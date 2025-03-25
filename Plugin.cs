using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib.MonoBehaviors;
using MenuLib;
using UnityEngine;
using Photon.Pun;
using Steamworks;

namespace UpgradeEveryRound;

[BepInPlugin(modGUID, modName, modVersion), BepInDependency("nickklmao.menulib", "2.1.1")]
public class Plugin : BaseUnityPlugin
{
    public const string modGUID = "dev.redfops.repo.upgradeeveryround";
    public const string modName = "upgradeeveryround";
    public const string modVersion = "1.0.0";

    public static bool isOpen = false;

    internal static new ManualLogSource Logger;
    private readonly Harmony harmony = new Harmony(modGUID);

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        harmony.PatchAll(typeof(PlayerSpawnPatch));
        harmony.PatchAll(typeof(MenuClosePatch));
        harmony.PatchAll(typeof(MenuManagerClosePatch));
        harmony.PatchAll(typeof(MenuManagerOpenPatch));
        harmony.PatchAll(typeof(UpgradeMapPlayerCountPatch));
        harmony.PatchAll(typeof(UpgradePlayerEnergyPatch));
        harmony.PatchAll(typeof(UpgradePlayerExtraJumpPatch));
        harmony.PatchAll(typeof(UpgradePlayerGrabRangePatch));
        harmony.PatchAll(typeof(UpgradePlayerGrabStrengthPatch));
        harmony.PatchAll(typeof(UpgradePlayerHealthPatch));
        harmony.PatchAll(typeof(UpgradePlayerSprintSpeedPatch));
        harmony.PatchAll(typeof(UpgradePlayerTumbleLaunchPatch));


        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}

[HarmonyPatch(typeof(PlayerAvatar))]
[HarmonyPatch("SpawnRPC")]
public static class PlayerSpawnPatch
{
    static void Prefix(PhotonView ___photonView)
    {
        if (RunManager.instance.levelShop != RunManager.instance.levelCurrent) return;
        if (GameManager.Multiplayer() && !___photonView.IsMine) return;

        Plugin.isOpen = true;
        var repoPopupPage = MenuAPI.CreateREPOPopupPage("Choose an upgrade", REPOPopupPage.PresetSide.Left, pageDimmerVisibility: true, spacing: 1.5f);
        
        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Stamina", () => {
            PunManager.instance.UpgradePlayerEnergy(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();
            

            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(46f, 18f)));
        
        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Extra Jump", () => {
            PunManager.instance.UpgradePlayerExtraJump(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(186f, 18f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Range", () => {
            PunManager.instance.UpgradePlayerGrabRange(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(46f, 60f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Strength", () => {
            PunManager.instance.UpgradePlayerGrabStrength(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(186f, 60f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Health", () => {
            PunManager.instance.UpgradePlayerHealth(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(46f, 102f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Sprint speed", () => {
            PunManager.instance.UpgradePlayerSprintSpeed(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(186f, 102f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Tumble Launch", () => {
            PunManager.instance.UpgradePlayerTumbleLaunch(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(46f, 144f)));

        repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Map Player Count", () => {
            PunManager.instance.UpgradeMapPlayerCount(SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal())));
            StatsUI.instance.Fetch();
            StatsUI.instance.ShowStats();
            CameraGlitch.Instance.PlayUpgrade();


            repoPopupPage.ClosePage(true);
            Plugin.isOpen = false;
            return;
        }, parent, new Vector2(186f, 144f)));

        repoPopupPage.OpenPage(false);
    }
}

//Yippee networking and boilerplate!

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradeMapPlayerCount))]
public static class UpgradeMapPlayerCountPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if(!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradeMapPlayerCountRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeMapPlayerCount[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerEnergy))]
public static class UpgradePlayerEnergyPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerEnergyCountRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeStamina[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerExtraJump))]
public static class UpgradePlayerExtraJumpPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerExtraJumpRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeExtraJump[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabRange))]
public static class UpgradePlayerGrabRangePatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerGrabRangeRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeRange[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerGrabStrength))]
public static class UpgradePlayerGrabStrengthPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerGrabStrengthRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeStrength[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerHealth))]
public static class UpgradePlayerHealthPatch
{
    static void Postfix(string playerName, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerHealthRPC", RpcTarget.Others, playerName, ___statsManager.playerUpgradeHealth[playerName]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerSprintSpeed))]
public static class UpgradePlayerSprintSpeedPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerSprintSpeedRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeSpeed[_steamID]);
        }
    }
}

[HarmonyPatch(typeof(PunManager))]
[HarmonyPatch(nameof(PunManager.UpgradePlayerTumbleLaunch))]
public static class UpgradePlayerTumbleLaunchPatch
{
    static void Postfix(string _steamID, PhotonView ___photonView, StatsManager ___statsManager)
    {
        if (!SemiFunc.IsMasterClient() && GameManager.Multiplayer() && Plugin.isOpen)
        {
            ___photonView.RPC("UpgradePlayerTumbleLaunchRPC", RpcTarget.Others, _steamID, ___statsManager.playerUpgradeLaunch[_steamID]);
        }
    }
}

//All of this is horrible bad jankness to prevent players from accidentally closing the menu before they pick an upgrade

[HarmonyPatch(typeof(MenuPagePopUp))]
[HarmonyPatch(nameof(MenuPagePopUp.ButtonEvent))]
public static class MenuClosePatch
{
    static bool Prefix()
    {
        return !Plugin.isOpen;
    }
}



[HarmonyPatch(typeof(MenuManager))]
[HarmonyPatch(nameof(MenuManager.PageOpen))]
public static class MenuManagerOpenPatch
{
    static bool Prefix()
    {
        return !Plugin.isOpen;
    }
}

[HarmonyPatch(typeof(MenuManager))]
[HarmonyPatch(nameof(MenuManager.PageCloseAll))]
public static class MenuManagerClosePatch
{
    static bool Prefix()
    {
        return !Plugin.isOpen;
    }
}