using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Steamworks;
using BepInEx.Configuration;
using System.Linq;

namespace RandomUpgradeEveryRound;

[BepInProcess("REPO.exe")]
[BepInPlugin(Plugin.modGUID, Plugin.modName, Plugin.modVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string modGUID = "dev.quantumaidan.repo.randomupgradeeveryround";
    public const string modName = "Random Upgrade Every Round";
    public const string modVersion = "1.1.0";

    public static Plugin Instance { get; private set; }
    public static bool isOpen = false;
    public static ConfigEntry<int> upgradesPerRound;
    public static ConfigEntry<bool> limitedChoices;
    public static ConfigEntry<int> numChoices;

    internal static new ManualLogSource Logger;
    private readonly Harmony harmony = new Harmony(modGUID);

    private void Awake()
    {
        // Plugin startup logic
        Instance = this;
        Logger = base.Logger;

        _ = CoroutineRunner.Instance;

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


        Logger.LogInfo($"Plugin {modGUID} is loaded!");
    }

    public static void ApplyUpgrade(string _steamID)
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
            // no more popupPage.ClosePage(true);
        }
    }
}

[HarmonyPatch(typeof(PlayerAvatar))]
[HarmonyPatch("SpawnRPC")]
public static class PlayerSpawnPatch
{
    static void Prefix(PhotonView ___photonView)
    {
        if (RunManager.instance.levelCurrent != RunManager.instance.levelShop)
        {
            //Plugin.Logger.LogInfo("[UpgradeEveryRound] Skipping upgrade logic — not in Shop.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            //Plugin.Logger.LogInfo("[UpgradeEveryRound] Skipped upgrade logic — not master client.");
            return;
        }

        Plugin.Logger.LogInfo("[UpgradeEveryRound] Running upgrade logic as master client.");
        CoroutineRunner.Instance.StartCoroutine(WaitForPlayersAndApplyUpgrades());
    }


    private static void ApplyRandomUpgrade(string _steamID)
    {

        switch (Random.Range(0, 8))
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
                try
                {
                    PunManager.instance.UpgradePlayerTumbleLaunch(_steamID);
                }
                catch (System.NullReferenceException ex)
                {
                    Plugin.Logger.LogWarning($"Caught NullRef in TumbleLaunch for {_steamID}: {ex.Message}");
                }
                break;
            case 7:
                if (StatsManager.instance.playerUpgradeMapPlayerCount.ContainsKey(_steamID) &&
                    StatsManager.instance.playerUpgradeMapPlayerCount[_steamID] > 0)
                {
                    Plugin.Logger.LogInfo($"[UpgradeEveryRound] Rerolling upgrade for {_steamID} — already has MapPlayerCount");
                    ApplyRandomUpgrade(_steamID); // Try another one
                    return;
                }
                else
                {
                    PunManager.instance.UpgradeMapPlayerCount(_steamID);
                }
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

    /*
    private static IEnumerator WaitUntilStatInitialized(Dictionary<string, int> dict, string steamID, string statName, int maxTries = 600)
    {
        int tries = 0;
        while (!dict.ContainsKey(steamID))
        {
            if (tries++ >= maxTries)
            {
                Plugin.Logger.LogWarning($"[UpgradeEveryRound] Timeout while waiting for {statName} to init for {steamID}");
                yield break;
            }

            Plugin.Logger.LogInfo($"[UpgradeEveryRound] Waiting for {statName} to init for {steamID}... ({tries}/{maxTries})");
            yield return null;
        }
    }
    */

    private static System.Collections.IEnumerator WaitForPlayersAndApplyUpgrades()
    {
        List<PlayerAvatar> players = null;

        // Poll until player avatars are ready
        while ((players = SemiFunc.PlayerGetAll()) == null || players.Count == 0)
        {
            Plugin.Logger.LogInfo("[UpgradeEveryRound] Waiting for player avatars to load...");
            yield return null; // wait 1 frame
        }

        foreach (var avatar in players)
        {
            string steamID = SemiFunc.PlayerGetSteamID(avatar);
            if (steamID == null) { break; }
               
            var stats = StatsManager.instance;

            if (!stats.dictionaryOfDictionaries["playerUpgradesUsed"].ContainsKey(steamID))
                stats.dictionaryOfDictionaries["playerUpgradesUsed"][steamID] = 0;

            if (!stats.playerUpgradeLaunch.ContainsKey(steamID)) stats.playerUpgradeLaunch[steamID] = 0;
            if (!stats.playerUpgradeStamina.ContainsKey(steamID)) stats.playerUpgradeStamina[steamID] = 0;
            if (!stats.playerUpgradeExtraJump.ContainsKey(steamID)) stats.playerUpgradeExtraJump[steamID] = 0;
            if (!stats.playerUpgradeRange.ContainsKey(steamID)) stats.playerUpgradeRange[steamID] = 0;
            if (!stats.playerUpgradeStrength.ContainsKey(steamID)) stats.playerUpgradeStrength[steamID] = 0;
            if (!stats.playerUpgradeHealth.ContainsKey(steamID)) stats.playerUpgradeHealth[steamID] = 0;
            if (!stats.playerUpgradeSpeed.ContainsKey(steamID)) stats.playerUpgradeSpeed[steamID] = 0;
            if (!stats.playerUpgradeMapPlayerCount.ContainsKey(steamID)) stats.playerUpgradeMapPlayerCount[steamID] = 0;


            //Plugin.Logger.LogInfo($"[UpgradeEveryRound] Found player avatar: {steamID}");

            int upsDeserved = RunManager.instance.levelsCompleted * Plugin.upgradesPerRound.Value;

            #if DEBUG
                upsDeserved += 1;
            #endif

            int upsUsed = StatsManager.instance.dictionaryOfDictionaries["playerUpgradesUsed"].GetValueOrDefault(steamID, 0);
            int upsToApply = Mathf.Max(1, upsDeserved - upsUsed);

            Plugin.Logger.LogInfo($"Player {steamID}: Upgrades used: {upsUsed}, deserved: {upsDeserved}, to apply: {upsToApply}");

            for (int i = 0; i < upsToApply; i++)
            {
                //Plugin.Logger.LogInfo($"[UpgradeEveryRound] Applying upgrade {i + 1} to {steamID}");
                ApplyRandomUpgrade(steamID);
            }
        }
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

        // Initialize all player upgrade stat dictionaries to prevent null refs
        __instance.playerUpgradeLaunch = new Dictionary<string, int>();
        __instance.playerUpgradeStamina = new Dictionary<string, int>();
        __instance.playerUpgradeExtraJump = new Dictionary<string, int>();
        __instance.playerUpgradeRange = new Dictionary<string, int>();
        __instance.playerUpgradeStrength = new Dictionary<string, int>();
        __instance.playerUpgradeHealth = new Dictionary<string, int>();
        __instance.playerUpgradeSpeed = new Dictionary<string, int>();
        __instance.playerUpgradeMapPlayerCount = new Dictionary<string, int>();
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

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CoroutineRunner");
                _instance = go.AddComponent<CoroutineRunner>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
}


