using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib.MonoBehaviors;
using MenuLib;
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Steamworks;
using BepInEx.Configuration;
using System.Linq;

namespace UpgradeEveryRound;

[BepInPlugin(modGUID, modName, modVersion), BepInDependency("nickklmao.menulib", "2.1.3")]
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

        MenuManager.instance.PageCloseAll(); //Just in case somehow other menus were opened previously.
        
        var repoPopupPage = MenuAPI.CreateREPOPopupPage("Choose an upgrade", REPOPopupPage.PresetSide.Left, shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f);

        repoPopupPage.menuPage.onPageEnd.AddListener(() => { Plugin.isOpen = false; }); //They really shouldn't be able to close it, but just in case we want to make sure their menus work

        int numChoices = Plugin.limitedChoices.Value ? Plugin.numChoices.Value : 8;
        List<int> choices = [0, 1, 2, 3, 4, 5, 6, 7];

        //Add limited buttons randomly or all in order depending on config
        for (int i = 0; i < numChoices; i++)
        {
            int choiceIndex = Plugin.limitedChoices.Value ? Random.Range(0, choices.Count) : 0; //If not limited choices then we don't need to use random
            int choice = choices[choiceIndex];
            choices.RemoveAt(choiceIndex);
            

            switch (choice)
            {
                case 0:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Stamina", () =>
                    {
                        PunManager.instance.UpgradePlayerEnergy(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(46f, 18f)));
                    break;

                case 1:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Extra Jump", () =>
                    {
                        PunManager.instance.UpgradePlayerExtraJump(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(186f, 18f)));
                    break;

                case 2:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Range", () =>
                    {
                        PunManager.instance.UpgradePlayerGrabRange(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(46f, 60f)));
                    break;

                case 3:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Strength", () =>
                    {
                        PunManager.instance.UpgradePlayerGrabStrength(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(186f, 60f)));
                    break;

                case 4:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Health", () =>
                    {
                        PunManager.instance.UpgradePlayerHealth(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(46f, 102f)));
                    break;

                case 5:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Sprint speed", () =>
                    {
                        PunManager.instance.UpgradePlayerSprintSpeed(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(186f, 102f)));
                    break;

                case 6:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Tumble Launch", () =>
                    {
                        PunManager.instance.UpgradePlayerTumbleLaunch(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(46f, 144f)));
                    break;

                case 7:
                    repoPopupPage.AddElement(parent => MenuAPI.CreateREPOButton("Map Player Count", () =>
                    {
                        PunManager.instance.UpgradeMapPlayerCount(_steamID);
                        Plugin.ApplyUpgrade(_steamID, repoPopupPage);
                        return;
                    }, parent, new Vector2(186f, 144f)));
                    break;
            }
        }

        repoPopupPage.OpenPage(false);
        Plugin.isOpen = true;
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