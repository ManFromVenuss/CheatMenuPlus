using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Pigeon.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[MycoMod(null, ModFlags.IsClientSide | ModFlags.IsSandbox)]
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "venuss.cheatmenuplus";
    public const string PluginName = "CheatMenu+";
    public const string PluginVersion = "1.5.5";
    
    public static ManualLogSource Logger;
    public InputActionMap _actionmap;
    private InputAction _openMenu;
    public GameObject menuCanvas;
    private bool enabled = false;
    public List<MenuMod2Menu> allMenues = new List<MenuMod2Menu>();
    public MenuMod2Menu mainMenu;

    public static int previousAirJumps = 0;
    public static float previousAirJumpSpeed = 0;
    public static bool god = false;
    public static bool sprintFast = false;
    public static bool superJump = false;
    public static bool airJump = false;
    public static bool infiniteResources = false;
    public static bool infiniteAmmo = false;
    public static bool noCooldowns = false;
    public static bool movementPreset = false;
    public static float moveSpeedOverride = 10f;
    public static float jumpSpeedOverride = 14f;
    public static bool sparrohMode = false;
    public static List<MissionModifier> forcedModifiers = new List<MissionModifier>();
    private static float nextAmmoRefreshTime = 0f;
    private static float nextCooldownRefreshTime = 0f;

    private void Awake()
    {
        Logger = base.Logger;

        Harmony.CreateAndPatchAll(typeof(SparrohPlugin));

        SceneManager.sceneLoaded += OnSceneLoaded;

        _actionmap = new InputActionMap();
        _openMenu = _actionmap.AddAction("OpenMenu");
        _openMenu.AddBinding("<Keyboard>/insert");
        _openMenu.performed += _ => toggleMenu();
        
        Logger.LogInfo($"{PluginName} loaded");
    }

    private void Update()
    {
        ApplyActiveCheats();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Logger.LogDebug($"Scene loaded: {scene.name}");
        if (PlayerData.ProfileConfig.Instance == null)
            return;
        FieldInfo field =
            typeof(PlayerData.ProfileConfig).GetField("profileIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            return;
        }

        int profileIndex = (int)field.GetValue(PlayerData.ProfileConfig.Instance);
        if (profileIndex == 0)
        {
            Logger.LogWarning(
                $"Using default profile is not supported.  Please switch to a different profile on the main menu.");
            _actionmap.Disable();
            return;
        }

        createMainMenu();
        _actionmap.Enable();
    }


    public void createMainMenu()
    {
        try
        {
            if (mainMenu != null)
            {
                return;
            }
            
            if (Global.Instance != null)
            {
            }
            mainMenu = new MenuMod2Menu("Main Menu");

            SpawnMenu.CreateSpawnMenu(mainMenu);
            CheatsMenu.CreateCheatsMenu(mainMenu);

            if (Global.Instance != null)
            {
                MissionsMenu.CreateMissionsMenu(mainMenu);
                UpgradesMenu.CreateUpgradesMenu(mainMenu);
                UpgradesMenu.RehydratePersistedCustomUpgrades();
                OuroborosMenu.CreateOuroborosMenu(mainMenu);
                ProgressionMenu.CreateProgressionMenu(mainMenu);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in createMainMenu: {ex.Message}");
            if (mainMenu == null)
            {
                mainMenu = new MenuMod2Menu("Main Menu");
                var backupMenu = new MenuMod2Menu("Error - Check Logs", mainMenu);
                backupMenu.addButton("Error occurred - check logs", () => { });
            }
        }
    }

    public static List<T> GetItemsFromWeightedArray<T>(object weightedArray)
    {
        if (weightedArray == null) return null;

        Type type = weightedArray.GetType();

        if (!type.IsGenericType || type.GetGenericTypeDefinition().Name != "WeightedArray`1")
            throw new ArgumentException("Expected instance of WeightedArray<T>");

        FieldInfo itemsField = type.GetField("items", BindingFlags.NonPublic | BindingFlags.Instance);
        if (itemsField == null)
            throw new MissingFieldException("Could not find 'items' field in WeightedArray");

        Array items = itemsField.GetValue(weightedArray) as Array;
        if (items == null) return null;

        List<T> result = new List<T>();

        foreach (var node in items)
        {
            if (node == null) continue;

            var nodeType = node.GetType();
            FieldInfo weightField = nodeType.GetField("weight");
            FieldInfo valueField = nodeType.GetField("value");

            if (weightField == null || valueField == null) continue;

            int weight = (int)weightField.GetValue(node);
            if (weight > 0)
            {
                T value = (T)valueField.GetValue(node);
                result.Add(value);
            }
        }

        return result;
    }

    public void toggleMenu()
    {
        try
        {
            if (MenuMod2Manager.currentMenu != null)
            {
                MenuMod2Manager.currentMenu.Close();
            }
            else
            {
                if (mainMenu == null)
                {
                    if (PlayerData.ProfileConfig.Instance != null)
                    {
                        FieldInfo field = typeof(PlayerData.ProfileConfig).GetField("profileIndex",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            int profileIndex = (int)field.GetValue(PlayerData.ProfileConfig.Instance);
                            if (profileIndex != 0)
                            {
                                createMainMenu();
                            }
                        }
                    }
                }

                if (mainMenu != null)
                {
                    mainMenu.Open();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in toggleMenu: {ex.Message}");
        }
    }

    public static GameObject findObjectByName(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == name)
            {
                if (!obj.scene.IsValid())
                    return obj;
            }
        }

        return null;
    }

    public static void AddEnemyMenu(MenuMod2Menu enemyMenu)
    {
        try
        {
            {
                MM2Button button = null;
                button = enemyMenu.addButton("Toggle enemy spawning", () => CheatsMenu.toggleSpawning(button))
                    .changeColour(Color.green);
            }
            enemyMenu.addButton("Kill all enemies", () => CheatsMenu.killAllEnemies());
            enemyMenu.addButton("Spawn swarm", () => SpawnMenu.spawnSwarm(10));
            enemyMenu.addButton("Clean up parts", () => CheatsMenu.cleanUpParts());
            enemyMenu.addButton("Clean up collectables", () => CheatsMenu.cleanUpCollectables());
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in AddEnemyMenu: {ex.Message}");
            enemyMenu.addButton("Enemy spawning error", () => { });
        }
    }

    public static string CleanRichText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    public static void SendTextChatMessageToClient(string Message)
    {
        Player.LocalPlayer.PlayerLook.AddTextChatMessage(Message, Player.LocalPlayer);
    }

    public static EnemyManager GetEnemyManager()
    {
        return EnemyManager.Instance;
    }

    public static void ApplyActiveCheats()
    {
        try
        {
            if (Player.LocalPlayer == null) return;

            if (god)
            {
                Player.LocalPlayer.SetMaxHealth(999999f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 999999f });
            }

            if (sprintFast)
            {
                Player.LocalPlayer.DefaultMoveSpeed = 100;
            }

            if (superJump)
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 100f);
            }

            if (movementPreset)
            {
                CheatsMenu.applyMovement(moveSpeedOverride, jumpSpeedOverride);
            }

            if (infiniteResources)
            {
                CheatsMenu.giveAllResources();
            }

            if (infiniteAmmo)
            {
                if (Time.unscaledTime >= nextAmmoRefreshTime)
                {
                    CheatsMenu.refillAmmoNow();
                    nextAmmoRefreshTime = Time.unscaledTime + 0.10f;
                }
            }

            if (noCooldowns)
            {
                if (Time.unscaledTime >= nextCooldownRefreshTime)
                {
                    CheatsMenu.refreshCooldownsNow();
                    nextCooldownRefreshTime = Time.unscaledTime + 0.15f;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in ApplyActiveCheats: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Mission), "GetModifierCount")]
    [HarmonyPostfix]
    static void GetModifierCountPostfix(int seed, ref int __result)
    {
        try
        {
            var forced = forcedModifiers;
            if (forced != null && forced.Count > 0)
            {
                __result += forced.Count;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in GetModifierCountPostfix: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Mission), "GetModifiers")]
    [HarmonyPostfix]
    static void GetModifiersPostfix(Span<int> indices, int seed, Mission mission, int startIndex, int count, bool allowStacking)
    {
        try
        {
            var forced = forcedModifiers;
            if (forced != null && forced.Count > 0 && indices.Length >= forced.Count)
            {
                for (int i = 0; i < forced.Count && i < indices.Length; i++)
                {
                    var modifierIndex = Global.Instance.MissionModifiers.IndexOf(forced[i]);
                    if (modifierIndex >= 0)
                    {
                        for (int j = indices.Length - 1; j > i; j--)
                        {
                            indices[j] = indices[j - 1];
                        }
                        indices[i] = modifierIndex;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in GetModifiersPostfix: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.GetInstanceName), typeof(UpgradeInstance))]
    [HarmonyPostfix]
    static void GetInstanceNamePostfix(UpgradeInstance instance, ref string __result)
    {
        try
        {
            UpgradesMenu.TryGetRuntimeCustomName(instance, ref __result);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in GetInstanceNamePostfix: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.GetStatList))]
    [HarmonyPrefix]
    static void UpgradeGetStatListPrefix(Upgrade __instance, UpgradeInstance instance, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomProperties(__instance, instance, ref __state);
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.GetStatList))]
    [HarmonyPostfix]
    static void UpgradeGetStatListPostfix(Upgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.OnEquippedInGrid))]
    [HarmonyPrefix]
    static void UpgradeOnEquippedInGridPrefix(Upgrade __instance, UpgradeInstance instance, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomProperties(__instance, instance, ref __state);
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.OnEquippedInGrid))]
    [HarmonyPostfix]
    static void UpgradeOnEquippedInGridPostfix(Upgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    [HarmonyPatch(typeof(GenericGunUpgrade), nameof(GenericGunUpgrade.Apply))]
    [HarmonyPrefix]
    static void GenericGunUpgradeApplyPrefix(GenericGunUpgrade __instance, UpgradeInstance instance, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomProperties(__instance, instance, ref __state);
    }

    [HarmonyPatch(typeof(GenericGunUpgrade), nameof(GenericGunUpgrade.Apply))]
    [HarmonyPostfix]
    static void GenericGunUpgradeApplyPostfix(GenericGunUpgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    [HarmonyPatch(typeof(GenericGunUpgrade), nameof(GenericGunUpgrade.Remove))]
    [HarmonyPrefix]
    static void GenericGunUpgradeRemovePrefix(GenericGunUpgrade __instance, UpgradeInstance instance, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomProperties(__instance, instance, ref __state);
    }

    [HarmonyPatch(typeof(GenericGunUpgrade), nameof(GenericGunUpgrade.Remove))]
    [HarmonyPostfix]
    static void GenericGunUpgradeRemovePostfix(GenericGunUpgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    [HarmonyPatch(typeof(SkinUpgrade), nameof(SkinUpgrade.Apply))]
    [HarmonyPrefix]
    static void SkinUpgradeApplyPrefix(SkinUpgrade __instance, int seed, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomSkinProperties(__instance, seed, ref __state);
    }

    [HarmonyPatch(typeof(SkinUpgrade), nameof(SkinUpgrade.Apply))]
    [HarmonyPostfix]
    static void SkinUpgradeApplyPostfix(SkinUpgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    [HarmonyPatch(typeof(SkinUpgrade), nameof(SkinUpgrade.Remove))]
    [HarmonyPrefix]
    static void SkinUpgradeRemovePrefix(SkinUpgrade __instance, int seed, ref UpgradesMenu.RuntimePropertySwapState __state)
    {
        TryBeginRuntimeCustomSkinProperties(__instance, seed, ref __state);
    }

    [HarmonyPatch(typeof(SkinUpgrade), nameof(SkinUpgrade.Remove))]
    [HarmonyPostfix]
    static void SkinUpgradeRemovePostfix(SkinUpgrade __instance, UpgradesMenu.RuntimePropertySwapState __state)
    {
        UpgradesMenu.EndRuntimeCustomProperties(__instance, __state);
    }

    private static void TryBeginRuntimeCustomProperties(Upgrade upgrade, UpgradeInstance instance, ref UpgradesMenu.RuntimePropertySwapState state)
    {
        try
        {
            UpgradesMenu.TryBeginRuntimeCustomProperties(upgrade, instance, out state);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in TryBeginRuntimeCustomProperties: {ex.Message}");
        }
    }

    private static void TryBeginRuntimeCustomSkinProperties(SkinUpgrade skin, int seed, ref UpgradesMenu.RuntimePropertySwapState state)
    {
        try
        {
            UpgradesMenu.TryBeginRuntimeCustomSkinProperties(skin, seed, out state);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception in TryBeginRuntimeCustomSkinProperties: {ex.Message}");
        }
    }
}
