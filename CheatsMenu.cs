using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using System.Reflection;
using Pigeon.Movement;
using UnityEngine;

public static class CheatsMenu
{
    private static readonly Dictionary<Type, FieldInfo[]> cooldownFieldsByType = new Dictionary<Type, FieldInfo[]>();
    private static readonly FieldInfo cooldownChargeField = typeof(CooldownData).GetField("charge", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    private static readonly FieldInfo cooldownMaxChargesField = typeof(CooldownData).GetField("maxCharges", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    private static readonly PropertyInfo gunReloadingProperty = typeof(Gun).GetProperty("Reloading", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    private static Vector3 savedTeleportPosition;
    private static bool hasSavedTeleportPosition = false;

    public static void CreateCheatsMenu(MenuMod2Menu parentMenu)
    {
        MenuMod2Menu cheatsMenu = new MenuMod2Menu("CHEATS", parentMenu);

        MenuMod2Menu combatMenu = new MenuMod2Menu("Ammo & Abilities", cheatsMenu);
        {
            MM2Button button = null;
            button = combatMenu.addButton("Infinite ammo", () => toggleInfiniteAmmo(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        {
            MM2Button button = null;
            button = combatMenu.addButton("No cooldowns", () => toggleNoCooldowns(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        combatMenu.addButton("Refill ammo now", () => refillAmmoNow());
        combatMenu.addButton("Refresh cooldowns now", () => refreshCooldownsNow());

        MenuMod2Menu movementMenu = new MenuMod2Menu("Movement", cheatsMenu);
        movementMenu.addButton("Movement normal", () => setMovementPreset(10f, 14f));
        movementMenu.addButton("Movement fast", () => setMovementPreset(24f, 20f));
        movementMenu.addButton("Movement very fast", () => setMovementPreset(48f, 28f));
        movementMenu.addButton("Jump high", () => setMovementPreset(10f, 45f));
        movementMenu.addButton("Reset movement", () => resetMovement());

        MenuMod2Menu teleportMenu = new MenuMod2Menu("Teleport", cheatsMenu);
        teleportMenu.addButton("Save position", () => saveTeleportPosition());
        teleportMenu.addButton("Teleport to saved position", () => teleportToSavedPosition());
        teleportMenu.addButton("Teleport to crosshair", () => teleportToCrosshair());

        MenuMod2Menu enemyMenu = new MenuMod2Menu("Enemies", cheatsMenu);
        {
            MM2Button button = null;
            button = enemyMenu.addButton("Toggle enemy spawning", () => toggleSpawning(button))
                .changeSuffix("  ON")
                .changeColour(MenuStyle.Success);
        }
        enemyMenu.addButton("Kill all enemies", () => killAllEnemies());
        enemyMenu.addButton("Spawn swarm x10", () => SpawnMenu.spawnSwarm(10));
        enemyMenu.addButton("Spawn swarm x25", () => SpawnMenu.spawnSwarm(25));
        enemyMenu.addButton("Make enemies flee", () => makeEnemiesFlee());
        enemyMenu.addButton("Stop enemies fleeing", () => stopEnemiesFleeing());
        enemyMenu.addButton("Calm enemy intensity", () => setEnemyIntensity(0f));
        enemyMenu.addButton("Max enemy intensity", () => setEnemyIntensity(1f));
        enemyMenu.addButton("Clean up parts", () => cleanUpParts());
        enemyMenu.addButton("Clean up collectables", () => cleanUpCollectables());

        MenuMod2Menu lootMenu = new MenuMod2Menu("Loot", cheatsMenu);
        {
            MM2Button button = null;
            button = lootMenu.addButton("Infinite resources", () => toggleInfiniteResources(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        lootMenu.addButton("Give max resources", () => giveAllResources());
        lootMenu.addButton("Clear lost loot upgrades", () => clearLostLootUpgrades());
        lootMenu.addButton("Spawn Saxitos", () => SpawnMenu.spawnObject("SaxitosBag"));
        lootMenu.addButton("Spawn barrel", () => SpawnMenu.spawnObject("HoldableBarrel"));
        lootMenu.addButton("Spawn milk", () => SpawnMenu.spawnObject("MilkJug"));

        {
            MM2Button button = null;
            button = cheatsMenu.addButton("Godmode", () => toggleGod(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        {
            MM2Button button = null;
            button = cheatsMenu.addButton("Super sprint", () => toggleSprintFast(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        {
            MM2Button button = null;
            button = cheatsMenu.addButton("Super jump", () => toggleSuperJump(button))
                .changeSuffix("  OFF")
                .changeColour(MenuStyle.Danger);
        }
        cheatsMenu.addButton("Unlock everything", () => ProgressionMenu.unlockEverything());
    }

    public static void toggleGod(MM2Button button = null)
    {
        try
        {
            if (SparrohPlugin.god)
            {
                Player.LocalPlayer.SetMaxHealth(37.5f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
                SparrohPlugin.god = false;
            }
            else
            {
                Player.LocalPlayer.SetMaxHealth(999999f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                SparrohPlugin.god = true;
            }
            if (button != null)
            {
                updateToggleButton(button, SparrohPlugin.god);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleGod: {ex.Message}");
        }
    }

    public static void toggleSprintFast(MM2Button b = null)
    {
        try
        {
            if (SparrohPlugin.sprintFast)
            {
                Player.LocalPlayer.DefaultMoveSpeed = 10;
                SparrohPlugin.sprintFast = false;
            }
            else
            {
                Player.LocalPlayer.DefaultMoveSpeed = 100;
                SparrohPlugin.sprintFast = true;
            }
            if (b != null)
            {
                updateToggleButton(b, SparrohPlugin.sprintFast);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleSprintFast: {ex.Message}");
        }
    }

    public static void toggleSuperJump(MM2Button b = null)
    {
        try
        {
            if (SparrohPlugin.superJump)
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 14f);
                SparrohPlugin.superJump = false;
            }
            else
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 100f);
                SparrohPlugin.superJump = true;
            }
            if (b != null)
            {
                updateToggleButton(b, SparrohPlugin.superJump);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleSuperJump: {ex.Message}");
        }
    }

    public static void setGod(bool enabled, MM2Button b = null)
    {
        if (enabled)
        {
            Player.LocalPlayer.SetMaxHealth(999999f);
            MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
            setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 999999f });
            MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
            setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 999999f });
        }
        else
        {
            Player.LocalPlayer.SetMaxHealth(37.5f);
            MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
            setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
            MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
            setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
        }
    }

    public static void setSprintFast(bool enabled, MM2Button b = null)
    {
        if (enabled)
        {
            Player.LocalPlayer.DefaultMoveSpeed = 100;
        }
        else
        {
            Player.LocalPlayer.DefaultMoveSpeed = 10;
        }
    }

    public static void setSuperJump(bool enabled, MM2Button b = null)
    {
        if (enabled)
        {
            FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(Player.LocalPlayer, 100f);
        }
        else
        {
            FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(Player.LocalPlayer, 14f);
        }
    }

    public static void setMovementPreset(float moveSpeed, float jumpSpeed)
    {
        try
        {
            SparrohPlugin.sprintFast = false;
            SparrohPlugin.superJump = false;
            SparrohPlugin.movementPreset = true;
            SparrohPlugin.moveSpeedOverride = moveSpeed;
            SparrohPlugin.jumpSpeedOverride = jumpSpeed;
            applyMovement(moveSpeed, jumpSpeed);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in setMovementPreset: {ex.Message}");
        }
    }

    public static void resetMovement()
    {
        try
        {
            SparrohPlugin.sprintFast = false;
            SparrohPlugin.superJump = false;
            SparrohPlugin.movementPreset = false;
            applyMovement(10f, 14f);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in resetMovement: {ex.Message}");
        }
    }

    public static void applyMovement(float moveSpeed, float jumpSpeed)
    {
        if (Player.LocalPlayer == null) return;

        Player.LocalPlayer.DefaultMoveSpeed = moveSpeed;
        FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(Player.LocalPlayer, jumpSpeed);
    }

    public static void enemySpawning(bool enabled, MM2Button b = null)
    {
        var em = SparrohPlugin.GetEnemyManager();
        if (em != null)
        {
            if (enabled)
                em.EnableSpawning();
            else
                em.DisableSpawning();
        }
        else
        {
        }
    }

    public static MM2Button toggleSpawning(MM2Button b = null)
    {
        try
        {
            var em = SparrohPlugin.GetEnemyManager();
            if (em != null)
            {
                FieldInfo field = typeof(EnemyManager).GetField("enableAmbientWave", BindingFlags.NonPublic | BindingFlags.Instance);
                if ((bool)field.GetValue(em))
                {
                em.DisableSpawning();
                if (b != null)
                {
                    updateToggleButton(b, false);
                }
            }
            else
            {
                em.EnableSpawning();
                if (b != null)
                {
                    updateToggleButton(b, true);
                }
            }
            }
            else
            {
            }
            return b;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleSpawning: {ex.Message}");
            return b;
        }
    }

    public static void killAllEnemies(MM2Button b = null)
    {
        try
        {
            var em = SparrohPlugin.GetEnemyManager();
            if (em != null)
                em.KillAllEnemies_Server();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in killAllEnemies: {ex.Message}");
        }
    }

    public static void cleanUpParts(MM2Button b = null)
    {
        try
        {
            List<EnemyPart> enemyParts = GameObject.FindObjectsOfType<EnemyPart>().ToList();
            foreach (var part in enemyParts)
            {
                part.Kill(DamageFlags.Despawn);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in cleanUpParts: {ex.Message}");
        }
    }

    public static void cleanUpCollectables(MM2Button b = null)
    {
        try
        {
            List<ClientCollectable> collectables = GameObject.FindObjectsOfType<ClientCollectable>().ToList();
            foreach (var part in collectables)
            {
                part.DespawnTrackedObject();
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in cleanUpCollectables: {ex.Message}");
        }
    }

    public static void clearLostLootUpgrades(MM2Button b = null)
    {
        try
        {
            PlayerData.Instance.rentedUpgrades.Clear();
            SparrohPlugin.Logger.LogInfo("Cleared all rented upgrades from lost loot machine");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in clearLostLootUpgrades: {ex.Message}");
        }
    }

    public static void toggleInfiniteResources(MM2Button b = null)
    {
        try
        {
            SparrohPlugin.infiniteResources = !SparrohPlugin.infiniteResources;
            if (SparrohPlugin.infiniteResources)
            {
                giveAllResources();
                updateToggleButton(b, true);
            }
            else
            {
                updateToggleButton(b, false);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleInfiniteResources: {ex.Message}");
        }
    }

    public static void toggleInfiniteAmmo(MM2Button b = null)
    {
        try
        {
            SparrohPlugin.infiniteAmmo = !SparrohPlugin.infiniteAmmo;
            if (SparrohPlugin.infiniteAmmo)
            {
                refillAmmoNow();
                updateToggleButton(b, true);
            }
            else
            {
                updateToggleButton(b, false);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleInfiniteAmmo: {ex.Message}");
        }
    }

    public static void toggleNoCooldowns(MM2Button b = null)
    {
        try
        {
            SparrohPlugin.noCooldowns = !SparrohPlugin.noCooldowns;
            if (SparrohPlugin.noCooldowns)
            {
                refreshCooldownsNow();
                updateToggleButton(b, true);
            }
            else
            {
                updateToggleButton(b, false);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleNoCooldowns: {ex.Message}");
        }
    }

    public static void refillAmmoNow(MM2Button b = null)
    {
        try
        {
            foreach (Gun gun in UnityEngine.Object.FindObjectsOfType<Gun>())
            {
                gun.RemainingAmmo = Mathf.Max(gun.RemainingAmmo, 999f);
                gun.StoredAmmo = Mathf.Max(gun.StoredAmmo, 999f);
                gunReloadingProperty?.SetValue(gun, false, null);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in refillAmmoNow: {ex.Message}");
        }
    }

    public static void refreshCooldownsNow(MM2Button b = null)
    {
        try
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null) continue;

                foreach (FieldInfo field in getCooldownFields(behaviour.GetType()))
                {
                    object boxedCooldown = field.GetValue(behaviour);
                    if (boxedCooldown == null) continue;

                    int maxCharges = cooldownMaxChargesField != null ? (int)cooldownMaxChargesField.GetValue(boxedCooldown) : 1;
                    if (maxCharges <= 0)
                    {
                        maxCharges = 1;
                    }

                    cooldownChargeField?.SetValue(boxedCooldown, (float)maxCharges);
                    field.SetValue(behaviour, boxedCooldown);
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in refreshCooldownsNow: {ex.Message}");
        }
    }

    private static FieldInfo[] getCooldownFields(Type type)
    {
        if (cooldownFieldsByType.TryGetValue(type, out FieldInfo[] cachedFields))
        {
            return cachedFields;
        }

        List<FieldInfo> fields = new List<FieldInfo>();
        for (Type current = type; current != null; current = current.BaseType)
        {
            fields.AddRange(current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(CooldownData)));
        }

        FieldInfo[] result = fields.ToArray();
        cooldownFieldsByType[type] = result;
        return result;
    }

    public static void saveTeleportPosition(MM2Button b = null)
    {
        try
        {
            if (Player.LocalPlayer == null) return;

            savedTeleportPosition = Player.LocalPlayer.transform.position;
            hasSavedTeleportPosition = true;
            SparrohPlugin.SendTextChatMessageToClient("Saved teleport position.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in saveTeleportPosition: {ex.Message}");
        }
    }

    public static void teleportToSavedPosition(MM2Button b = null)
    {
        try
        {
            if (!hasSavedTeleportPosition || Player.LocalPlayer == null) return;

            teleportPlayer(savedTeleportPosition);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in teleportToSavedPosition: {ex.Message}");
        }
    }

    public static void teleportToCrosshair(MM2Button b = null)
    {
        try
        {
            if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Camera == null) return;

            Transform cameraTransform = Player.LocalPlayer.PlayerLook.Camera.transform;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, 100000f))
            {
                teleportPlayer(hit.point + Vector3.up * 1.5f);
            }
            else
            {
                SparrohPlugin.SendTextChatMessageToClient("No teleport target found.");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in teleportToCrosshair: {ex.Message}");
        }
    }

    private static void teleportPlayer(Vector3 position)
    {
        CharacterController controller = Player.LocalPlayer.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Player.LocalPlayer.transform.position = position;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    public static void makeEnemiesFlee(MM2Button b = null)
    {
        try
        {
            SparrohPlugin.GetEnemyManager()?.MakeAllEnemiesFlee_Server(1000f, 30f);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in makeEnemiesFlee: {ex.Message}");
        }
    }

    public static void stopEnemiesFleeing(MM2Button b = null)
    {
        try
        {
            SparrohPlugin.GetEnemyManager()?.StopFlee_Server();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in stopEnemiesFleeing: {ex.Message}");
        }
    }

    public static void setEnemyIntensity(float intensity, MM2Button b = null)
    {
        try
        {
            SparrohPlugin.GetEnemyManager()?.SetEnemyIntensity(intensity, false);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in setEnemyIntensity: {ex.Message}");
        }
    }

    private static void updateToggleButton(MM2Button button, bool enabled)
    {
        if (button == null) return;

        button.changeSuffix(enabled ? "  ON" : "  OFF");
        button.changeColour(enabled ? MenuStyle.Success : MenuStyle.Danger);
    }

    public static void giveAllResoruces(MM2Button b = null)
    {
        giveAllResources(b);
    }

    public static void giveAllResources(MM2Button b = null)
    {
        try
        {
            if (Global.Instance == null || PlayerData.Instance == null) return;

            var allResources = Global.Instance.PlayerResources;
            foreach (var resource in allResources)
            {
                PlayerData.Instance.AddResource(resource, resource.Max);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveAllResources: {ex.Message}");
        }
    }
}
