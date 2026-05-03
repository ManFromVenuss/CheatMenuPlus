using System;
using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Pigeon.Movement;
using UnityEngine;

public static class UpgradesMenu
{
    private static int nextCustomUpgradeId = 800000000;
    private static int nextCustomMenuId = 0;
    private static readonly Dictionary<int, RuntimeCustomUpgrade> runtimeCustomUpgrades = new Dictionary<int, RuntimeCustomUpgrade>();
    private static readonly Dictionary<string, RuntimeCustomUpgrade> runtimeCustomSkins = new Dictionary<string, RuntimeCustomUpgrade>();
    private static readonly string persistencePath = Path.Combine(Paths.ConfigPath, "CheatMenu.customupgrades.txt");
    private static readonly string cosmeticPersistencePath = Path.Combine(Paths.ConfigPath, "CheatMenu.customcosmetics.txt");
    private static readonly FieldInfo upgradePropertiesField = typeof(UpgradePropertyList).GetField("properties",
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

    private class CustomStatTarget
    {
        public int PropertyIndex;
        public string FieldName;
        public string Label;
        public bool IsFireInterval;
    }

    private class RuntimeCustomUpgrade
    {
        public Upgrade Source;
        public UpgradeProperty[] Properties;
        public string Name;
        public List<CustomStatOverride> Overrides;
    }

    private class CustomStatInput
    {
        public CustomStatTarget Target;
        public MM2Button Input;
    }

    private class CustomCosmeticTarget
    {
        public int PropertyIndex;
        public string FieldName;
        public string Label;
        public string Channel;
        public Type ValueType;
    }

    private class CustomCosmeticInput
    {
        public CustomCosmeticTarget Target;
        public MM2Button Input;
    }

    private class CustomStatOverride
    {
        public int PropertyIndex;
        public string FieldName;
        public float Percent;
    }

    private class PersistedCustomUpgrade
    {
        public int InstanceId;
        public string Name;
        public List<CustomStatOverride> Overrides = new List<CustomStatOverride>();
    }

    private class CustomCosmeticOverride
    {
        public int PropertyIndex;
        public string FieldName;
        public string Channel;
        public float Value;
    }

    private class PersistedCustomCosmetic
    {
        public int InstanceId;
        public int Seed;
        public string Name;
        public List<CustomCosmeticOverride> Overrides = new List<CustomCosmeticOverride>();
    }

    public class RuntimePropertySwapState
    {
        public FieldInfo PropertyListField;
        public UpgradeProperty[] OriginalProperties;
    }

    public static void CreateUpgradesMenu(MenuMod2Menu parentMenu)
    {
        try
        {
            const string debugPattern =
                @"(_test_|_dev_|_wip|debug|temp|placeholder|todo|_old|_backup|_copy|\.skinasset$|^test_|roachard)";
            MenuMod2Menu upgradesMenu = new MenuMod2Menu("UPGRADES", parentMenu);
            CreateCustomUpgradeMenu(upgradesMenu, debugPattern);
            CreateCosmeticPickupMenu(upgradesMenu, debugPattern);

            var gearByType = Global.Instance.AllGear
                .Where(g => g.Info.Upgrades.Count > 0)
                .GroupBy(g => g.GearType)
                .OrderBy(g => g.Key.ToString());

            foreach (var gearGroup in gearByType)
            {
                var typeName = gearGroup.Key.ToString();
                var gearTypeMenu = new MenuMod2Menu(typeName, upgradesMenu);

                foreach (var gear in gearGroup.OrderBy(g => g.Info.Name))
                {
                    var gearInfo = gear.Info;
                    var individualGearMenu = new MenuMod2Menu(gearInfo.Name, gearTypeMenu);

                    var allGearUpgrades = gearInfo.Upgrades
                    .Where(u => !Regex.IsMatch(u.Name, debugPattern, RegexOptions.IgnoreCase) || SparrohPlugin.sparrohMode)
                        .OrderByDescending(u => u.Rarity)
                        .ThenBy(u => SparrohPlugin.CleanRichText(u.Name))
                        .ToList();

                    foreach (var upgrade in allGearUpgrades)
                    {
                        if (upgrade.UpgradeType == Upgrade.Type.Cosmetic)
                        {
                            var button = individualGearMenu.addButton(SparrohPlugin.CleanRichText(upgrade.Name),
                                () => giveCosmetic(upgrade, gear));
                            button.changeColour(upgrade.Color);
                        }
                        else
                        {
                            var button = individualGearMenu.addButton(SparrohPlugin.CleanRichText(upgrade.Name),
                                () => giveUpgrade(upgrade, gear));
                            button.changeColour(upgrade.Color);
                        }
                    }
                }
            }

            MenuMod2Menu charactersMenu = new MenuMod2Menu("Characters", upgradesMenu);
            foreach (var character in Global.Instance.Characters)
            {
                var gearInfo = character.Info;
                var individualCharMenu = new MenuMod2Menu(gearInfo.Name, charactersMenu);

                var allCharacterUpgrades = character.Info.Upgrades
                    .Where(u => !Regex.IsMatch(u.Name, debugPattern, RegexOptions.IgnoreCase) || SparrohPlugin.sparrohMode)
                    .OrderByDescending(u => u.Rarity)
                    .ThenBy(u => SparrohPlugin.CleanRichText(u.Name))
                    .ToList();

                foreach (var upgrade in allCharacterUpgrades)
                {
                    if (upgrade.UpgradeType == Upgrade.Type.Cosmetic)
                    {
                        var button = individualCharMenu.addButton(SparrohPlugin.CleanRichText(upgrade.Name),
                            () => giveCosmetic(upgrade, character));
                        button.changeColour(upgrade.Color);
                    }
                    else
                    {
                        var button = individualCharMenu.addButton(SparrohPlugin.CleanRichText(upgrade.Name),
                            () => giveCharacterUpgrade(upgrade, character));
                        button.changeColour(upgrade.Color);
                    }
                }
            }

            MenuMod2Menu specificGeneric = new MenuMod2Menu("Universal", charactersMenu);

            try
            {
                var allUpgrades = PlayerData.GetAllUpgrades(Global.Instance);
                GenericPlayerUpgrade[] allGenericUpgrades = allUpgrades.Where(u => u.Upgrade is GenericPlayerUpgrade).Select(u => u.Upgrade as GenericPlayerUpgrade).ToArray();

                HashSet<Upgrade> skillTreeUpgrades = new HashSet<Upgrade>();
                foreach (var character in Global.Instance.Characters)
                {
                    var skillTree = character.SkillTree;
                    SkillTreeUpgradeUI[] treeUpgradesUI = skillTree.GetComponentsInChildren<SkillTreeUpgradeUI>();
                    skillTreeUpgrades.UnionWith(treeUpgradesUI.Select(ui => ui.Upgrade));
                }

                HashSet<Upgrade> characterSpecificUpgrades = new HashSet<Upgrade>();
                foreach (var character in Global.Instance.Characters)
                {
                    characterSpecificUpgrades.UnionWith(character.Info.Upgrades);
                }

                var genericUpgrades = allGenericUpgrades
                    .Where(u => u.UpgradeType != Upgrade.Type.Cosmetic &&
                                (!Regex.IsMatch(u.Name, debugPattern, RegexOptions.IgnoreCase) || SparrohPlugin.sparrohMode) &&
                                !characterSpecificUpgrades.Contains(u))
                    .ToList();


                foreach (var u in genericUpgrades)
                {
                }

                Dictionary<string, Upgrade> uniqueGenerics = new Dictionary<string, Upgrade>();
                foreach (var upgrade in genericUpgrades)
                {
                    if (!uniqueGenerics.ContainsKey(upgrade.APIName))
                    {
                        uniqueGenerics[upgrade.APIName] = upgrade;
                    }
                }

                if (uniqueGenerics.Any())
                {
                    foreach (var kvp in uniqueGenerics.OrderByDescending(kvp => kvp.Value.Rarity).ThenBy(kvp => kvp.Key))
                    {
                        var upgrade = kvp.Value;

                        bool isOwned = false;

                        var button = specificGeneric.addButton(SparrohPlugin.CleanRichText(upgrade.Name),
                            () => giveUniversalUpgrade(upgrade));
                        button.changeColour(isOwned ? Color.green : upgrade.Color);
                    }
                }
                else
                {
                    specificGeneric.addButton("No generics found", () => { });
                }
            }
            catch (System.Exception ex)
            {
                SparrohPlugin.Logger.LogError($"Exception in universal upgrade menu creation: {ex.Message}\n{ex.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateUpgradesMenu: {ex.Message}");
        }
    }

    private static void CreateCustomUpgradeMenu(MenuMod2Menu upgradesMenu, string debugPattern)
    {
        try
        {
            MenuMod2Menu customMenu = new MenuMod2Menu(nextCustomMenuName("Custom Stats"), upgradesMenu);
            customMenu.thisButton?.changeName("Custom Stats");
            int customUpgradeCount = 0;
            customUpgradeCount += CreateCustomUpgradeCategory(customMenu, "Gear", Global.Instance.AllGear.Cast<IUpgradable>(), debugPattern);
            customUpgradeCount += CreateCustomUpgradeCategory(customMenu, "Characters", Global.Instance.Characters.Cast<IUpgradable>(), debugPattern);
            customUpgradeCount += CreateUniversalCustomUpgradeCategory(customMenu, debugPattern);

            if (customUpgradeCount == 0)
            {
                customMenu.addButton("No custom stats found", () => { });
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateCustomUpgradeMenu: {ex.Message}");
        }
    }

    private static void CreateCosmeticPickupMenu(MenuMod2Menu upgradesMenu, string debugPattern)
    {
        try
        {
            MenuMod2Menu cosmeticMenu = new MenuMod2Menu(nextCustomMenuName("Cosmetic Pickups"), upgradesMenu);
            cosmeticMenu.thisButton?.changeName("Cosmetic Pickups");

            int cosmeticCount = 0;
            cosmeticCount += CreateCosmeticPickupCategory(cosmeticMenu, "Gear", Global.Instance.AllGear.Cast<IUpgradable>(), debugPattern);
            cosmeticCount += CreateCosmeticPickupCategory(cosmeticMenu, "Characters", Global.Instance.Characters.Cast<IUpgradable>(), debugPattern);

            if (Global.Instance.DropPod is IUpgradable dropPod && dropPod.Info != null)
            {
                MenuMod2Menu dropPodMenu = new MenuMod2Menu(nextCustomMenuName("Cosmetic Drop Pod"), cosmeticMenu);
                dropPodMenu.thisButton?.changeName("Drop Pod");
                cosmeticCount += CreateCosmeticPickupGearMenu(dropPodMenu, "Drop Pod", dropPod, debugPattern, false);
            }

            if (cosmeticCount == 0)
            {
                cosmeticMenu.addButton("No cosmetics found", () => { });
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateCosmeticPickupMenu: {ex.Message}");
        }
    }

    private static int CreateCosmeticPickupCategory(MenuMod2Menu cosmeticMenu, string categoryName, IEnumerable<IUpgradable> upgradables, string debugPattern)
    {
        int cosmeticCount = 0;
        MenuMod2Menu categoryMenu = new MenuMod2Menu(nextCustomMenuName($"Cosmetic {categoryName}"), cosmeticMenu);
        categoryMenu.thisButton?.changeName(categoryName);

        foreach (var gear in upgradables.OrderBy(g => g.Info.Name))
        {
            cosmeticCount += CreateCosmeticPickupGearMenu(categoryMenu, categoryName, gear, debugPattern, true);
        }

        if (cosmeticCount == 0)
        {
            categoryMenu.addButton("No cosmetics found", () => { });
        }

        return cosmeticCount;
    }

    private static int CreateCosmeticPickupGearMenu(MenuMod2Menu parentMenu, string categoryName, IUpgradable gear, string debugPattern, bool createGearMenu)
    {
        var cosmetics = gear.Info.Upgrades
            .Where(upgrade => upgrade.UpgradeType == Upgrade.Type.Cosmetic &&
                              (!Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase) || SparrohPlugin.sparrohMode))
            .OrderBy(upgrade => SparrohPlugin.CleanRichText(upgrade.Name))
            .ToList();

        if (cosmetics.Count == 0)
        {
            return 0;
        }

        MenuMod2Menu gearMenu = createGearMenu
            ? new MenuMod2Menu(nextCustomMenuName($"Cosmetic {categoryName} {gear.Info.Name}"), parentMenu)
            : parentMenu;
        if (createGearMenu)
        {
            gearMenu.thisButton?.changeName(gear.Info.Name);
        }

        foreach (var cosmetic in cosmetics)
        {
            string cosmeticName = SparrohPlugin.CleanRichText(cosmetic.Name);
            MenuMod2Menu skinMenu = new MenuMod2Menu(nextCustomMenuName($"Cosmetic {categoryName} {gear.Info.Name} {cosmeticName}"), gearMenu);
            skinMenu.thisButton?.changeName(cosmeticName);
            MM2Button seedInput = skinMenu.addInput("Seed", string.Empty);
            skinMenu.addButton("Spawn random", () => spawnCosmeticPickup(cosmetic, gear, null));
            skinMenu.addButton("Spawn seed", () => spawnCosmeticPickup(cosmetic, gear, seedInput.getInputText()));

            List<CustomCosmeticTarget> customTargets = getCustomCosmeticTargets(cosmetic);
            if (customTargets.Count > 0)
            {
                MenuMod2Menu customMenu = new MenuMod2Menu(nextCustomMenuName($"Custom Cosmetic {categoryName} {gear.Info.Name} {cosmeticName}"), skinMenu);
                customMenu.thisButton?.changeName("Custom pickup");
                MM2Button customSeedInput = customMenu.addInput("Seed", string.Empty);
                List<CustomCosmeticInput> inputs = new List<CustomCosmeticInput>();
                foreach (var target in customTargets)
                {
                    MM2Button input = customMenu.addInput(target.Label, string.Empty);
                    inputs.Add(new CustomCosmeticInput { Target = target, Input = input });
                }

                customMenu.addButton("Spawn custom", () => spawnCustomCosmeticPickup(cosmetic, gear, customSeedInput.getInputText(), inputs));
            }
        }

        return cosmetics.Count;
    }

    private static int CreateCustomUpgradeCategory(MenuMod2Menu customMenu, string categoryName, IEnumerable<IUpgradable> upgradables, string debugPattern)
    {
        int customUpgradeCount = 0;
        MenuMod2Menu categoryMenu = new MenuMod2Menu(nextCustomMenuName($"Custom {categoryName}"), customMenu);
        categoryMenu.thisButton?.changeName(categoryName);

        foreach (var gear in upgradables.OrderBy(g => g.Info.Name))
        {
            customUpgradeCount += CreateCustomGearMenu(categoryMenu, categoryName, gear, gear.Info.Upgrades, debugPattern);
        }

        if (customUpgradeCount == 0)
        {
            categoryMenu.addButton("No custom stats found", () => { });
        }

        return customUpgradeCount;
    }

    private static int CreateUniversalCustomUpgradeCategory(MenuMod2Menu customMenu, string debugPattern)
    {
        int customUpgradeCount = 0;
        MenuMod2Menu universalMenu = new MenuMod2Menu(nextCustomMenuName("Custom Universal"), customMenu);
        universalMenu.thisButton?.changeName("Universal");

        var allUpgrades = PlayerData.GetAllUpgrades(Global.Instance);
        var uniqueUpgrades = allUpgrades
            .Where(info => info.Upgrade is GenericPlayerUpgrade)
            .Select(info => info.Upgrade)
            .GroupBy(upgrade => upgrade.APIName)
            .Select(group => group.First())
            .OrderBy(upgrade => SparrohPlugin.CleanRichText(upgrade.Name))
            .ToList();

        customUpgradeCount += CreateCustomGearMenu(universalMenu, "Universal", Global.Instance, uniqueUpgrades, debugPattern);

        if (customUpgradeCount == 0)
        {
            universalMenu.addButton("No custom stats found", () => { });
        }

        return customUpgradeCount;
    }

    private static int CreateCustomGearMenu(MenuMod2Menu categoryMenu, string categoryName, IUpgradable gear, IEnumerable<Upgrade> upgrades, string debugPattern)
    {
        var customUpgrades = upgrades
            .Where(upgrade => upgrade.UpgradeType != Upgrade.Type.Cosmetic &&
                              (!Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase) || SparrohPlugin.sparrohMode) &&
                              getCustomStatTargets(upgrade).Count > 0)
            .OrderBy(upgrade => SparrohPlugin.CleanRichText(upgrade.Name))
            .ToList();

        if (customUpgrades.Count == 0)
        {
            return 0;
        }

        MenuMod2Menu gearMenu = new MenuMod2Menu(nextCustomMenuName($"Custom {categoryName} {gear.Info.Name}"), categoryMenu);
        gearMenu.thisButton?.changeName(gear.Info.Name);
        foreach (var upgrade in customUpgrades)
        {
            string upgradeName = SparrohPlugin.CleanRichText(upgrade.Name);
            MenuMod2Menu upgradeMenu = new MenuMod2Menu(nextCustomMenuName($"Custom {categoryName} {gear.Info.Name} {upgradeName}"), gearMenu);
            upgradeMenu.thisButton?.changeName(upgradeName);

            List<CustomStatInput> inputs = new List<CustomStatInput>();
            foreach (var target in getCustomStatTargets(upgrade))
            {
                MM2Button input = upgradeMenu.addInput(target.Label, string.Empty);
                inputs.Add(new CustomStatInput { Target = target, Input = input });
            }

            upgradeMenu.addButton("Spawn pickup", () => spawnCustomStatUpgradePickup(upgrade, gear, inputs));
        }

        return customUpgrades.Count;
    }

    private static string nextCustomMenuName(string label)
    {
        nextCustomMenuId++;
        return $"CustomStats::{nextCustomMenuId}::{label}";
    }

    public static void giveAllUpgrades(MM2Button b = null)
    {
        try
        {
            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Invalid && upgrade.UpgradeType != Upgrade.Type.Cosmetic)
                    {
                        var iUpgrade = new UpgradeInstance(upgrade, gear);
                        PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                        iUpgrade.Unlock(true);
                    }
                }
            }
            SparrohPlugin.SendTextChatMessageToClient("All upgrades for weapons are added silently.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveAllUpgrades: {ex.Message}");
        }
    }

    public static void giveAllCosmetics(MM2Button b = null)
    {
        try
        {
            const string debugPattern = @"(_test_|_dev_|_wip|debug|temp|placeholder|todo|_old|_backup|_copy|\.skinasset$|^test_)";
            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Cosmetic ||
                        Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase))
                        continue;
                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }

            foreach (var gear in Global.Instance.Characters)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Cosmetic ||
                        Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase))
                        continue;

                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }

            if (Global.Instance.DropPod != null)
            {
                var dropPodUpgradable = (IUpgradable)Global.Instance.DropPod;
                var gearInfo = dropPodUpgradable.Info;
                if (gearInfo != null)
                {
                    foreach (var upgrade in gearInfo.Upgrades)
                    {
                        if (upgrade.UpgradeType == Upgrade.Type.Cosmetic &&
                            !Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase))
                        {
                            var iUpgrade = new UpgradeInstance(upgrade, dropPodUpgradable);
                            PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                            iUpgrade.Unlock(true);
                        }
                    }
                }
            }

            SparrohPlugin.SendTextChatMessageToClient("All cosmetics for characters, weapons, and drop pod are added silently.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveAllCosmetics: {ex.Message}");
        }
    }

    public static void giveCosmetic(Upgrade upgrade, IUpgradable gear, MM2Button b = null)
    {
        try
        {
            var iUpgrade = new UpgradeInstance(upgrade, gear);
            PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
            iUpgrade.Unlock(true);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveCosmetic: {ex.Message}");
        }
    }

    public static void giveUpgrade(Upgrade upgrade, IUpgradable gear, MM2Button b = null)
    {
        try
        {
            var iUpgrade = new UpgradeInstance(upgrade, gear);
            PlayerData.CollectInstance(iUpgrade);
            iUpgrade.Unlock(true);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveUpgrade: {ex.Message}");
        }
    }

    public static void giveCharacterUpgrade(Upgrade upgrade, IUpgradable character, MM2Button b = null)
    {
        try
        {
            UpgradeInstance upgradeInstance = PlayerData.CollectInstance(character, upgrade, PlayerData.UnlockFlags.Hidden);
            upgradeInstance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            upgradeInstance.Unlock(false);
            PlayerData.Instance.TotalSkillPointsSpent += 1;
            if (character is Character charObj && charObj.SkillTree != null)
            {
                charObj.SkillTree.Refresh();
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveCharacterUpgrade: {ex.Message}");
        }
    }

    public static void giveUniversalUpgrade(Upgrade upgrade, MM2Button b = null)
    {
        try
        {
            IUpgradable gear = Global.Instance;
            UpgradeInstance upgradeInstance = new UpgradeInstance(upgrade, gear);
            PlayerData.CollectInstance(upgradeInstance, PlayerData.UnlockFlags.Hidden);
            upgradeInstance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            upgradeInstance.Unlock(true);
            ((PlayerUpgrade)upgrade).Apply(Player.LocalPlayer, upgradeInstance);
            SparrohPlugin.SendTextChatMessageToClient("Universal upgrade added silently.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveUniversalUpgrade: {ex.Message}");
        }
    }

    private static void spawnCosmeticPickup(Upgrade cosmetic, IUpgradable gear, string seedText, MM2Button b = null)
    {
        try
        {
            if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Camera == null)
            {
                SparrohPlugin.SendTextChatMessageToClient("Spawn a cosmetic pickup after loading into the world.");
                return;
            }

            int seed;
            if (string.IsNullOrWhiteSpace(seedText))
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            else if (!int.TryParse(seedText, out seed))
            {
                SparrohPlugin.SendTextChatMessageToClient("Invalid cosmetic seed.");
                return;
            }

            var instance = new UpgradeInstance(cosmetic, gear);
            instance.Seed = seed;

            Transform cameraTransform = Player.LocalPlayer.PlayerLook.Camera.transform;
            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, 100000f))
            {
                SparrohPlugin.SendTextChatMessageToClient("No cosmetic spawn target found.");
                return;
            }

            UpgradeCollectable collectable = PlayerData.SpawnSkinCollectable(hit.point + Vector3.up * 0.5f, instance, 1f);
            if (collectable == null)
            {
                SparrohPlugin.SendTextChatMessageToClient("Could not spawn cosmetic pickup.");
                return;
            }

            string instanceName = cosmetic.GetInstanceName(seed);
            SparrohPlugin.SendTextChatMessageToClient($"Spawned {SparrohPlugin.CleanRichText(instanceName)} cosmetic pickup.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnCosmeticPickup: {ex.Message}");
        }
    }

    private static void spawnCustomCosmeticPickup(Upgrade cosmetic, IUpgradable gear, string seedText, List<CustomCosmeticInput> inputs)
    {
        try
        {
            if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Camera == null)
            {
                SparrohPlugin.SendTextChatMessageToClient("Spawn a custom cosmetic pickup after loading into the world.");
                return;
            }

            int seed;
            if (string.IsNullOrWhiteSpace(seedText))
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            else if (!int.TryParse(seedText, out seed))
            {
                SparrohPlugin.SendTextChatMessageToClient("Invalid cosmetic seed.");
                return;
            }

            List<CustomCosmeticOverride> overrides = new List<CustomCosmeticOverride>();
            foreach (var input in inputs)
            {
                string valueText = input.Input.getInputText();
                if (string.IsNullOrWhiteSpace(valueText))
                {
                    continue;
                }

                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                    !float.TryParse(valueText, out value))
                {
                    SparrohPlugin.SendTextChatMessageToClient("Invalid cosmetic value.");
                    return;
                }

                overrides.Add(new CustomCosmeticOverride
                {
                    PropertyIndex = input.Target.PropertyIndex,
                    FieldName = input.Target.FieldName,
                    Channel = input.Target.Channel,
                    Value = Mathf.Clamp(value, -10000f, 10000f)
                });
            }

            if (overrides.Count == 0)
            {
                SparrohPlugin.SendTextChatMessageToClient("Type at least one cosmetic value.");
                return;
            }

            if (!tryCreateModifiedCosmeticProperties(cosmetic, overrides, out UpgradeProperty[] clonedProperties))
            {
                SparrohPlugin.SendTextChatMessageToClient("One of those cosmetic fields is not supported yet.");
                return;
            }

            string customName = buildCustomCosmeticName(cosmetic, seed, overrides);
            var instance = new UpgradeInstance(cosmetic, gear);
            instance.Seed = seed;

            var custom = new RuntimeCustomUpgrade
            {
                Source = cosmetic,
                Properties = clonedProperties,
                Name = customName,
                Overrides = new List<CustomStatOverride>()
            };
            runtimeCustomUpgrades[instance.InstanceID] = custom;
            runtimeCustomSkins[getSkinRuntimeKey(cosmetic, seed)] = custom;
            savePersistedCustomCosmetic(instance.InstanceID, seed, customName, overrides);

            Transform cameraTransform = Player.LocalPlayer.PlayerLook.Camera.transform;
            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, 100000f))
            {
                SparrohPlugin.SendTextChatMessageToClient("No cosmetic spawn target found.");
                return;
            }

            UpgradeCollectable collectable = PlayerData.SpawnSkinCollectable(hit.point + Vector3.up * 0.5f, instance, 1f);
            if (collectable == null)
            {
                runtimeCustomUpgrades.Remove(instance.InstanceID);
                runtimeCustomSkins.Remove(getSkinRuntimeKey(cosmetic, seed));
                SparrohPlugin.SendTextChatMessageToClient("Could not spawn custom cosmetic pickup.");
                return;
            }

            SparrohPlugin.SendTextChatMessageToClient($"Spawned {customName} cosmetic pickup.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnCustomCosmeticPickup: {ex.Message}");
        }
    }


    public static void giveDoubleTimeX10(MM2Button b = null)
    {
        try
        {
            int givenCount = 0;

            foreach (var gear in Global.Instance.AllGear)
            {
                var upgrade = gear.Info.Upgrades.FirstOrDefault(u =>
                    string.Equals(SparrohPlugin.CleanRichText(u.Name), "Double Time", StringComparison.OrdinalIgnoreCase));

                if (upgrade == null)
                {
                    continue;
                }

                if (giveUpgradeWithFireIntervalMultiplier(upgrade, gear, 0.1f))
                {
                    givenCount++;
                }
            }

            if (givenCount == 0)
            {
                SparrohPlugin.SendTextChatMessageToClient("Could not find Double Time.");
                return;
            }

            SparrohPlugin.SendTextChatMessageToClient($"Gave Double Time x10 to {givenCount} matching gear item(s).");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveDoubleTimeX10: {ex.Message}");
        }
    }

    public static void giveCustomFireRateUpgrade(Upgrade upgrade, IUpgradable gear, float fireRateMultiplier, MM2Button b = null)
    {
        try
        {
            float fireIntervalMultiplier = 1f / Mathf.Max(0.1f, fireRateMultiplier);
            if (giveUpgradeWithFireIntervalMultiplier(upgrade, gear, fireIntervalMultiplier))
            {
                SparrohPlugin.SendTextChatMessageToClient(
                    $"Gave {SparrohPlugin.CleanRichText(upgrade.Name)} x{fireRateMultiplier:0.#} to {gear.Info.Name}.");
            }
            else
            {
                SparrohPlugin.SendTextChatMessageToClient("That upgrade does not expose a fire-rate stat.");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveCustomFireRateUpgrade: {ex.Message}");
        }
    }

    public static bool TryGetRuntimeCustomName(UpgradeInstance instance, ref string name)
    {
        if (instance == null)
        {
            return false;
        }

        if (!runtimeCustomUpgrades.TryGetValue(instance.InstanceID, out RuntimeCustomUpgrade customUpgrade) ||
            customUpgrade == null ||
            string.IsNullOrEmpty(customUpgrade.Name))
        {
            return false;
        }

        name = customUpgrade.Name;
        return true;
    }

    public static void RehydratePersistedCustomUpgrades()
    {
        try
        {
            foreach (var persisted in loadPersistedCustomUpgrades())
            {
                if (runtimeCustomUpgrades.ContainsKey(persisted.InstanceId))
                {
                    continue;
                }

                UpgradeInstance instance = PlayerData.GetUpgradeInstanceFromID(persisted.InstanceId);
                if (instance == null || instance.Upgrade == null || persisted.Overrides.Count == 0)
                {
                    continue;
                }

                if (!tryCreateModifiedProperties(instance.Upgrade, persisted.Overrides, out UpgradeProperty[] properties))
                {
                    continue;
                }

                runtimeCustomUpgrades[persisted.InstanceId] = new RuntimeCustomUpgrade
                {
                    Source = instance.Upgrade,
                    Properties = properties,
                    Name = persisted.Name,
                    Overrides = persisted.Overrides
                };
            }

            foreach (var persisted in loadPersistedCustomCosmetics())
            {
                if (runtimeCustomUpgrades.ContainsKey(persisted.InstanceId))
                {
                    continue;
                }

                UpgradeInstance instance = PlayerData.GetUpgradeInstanceFromID(persisted.InstanceId);
                if (instance == null || instance.Upgrade == null || persisted.Overrides.Count == 0)
                {
                    continue;
                }

                if (!tryCreateModifiedCosmeticProperties(instance.Upgrade, persisted.Overrides, out UpgradeProperty[] properties))
                {
                    continue;
                }

                var custom = new RuntimeCustomUpgrade
                {
                    Source = instance.Upgrade,
                    Properties = properties,
                    Name = persisted.Name,
                    Overrides = new List<CustomStatOverride>()
                };
                runtimeCustomUpgrades[persisted.InstanceId] = custom;
                runtimeCustomSkins[getSkinRuntimeKey(instance.Upgrade, persisted.Seed)] = custom;
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in RehydratePersistedCustomUpgrades: {ex.Message}");
        }
    }

    public static bool TryBeginRuntimeCustomProperties(Upgrade upgrade, UpgradeInstance instance, out RuntimePropertySwapState state)
    {
        state = null;
        if (upgrade == null || instance == null)
        {
            return false;
        }

        if (!runtimeCustomUpgrades.TryGetValue(instance.InstanceID, out RuntimeCustomUpgrade customUpgrade) ||
            customUpgrade == null ||
            customUpgrade.Source != upgrade ||
            customUpgrade.Properties == null ||
            customUpgrade.Properties.Length == 0)
        {
            return false;
        }

        FieldInfo propertyListField = getPropertyListField(upgrade.GetType());
        if (propertyListField == null || upgradePropertiesField == null)
        {
            return false;
        }

        object propertyList = propertyListField.GetValue(upgrade);
        UpgradeProperty[] originalProperties = upgradePropertiesField.GetValue(propertyList) as UpgradeProperty[];
        upgradePropertiesField.SetValue(propertyList, customUpgrade.Properties);
        propertyListField.SetValue(upgrade, propertyList);

        state = new RuntimePropertySwapState
        {
            PropertyListField = propertyListField,
            OriginalProperties = originalProperties
        };
        return true;
    }

    public static bool TryBeginRuntimeCustomSkinProperties(SkinUpgrade skin, int seed, out RuntimePropertySwapState state)
    {
        state = null;
        if (skin == null)
        {
            return false;
        }

        if (!runtimeCustomSkins.TryGetValue(getSkinRuntimeKey(skin, seed), out RuntimeCustomUpgrade customUpgrade) ||
            customUpgrade == null ||
            customUpgrade.Properties == null ||
            customUpgrade.Properties.Length == 0)
        {
            return false;
        }

        FieldInfo propertyListField = getPropertyListField(skin.GetType());
        object propertyList = propertyListField != null ? propertyListField.GetValue(skin) : skin.Properties;
        if (propertyList == null)
        {
            return false;
        }

        UpgradeProperty[] originalProperties = upgradePropertiesField.GetValue(propertyList) as UpgradeProperty[];
        if (originalProperties == null)
        {
            return false;
        }

        upgradePropertiesField.SetValue(propertyList, customUpgrade.Properties);
        state = new RuntimePropertySwapState
        {
            PropertyListField = propertyListField,
            OriginalProperties = originalProperties
        };
        return true;
    }

    public static void EndRuntimeCustomProperties(Upgrade upgrade, RuntimePropertySwapState state)
    {
        if (upgrade == null || state == null || state.PropertyListField == null || state.OriginalProperties == null || upgradePropertiesField == null)
        {
            return;
        }

        object propertyList = state.PropertyListField.GetValue(upgrade);
        upgradePropertiesField.SetValue(propertyList, state.OriginalProperties);
        state.PropertyListField.SetValue(upgrade, propertyList);
    }

    public static bool TryGetRuntimeCustomProperty(UpgradeProperty property, UpgradeInstance instance, out UpgradeProperty customProperty)
    {
        customProperty = null;
        if (property == null || instance == null)
        {
            return false;
        }

        if (!runtimeCustomUpgrades.TryGetValue(instance.InstanceID, out RuntimeCustomUpgrade customUpgrade) ||
            customUpgrade == null ||
            customUpgrade.Source == null ||
            customUpgrade.Properties == null)
        {
            return false;
        }

        UpgradeProperty[] sourceProperties = getUpgradeProperties(customUpgrade.Source).ToArray();
        for (int i = 0; i < sourceProperties.Length && i < customUpgrade.Properties.Length; i++)
        {
            if (ReferenceEquals(sourceProperties[i], property))
            {
                customProperty = customUpgrade.Properties[i];
                return customProperty != null;
            }
        }

        return false;
    }

    public static void ForgetRuntimeCustomUpgrade(UpgradeInstance instance)
    {
        if (instance != null)
        {
            runtimeCustomUpgrades.Remove(instance.InstanceID);
        }
    }

    public static void ForgetRuntimeCustomUpgrade(int instanceId)
    {
        runtimeCustomUpgrades.Remove(instanceId);
    }

    public static bool HasRuntimeCustomUpgrade(UpgradeInstance instance)
    {
        return instance != null && runtimeCustomUpgrades.ContainsKey(instance.InstanceID);
    }

    public static bool TryCreateRuntimeCustomPropertyEnumerator(UpgradeProperty property, Pigeon.Math.Random rand, IUpgradable gear, UpgradeInstance upgrade, out IEnumerator<StatData> result)
    {
        result = null;
        if (!TryGetRuntimeCustomProperty(property, upgrade, out UpgradeProperty customProperty))
        {
            return false;
        }

        result = customProperty.GetStatData(rand, gear, upgrade);
        return true;
    }

    private static void spawnCustomStatUpgradePickup(Upgrade upgrade, IUpgradable gear, List<CustomStatInput> inputs, MM2Button b = null)
    {
        try
        {
            if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Camera == null)
            {
                SparrohPlugin.SendTextChatMessageToClient("Spawn a custom pickup from inside a mission.");
                return;
            }

            List<CustomStatOverride> overrides = new List<CustomStatOverride>();
            foreach (var input in inputs)
            {
                string percentText = input.Input.getInputText();
                if (string.IsNullOrWhiteSpace(percentText))
                {
                    continue;
                }

                if (!float.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent) &&
                    !float.TryParse(percentText, out percent))
                {
                    SparrohPlugin.SendTextChatMessageToClient("Invalid percentage.");
                    return;
                }

                overrides.Add(new CustomStatOverride
                {
                    PropertyIndex = input.Target.PropertyIndex,
                    FieldName = input.Target.FieldName,
                    Percent = Mathf.Clamp(percent, -10000f, 10000f)
                });
            }

            if (overrides.Count == 0)
            {
                SparrohPlugin.SendTextChatMessageToClient("Type at least one custom stat percentage.");
                return;
            }

            if (!tryCreateModifiedProperties(upgrade, overrides, out UpgradeProperty[] clonedProperties))
            {
                SparrohPlugin.SendTextChatMessageToClient("One of those stat types is not supported yet.");
                return;
            }

            string customName = buildCustomName(upgrade, overrides);
            var instance = new UpgradeInstance(upgrade, gear);
            instance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            runtimeCustomUpgrades[instance.InstanceID] = new RuntimeCustomUpgrade
            {
                Source = upgrade,
                Properties = clonedProperties,
                Name = customName,
                Overrides = overrides
            };
            savePersistedCustomUpgrade(instance.InstanceID, customName, overrides);

            Transform cameraTransform = Player.LocalPlayer.PlayerLook.Camera.transform;
            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, 100000f))
            {
                SparrohPlugin.SendTextChatMessageToClient("No pickup spawn target found.");
                return;
            }

            UpgradeCollectable collectable = PlayerData.SpawnUpgradeCollectable(hit.point + Vector3.up * 0.5f, instance, 1f, false, false);
            if (collectable == null)
            {
                runtimeCustomUpgrades.Remove(instance.InstanceID);
                SparrohPlugin.SendTextChatMessageToClient("Could not spawn custom pickup.");
                return;
            }

            SparrohPlugin.SendTextChatMessageToClient($"Spawned {customName} pickup.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnCustomStatUpgradePickup: {ex.Message}");
        }
    }

    private static bool tryCreateModifiedProperties(Upgrade upgrade, List<CustomStatOverride> overrides, out UpgradeProperty[] clonedProperties)
    {
        clonedProperties = cloneUpgradeProperties(upgrade);
        foreach (var customOverride in overrides)
        {
            if (customOverride.PropertyIndex < 0 || customOverride.PropertyIndex >= clonedProperties.Length)
            {
                return false;
            }

            UpgradeProperty property = clonedProperties[customOverride.PropertyIndex];
            FieldInfo field = getFields(property.GetType()).FirstOrDefault(f => f.Name == customOverride.FieldName);
            if (field == null)
            {
                return false;
            }

            bool isFireInterval = field.Name.ToLowerInvariant().Contains("fireinterval");
            float rawMultiplier = customOverride.Percent / 100f;
            float appliedMultiplier = isFireInterval && customOverride.Percent > -99f ? 100f / (customOverride.Percent + 100f) : rawMultiplier;
            object originalValue = field.GetValue(property);
            if (!tryCreateModifiedStatValue(originalValue, field.FieldType, appliedMultiplier, isFireInterval, out object modifiedValue))
            {
                return false;
            }

            field.SetValue(property, modifiedValue);
        }

        return true;
    }

    private static string buildCustomName(Upgrade upgrade, List<CustomStatOverride> overrides)
    {
        string baseName = SparrohPlugin.CleanRichText(upgrade.Name);
        string label;
        if (overrides.Count == 1)
        {
            label = $"{getCustomOverrideLabel(upgrade, overrides[0])} {overrides[0].Percent:0.#}%";
        }
        else
        {
            label = $"{overrides.Count} custom stats";
        }

        return $"{baseName} [{label}]";
    }

    private static string getCustomOverrideLabel(Upgrade upgrade, CustomStatOverride customOverride)
    {
        UpgradeProperty[] properties = getUpgradeProperties(upgrade).ToArray();
        if (customOverride.PropertyIndex < 0 || customOverride.PropertyIndex >= properties.Length || properties[customOverride.PropertyIndex] == null)
        {
            return customOverride.FieldName;
        }

        return prettifyStatLabel(properties[customOverride.PropertyIndex].GetType().Name, customOverride.FieldName);
    }

    private static void savePersistedCustomUpgrade(int instanceId, string customName, List<CustomStatOverride> overrides)
    {
        try
        {
            List<PersistedCustomUpgrade> persisted = loadPersistedCustomUpgrades()
                .Where(item => item.InstanceId != instanceId)
                .ToList();
            persisted.Add(new PersistedCustomUpgrade
            {
                InstanceId = instanceId,
                Name = customName,
                Overrides = overrides
            });
            Directory.CreateDirectory(Path.GetDirectoryName(persistencePath));
            File.WriteAllLines(persistencePath, persisted.Select(serializePersistedCustomUpgrade).ToArray());
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in savePersistedCustomUpgrade: {ex.Message}");
        }
    }

    private static List<PersistedCustomUpgrade> loadPersistedCustomUpgrades()
    {
        List<PersistedCustomUpgrade> result = new List<PersistedCustomUpgrade>();
        if (!File.Exists(persistencePath))
        {
            return result;
        }

        foreach (string line in File.ReadAllLines(persistencePath))
        {
            if (tryDeserializePersistedCustomUpgrade(line, out PersistedCustomUpgrade persisted))
            {
                result.Add(persisted);
            }
        }

        return result;
    }

    private static string serializePersistedCustomUpgrade(PersistedCustomUpgrade persisted)
    {
        string overrides = string.Join("|", persisted.Overrides.Select(o =>
            $"{o.PropertyIndex}:{escape(o.FieldName)}:{o.Percent.ToString(CultureInfo.InvariantCulture)}"));
        return $"{persisted.InstanceId}\t{escape(persisted.Name)}\t{overrides}";
    }

    private static bool tryDeserializePersistedCustomUpgrade(string line, out PersistedCustomUpgrade persisted)
    {
        persisted = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] parts = line.Split('\t');
        if (parts.Length < 3 || !int.TryParse(parts[0], out int instanceId))
        {
            return false;
        }

        PersistedCustomUpgrade item = new PersistedCustomUpgrade
        {
            InstanceId = instanceId,
            Name = unescape(parts[1])
        };

        foreach (string overridePart in parts[2].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] overridePieces = overridePart.Split(':');
            if (overridePieces.Length != 3 ||
                !int.TryParse(overridePieces[0], out int propertyIndex) ||
                !float.TryParse(overridePieces[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
            {
                continue;
            }

            item.Overrides.Add(new CustomStatOverride
            {
                PropertyIndex = propertyIndex,
                FieldName = unescape(overridePieces[1]),
                Percent = percent
            });
        }

        if (item.Overrides.Count == 0)
        {
            return false;
        }

        persisted = item;
        return true;
    }

    private static void savePersistedCustomCosmetic(int instanceId, int seed, string customName, List<CustomCosmeticOverride> overrides)
    {
        try
        {
            List<PersistedCustomCosmetic> persisted = loadPersistedCustomCosmetics()
                .Where(item => item.InstanceId != instanceId)
                .ToList();
            persisted.Add(new PersistedCustomCosmetic
            {
                InstanceId = instanceId,
                Seed = seed,
                Name = customName,
                Overrides = overrides
            });
            Directory.CreateDirectory(Path.GetDirectoryName(cosmeticPersistencePath));
            File.WriteAllLines(cosmeticPersistencePath, persisted.Select(serializePersistedCustomCosmetic).ToArray());
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in savePersistedCustomCosmetic: {ex.Message}");
        }
    }

    private static List<PersistedCustomCosmetic> loadPersistedCustomCosmetics()
    {
        List<PersistedCustomCosmetic> result = new List<PersistedCustomCosmetic>();
        if (!File.Exists(cosmeticPersistencePath))
        {
            return result;
        }

        foreach (string line in File.ReadAllLines(cosmeticPersistencePath))
        {
            if (tryDeserializePersistedCustomCosmetic(line, out PersistedCustomCosmetic persisted))
            {
                result.Add(persisted);
            }
        }

        return result;
    }

    private static string serializePersistedCustomCosmetic(PersistedCustomCosmetic persisted)
    {
        string overrides = string.Join("|", persisted.Overrides.Select(o =>
            $"{o.PropertyIndex}:{escape(o.FieldName)}:{escape(o.Channel)}:{o.Value.ToString(CultureInfo.InvariantCulture)}"));
        return $"{persisted.InstanceId}\t{persisted.Seed}\t{escape(persisted.Name)}\t{overrides}";
    }

    private static bool tryDeserializePersistedCustomCosmetic(string line, out PersistedCustomCosmetic persisted)
    {
        persisted = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] parts = line.Split('\t');
        if (parts.Length < 4 ||
            !int.TryParse(parts[0], out int instanceId) ||
            !int.TryParse(parts[1], out int seed))
        {
            return false;
        }

        PersistedCustomCosmetic item = new PersistedCustomCosmetic
        {
            InstanceId = instanceId,
            Seed = seed,
            Name = unescape(parts[2])
        };

        foreach (string overridePart in parts[3].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] overridePieces = overridePart.Split(':');
            if (overridePieces.Length != 4 ||
                !int.TryParse(overridePieces[0], out int propertyIndex) ||
                !float.TryParse(overridePieces[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                continue;
            }

            item.Overrides.Add(new CustomCosmeticOverride
            {
                PropertyIndex = propertyIndex,
                FieldName = unescape(overridePieces[1]),
                Channel = unescape(overridePieces[2]),
                Value = value
            });
        }

        if (item.Overrides.Count == 0)
        {
            return false;
        }

        persisted = item;
        return true;
    }

    private static string escape(string value)
    {
        return Uri.EscapeDataString(value ?? string.Empty);
    }

    private static string unescape(string value)
    {
        return Uri.UnescapeDataString(value ?? string.Empty);
    }

    private static string getSkinRuntimeKey(Upgrade skin, int seed)
    {
        return skin == null ? string.Empty : $"{skin.ModGUID}:{skin.NumberID}:{seed}";
    }

    private static bool giveUpgradeWithFireIntervalMultiplier(Upgrade upgrade, IUpgradable gear, float fireIntervalMultiplier)
    {
        List<Tuple<UpgradeProperty, FieldInfo, object>> originalValues = new List<Tuple<UpgradeProperty, FieldInfo, object>>();

        try
        {
            foreach (var property in getUpgradeProperties(upgrade))
            {
                if (property == null)
                {
                    continue;
                }

                foreach (var field in getFields(property.GetType()))
                {
                    if (!field.Name.ToLowerInvariant().Contains("fireinterval") ||
                        !field.FieldType.Name.StartsWith("OverrideData", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    object originalValue = field.GetValue(property);
                    object modifiedValue = createFireIntervalOverride(originalValue, fireIntervalMultiplier);

                    originalValues.Add(Tuple.Create(property, field, originalValue));
                    field.SetValue(property, modifiedValue);
                }
            }

            if (originalValues.Count == 0)
            {
                return false;
            }

            var instance = new UpgradeInstance(upgrade, gear);
            instance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            PlayerData.CollectInstance(instance, PlayerData.UnlockFlags.Hidden);
            instance.Unlock(true);
            return true;
        }
        finally
        {
            foreach (var original in originalValues)
            {
                original.Item2.SetValue(original.Item1, original.Item3);
            }
        }
    }

    private static bool hasFireIntervalOverride(Upgrade upgrade)
    {
        foreach (var property in getUpgradeProperties(upgrade))
        {
            if (property == null)
            {
                continue;
            }

            foreach (var field in getFields(property.GetType()))
            {
                if (field.Name.ToLowerInvariant().Contains("fireinterval") &&
                    field.FieldType.Name.StartsWith("OverrideData", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<CustomStatTarget> getCustomStatTargets(Upgrade upgrade)
    {
        List<CustomStatTarget> targets = new List<CustomStatTarget>();
        UpgradeProperty[] properties = getUpgradeProperties(upgrade).ToArray();

        for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
        {
            UpgradeProperty property = properties[propertyIndex];
            if (property == null)
            {
                continue;
            }

            foreach (var field in getFields(property.GetType()))
            {
                object value = field.GetValue(property);
                if (!tryCreateModifiedStatValue(value, field.FieldType, 1f, field.Name.ToLowerInvariant().Contains("fireinterval"), out _))
                {
                    continue;
                }

                targets.Add(new CustomStatTarget
                {
                    PropertyIndex = propertyIndex,
                    FieldName = field.Name,
                    Label = prettifyStatLabel(property.GetType().Name, field.Name),
                    IsFireInterval = field.Name.ToLowerInvariant().Contains("fireinterval")
                });
            }
        }

        return targets;
    }

    private static List<CustomCosmeticTarget> getCustomCosmeticTargets(Upgrade cosmetic)
    {
        List<CustomCosmeticTarget> targets = new List<CustomCosmeticTarget>();
        UpgradeProperty[] properties = getUpgradeProperties(cosmetic).ToArray();

        for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
        {
            UpgradeProperty property = properties[propertyIndex];
            if (property == null || !property.GetType().Name.StartsWith("SkinUpgradeProperty", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var field in getFields(property.GetType()))
            {
                if (!isSupportedCosmeticField(field.FieldType))
                {
                    continue;
                }

                if (field.FieldType == typeof(Color))
                {
                    targets.Add(createCosmeticTarget(propertyIndex, property.GetType().Name, field, "R"));
                    targets.Add(createCosmeticTarget(propertyIndex, property.GetType().Name, field, "G"));
                    targets.Add(createCosmeticTarget(propertyIndex, property.GetType().Name, field, "B"));
                    targets.Add(createCosmeticTarget(propertyIndex, property.GetType().Name, field, "A"));
                }
                else
                {
                    targets.Add(createCosmeticTarget(propertyIndex, property.GetType().Name, field, string.Empty));
                }
            }
        }

        return targets;
    }

    private static CustomCosmeticTarget createCosmeticTarget(int propertyIndex, string propertyTypeName, FieldInfo field, string channel)
    {
        string label = prettifyStatLabel(propertyTypeName, field.Name);
        if (!string.IsNullOrEmpty(channel))
        {
            label = $"{label} {channel}";
        }

        return new CustomCosmeticTarget
        {
            PropertyIndex = propertyIndex,
            FieldName = field.Name,
            Label = label,
            Channel = channel,
            ValueType = field.FieldType
        };
    }

    private static bool isSupportedCosmeticField(Type fieldType)
    {
        return fieldType == typeof(float) ||
               fieldType == typeof(int) ||
               fieldType == typeof(bool) ||
               fieldType == typeof(Color);
    }

    private static bool tryCreateModifiedCosmeticProperties(Upgrade cosmetic, List<CustomCosmeticOverride> overrides, out UpgradeProperty[] clonedProperties)
    {
        clonedProperties = cloneUpgradeProperties(cosmetic);
        foreach (var customOverride in overrides)
        {
            if (customOverride.PropertyIndex < 0 || customOverride.PropertyIndex >= clonedProperties.Length)
            {
                return false;
            }

            UpgradeProperty property = clonedProperties[customOverride.PropertyIndex];
            FieldInfo field = getFields(property.GetType()).FirstOrDefault(f => f.Name == customOverride.FieldName);
            if (field == null)
            {
                return false;
            }

            if (!tryApplyCustomCosmeticValue(property, field, customOverride.Channel, customOverride.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool tryApplyCustomCosmeticValue(UpgradeProperty property, FieldInfo field, string channel, float value)
    {
        if (field.FieldType == typeof(float))
        {
            field.SetValue(property, value);
            return true;
        }

        if (field.FieldType == typeof(int))
        {
            field.SetValue(property, Mathf.RoundToInt(value));
            return true;
        }

        if (field.FieldType == typeof(bool))
        {
            field.SetValue(property, value >= 0.5f);
            return true;
        }

        if (field.FieldType == typeof(Color))
        {
            Color color = (Color)field.GetValue(property);
            switch ((channel ?? string.Empty).ToUpperInvariant())
            {
                case "R":
                    color.r = value;
                    break;
                case "G":
                    color.g = value;
                    break;
                case "B":
                    color.b = value;
                    break;
                case "A":
                    color.a = value;
                    break;
                default:
                    return false;
            }

            field.SetValue(property, color);
            return true;
        }

        return false;
    }

    private static string buildCustomCosmeticName(Upgrade cosmetic, int seed, List<CustomCosmeticOverride> overrides)
    {
        string baseName = SparrohPlugin.CleanRichText(cosmetic.GetInstanceName(seed));
        string label = overrides.Count == 1
            ? $"{getCustomCosmeticOverrideLabel(cosmetic, overrides[0])} {overrides[0].Value:0.###}"
            : $"{overrides.Count} cosmetic edits";
        return $"{baseName} [{label}]";
    }

    private static string getCustomCosmeticOverrideLabel(Upgrade cosmetic, CustomCosmeticOverride customOverride)
    {
        UpgradeProperty[] properties = getUpgradeProperties(cosmetic).ToArray();
        if (customOverride.PropertyIndex < 0 || customOverride.PropertyIndex >= properties.Length || properties[customOverride.PropertyIndex] == null)
        {
            return customOverride.FieldName;
        }

        string label = prettifyStatLabel(properties[customOverride.PropertyIndex].GetType().Name, customOverride.FieldName);
        if (!string.IsNullOrEmpty(customOverride.Channel))
        {
            label = $"{label} {customOverride.Channel}";
        }

        return label;
    }

    private static UpgradeProperty[] cloneUpgradeProperties(Upgrade upgrade)
    {
        return getUpgradeProperties(upgrade)
            .Select(property => cloneObject(property) as UpgradeProperty)
            .Where(property => property != null)
            .ToArray();
    }

    private static object cloneObject(object source)
    {
        if (source == null)
        {
            return null;
        }

        MethodInfo memberwiseClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        return memberwiseClone.Invoke(source, null);
    }

    private static Upgrade createRuntimeCustomUpgrade(Upgrade source, IUpgradable gear, UpgradeProperty[] properties, string customName)
    {
        var parameters = new PlayerData.CustomUpgradeParams();
        parameters.gear = gear;
        parameters.id = nextCustomUpgradeId++;
        parameters.name = customName;
        parameters.description = source.Description;
        parameters.rarity = source.Rarity;
        parameters.icon = source.Icon;
        parameters.priority = source.Priority;
        parameters.flags = source.Flags;
        parameters.mustBeUnlockedFirst = source.MustBeUnlockedFirst;
        parameters.useDefaultUnlockCost = false;
        parameters.additionalUnlockCost = Array.Empty<ResourceCost>();
        parameters.revelantElement = source.EffectType;
        parameters.upgradeType = source.UpgradeType;

        return PlayerData.CreateUpgrade(SparrohPlugin.PluginGUID, parameters, properties);
    }

    private static Upgrade createRuntimeCustomUpgradeClone(Upgrade source, UpgradeProperty[] properties, string customName)
    {
        Upgrade clone = UnityEngine.Object.Instantiate(source);
        clone.SetProperties(properties);

        FieldInfo nameField = typeof(Upgrade).GetField("_name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        nameField?.SetValue(clone, customName);

        return clone;
    }

    private static bool tryCreateModifiedStatValue(object originalValue, Type valueType, float multiplier, bool forceMultiply, out object modifiedValue)
    {
        modifiedValue = originalValue;
        if (originalValue == null)
        {
            return false;
        }

        if (valueType.Name.StartsWith("OverrideData", StringComparison.Ordinal))
        {
            return tryCreateModifiedOverrideData(originalValue, multiplier, forceMultiply, out modifiedValue);
        }

        if (valueType.Name.StartsWith("Range", StringComparison.Ordinal))
        {
            return tryCreateModifiedRange(originalValue, multiplier, out modifiedValue);
        }

        if (valueType == typeof(float))
        {
            modifiedValue = multiplier;
            return true;
        }

        if (valueType == typeof(int))
        {
            modifiedValue = Mathf.RoundToInt(multiplier);
            return true;
        }

        if (valueType == typeof(Vector2))
        {
            modifiedValue = new Vector2(multiplier, multiplier);
            return true;
        }

        return false;
    }

    private static bool tryCreateModifiedOverrideData(object overrideData, float multiplier, bool forceMultiply, out object modifiedValue)
    {
        modifiedValue = overrideData;
        Type overrideType = overrideData.GetType();
        FieldInfo dataField = overrideType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo methodField = overrideType.GetField("method", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (dataField == null || methodField == null)
        {
            return false;
        }

        object data = dataField.GetValue(overrideData);
        if (!tryCreateModifiedStatValue(data, dataField.FieldType, multiplier, false, out object modifiedData))
        {
            return false;
        }

        dataField.SetValue(overrideData, modifiedData);
        if (forceMultiply)
        {
            methodField.SetValue(overrideData, OverrideType.Multiply);
        }

        modifiedValue = overrideData;
        return true;
    }

    private static bool tryCreateModifiedRange(object range, float multiplier, out object modifiedValue)
    {
        modifiedValue = range;
        Type rangeType = range.GetType();
        FieldInfo minField = rangeType.GetField("min", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo maxField = rangeType.GetField("max", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (minField == null || maxField == null)
        {
            return false;
        }

        if (!tryCreateLeafValue(minField.FieldType, multiplier, out object minValue) ||
            !tryCreateLeafValue(maxField.FieldType, multiplier, out object maxValue))
        {
            return false;
        }

        minField.SetValue(range, minValue);
        maxField.SetValue(range, maxValue);
        modifiedValue = range;
        return true;
    }

    private static bool tryCreateLeafValue(Type type, float multiplier, out object value)
    {
        value = null;

        if (type == typeof(float))
        {
            value = multiplier;
            return true;
        }

        if (type == typeof(int))
        {
            value = Mathf.RoundToInt(multiplier);
            return true;
        }

        if (type == typeof(Vector2))
        {
            value = new Vector2(multiplier, multiplier);
            return true;
        }

        return false;
    }

    private static string prettifyStatLabel(string propertyTypeName, string fieldName)
    {
        string label = Regex.Replace(propertyTypeName, "^UpgradeProperty_?", string.Empty);
        label = Regex.Replace(label, "([a-z])([A-Z])", "$1 $2");
        string field = Regex.Replace(fieldName, "([a-z])([A-Z])", "$1 $2");
        return $"{label}: {field}".Replace("_", " ");
    }

    private static IEnumerable<UpgradeProperty> getUpgradeProperties(Upgrade upgrade)
    {
        FieldInfo propertyListField = getPropertyListField(upgrade.GetType());
        object propertyList = propertyListField != null ? propertyListField.GetValue(upgrade) : upgrade.Properties;
        return upgradePropertiesField?.GetValue(propertyList) as UpgradeProperty[] ?? Enumerable.Empty<UpgradeProperty>();
    }

    private static FieldInfo getPropertyListField(Type type)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(f => f.FieldType == typeof(UpgradePropertyList));
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static IEnumerable<FieldInfo> getFields(Type type)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static object createFireIntervalOverride(object overrideData, float multiplier)
    {
        Type overrideType = overrideData.GetType();
        FieldInfo dataField = overrideType.GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo methodField = overrideType.GetField("method", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        object range = dataField.GetValue(overrideData);
        Type rangeType = range.GetType();
        FieldInfo minField = rangeType.GetField("min", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo maxField = rangeType.GetField("max", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        minField.SetValue(range, multiplier);
        maxField.SetValue(range, multiplier);
        dataField.SetValue(overrideData, range);
        methodField.SetValue(overrideData, OverrideType.Multiply);

        return overrideData;
    }
}
