using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Steamworks;
using BepInEx.Configuration;
using System.Linq;

namespace UpgradeEveryRound;

public class Plugin : BaseUnityPlugin
{
    public const string modGUID = "dev.redfops.repo.upgradeeveryround";
    public const string modName = "Upgrade Every Round";
    public const string modVersion = "1.1.0";

    public static bool isOpen = false;
    public static ConfigEntry<int> upgradesPerRound;
    public static ConfigEntry<bool> limitedChoices;
    public static ConfigEntry<int> numChoices;

    internal static new ManualLogSource Logger;
    private readonly Harmony harmony = new Harmony(modGUID);

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        upgradesPerRound = Config.Bind("Upgrades", "Upgrades Per Round", 1, new ConfigDescription("Number of upgrades per round", new AcceptableValueRange<int>(0, 10)));
        limitedChoices = Config.Bind("Upgrades", "Limited random choices", false, new ConfigDescription("Only presents a fixed number of random options"));
        numChoices = Config.Bind("Upgrades", "Number of choices", 3, new ConfigDescription("Number of options to choose from per upgrade", new AcceptableValueRange<int>(1, 8)));



        harmony.PatchAll(typeof(PlayerSpawnPatch));
        harmony.PatchAll(typeof(RunManagerChangeLevelPatch));
        harmony.PatchAll(typeof(RunManagerMainMenuPatch));
        harmony.PatchAll(typeof(StatsManagerPatch));
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

    public static void ApplyUpgrade(string _steamID, REPOPopupPage popupPage)
    {
        //Update UI to reflect upgrade
        StatsUI.instance.Fetch();
        StatsUI.instance.ShowStats();
        CameraGlitch.Instance.PlayUpgrade();

        int value = ++StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_steamID];
        if (GameManager.Multiplayer())
        {
            //Broadcast that we used an upgrade
            PhotonView _photonView = PunManager.instance.GetComponent<PhotonView>();
            _photonView.RPC("UpdateStatRPC", RpcTarget.Others, "playerUpgradesUsed", _steamID, value);
        }

        //See if we are done upgrading, and if so close the menu
        int upgradesDeserved = RunManager.instance.levelsCompleted * Plugin.upgradesPerRound.Value;
        if (value >= upgradesDeserved)
        {
            isOpen = false;
            popupPage.ClosePage(true);
        }
    }
}

[HarmonyPatch(typeof(PlayerAvatar))]
[HarmonyPatch("SpawnRPC")]
public static class PlayerSpawnPatch
{
    static void Prefix(PhotonView ___photonView)
    {
        Level[] bannedLevels = [RunManager.instance.levelMainMenu, RunManager.instance.levelLobbyMenu, RunManager.instance.levelTutorial];
        if (bannedLevels.Contains(RunManager.instance.levelCurrent)) return;

        string _steamID = SemiFunc.PlayerGetSteamID(SemiFunc.PlayerAvatarGetFromPhotonID(SemiFunc.PhotonViewIDPlayerAvatarLocal()));
        int upgradesDeserved = RunManager.instance.levelsCompleted * Plugin.upgradesPerRound.Value;

#if DEBUG
        upgradesDeserved += 1;
#endif

        if (StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_steamID] >= upgradesDeserved) return;
        if (GameManager.Multiplayer() && !___photonView.IsMine) return;

        // Get number of upgrades to apply
        int upgradesToApply = upgradesDeserved - StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_steamID];

        for (int i = 0; i < upgradesToApply; i++)
        {
            ApplyRandomUpgrade(_steamID);
        }
    }

    private static void ApplyRandomUpgrade(string _steamID)
    {
        List<int> availableChoices = [0, 1, 2, 3, 4, 5, 6, 7];
        int numChoices = Plugin.limitedChoices.Value ? Plugin.numChoices.Value : availableChoices.Count;

        if (Plugin.limitedChoices.Value)
        {
            while (availableChoices.Count > numChoices)
                availableChoices.RemoveAt(Random.Range(0, availableChoices.Count));
        }

        int chosenUpgrade = availableChoices[Random.Range(0, availableChoices.Count)];

        switch (chosenUpgrade)
        {
            case 0:
                PunManager.instance.UpgradePlayerEnergy(_steamID);
                break;
            case 1:
                PunManager.instance.UpgradePlayerExtraJump(_steamID);
                break;
            case 2:
                PunManager.instance.UpgradePlayerGrabRange(_steamID);
                break;
            case 3:
                PunManager.instance.UpgradePlayerGrabStrength(_steamID);
                break;
            case 4:
                PunManager.instance.UpgradePlayerHealth(_steamID);
                break;
            case 5:
                PunManager.instance.UpgradePlayerSprintSpeed(_steamID);
                break;
            case 6:
                PunManager.instance.UpgradePlayerTumbleLaunch(_steamID);
                break;
            case 7:
                PunManager.instance.UpgradeMapPlayerCount(_steamID);
                break;
        }

        // Increment upgrades used and sync if in multiplayer
        int value = ++StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"][_steamID];
        if (GameManager.Multiplayer())
        {
            PhotonView _photonView = PunManager.instance.GetComponent<PhotonView>();
            _photonView.RPC("UpdateStatRPC", RpcTarget.Others, "playerUpgradesUsed", _steamID, value);
        }

        // Optional: Visual effect or audio
        CameraGlitch.Instance?.PlayUpgrade();
    }
}

//Our custom save data handling
[HarmonyPatch(typeof(StatsManager))]
[HarmonyPatch("Start")]
public static class StatsManagerPatch
{
    static void Prefix(StatsManager __instance)
    {
        __instance.dictionaryOfDictionaries.Add("playerUpgradesUsed",[]);
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

//So it turns out that things break sometimes, make sure we reset this value incase they escape the menu through other means
[HarmonyPatch(typeof(RunManager))]
[HarmonyPatch(nameof(RunManager.ChangeLevel))]
public static class RunManagerChangeLevelPatch
{
    static void Prefix()
    {
        Plugin.isOpen = false;
    }
}

[HarmonyPatch(typeof(RunManager))]
[HarmonyPatch(nameof(RunManager.LeaveToMainMenu))]
public static class RunManagerMainMenuPatch
{
    static void Prefix()
    {
        Plugin.isOpen = false;
    }
}
