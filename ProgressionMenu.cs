using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

public static class ProgressionMenu
{
    public static void CreateProgressionMenu(MenuMod2Menu parentMenu)
    {
        try
        {
            MenuMod2Menu progressionMenu = new MenuMod2Menu("PROGRESSION", parentMenu);
            progressionMenu.addButton("Unlock everything", () => unlockEverything());
            progressionMenu.addButton("Give missing upgrades", () => giveMissingUpgrades());
            progressionMenu.addButton("Unlock locked skills", () => unlockLockedSkills());

            MenuMod2Menu unlockWeapon = new MenuMod2Menu("Unlock weapon", progressionMenu);
            MenuMod2Menu levelup = new MenuMod2Menu("Levels", progressionMenu);

            var allGearUnlock = Global.Instance.AllGear;
            foreach (var gear in allGearUnlock)
            {
                var gearInfo = gear.Info;
                Color color = Color.red;
                if (PlayerData.GetGearData(gear).IsUnlocked)
                    color = Color.green;
                else if (PlayerData.GetGearData(gear).IsCollected)
                    color = Color.yellow;
                {
                    MM2Button button = null;
                    button = unlockWeapon.addButton(gearInfo.Name, () => unlockGear(gear, button))
                        .changeColour(color);
                }
            }

            if (SparrohPlugin.sparrohMode)
            {
                MenuMod2Menu levelCharactersMenu = new MenuMod2Menu("Character Levels", levelup);

                var gearByTypeForLevels = Global.Instance.AllGear
                    .GroupBy(g => g.GearType)
                    .OrderBy(g => g.Key.ToString());

                foreach (var gearGroup in gearByTypeForLevels)
                {
                    var typeName = gearGroup.Key.ToString() + " Levels";
                    var gearTypeLevelMenu = new MenuMod2Menu(typeName, levelup);

                    foreach (var gear in gearGroup.OrderBy(g => g.Info.Name))
                    {
                        var gearMenu = new MenuMod2Menu($"{gear.Info.Name} (Level {getGearLevel(gear)})", gearTypeLevelMenu);
                        gearMenu.addButton("Level +1", () => levelUpGearRelative(gear, 1, gearMenu));
                        gearMenu.addButton("Level -1", () => levelUpGearRelative(gear, -1, gearMenu));
                    }
                }
                foreach (var character in Global.Instance.Characters)
                {
                    var charMenu = new MenuMod2Menu($"{character.Info.Name} (Level {getGearLevel(character)})", levelCharactersMenu);
                    charMenu.addButton("Level +1", () => levelUpGearRelative(character, 1, charMenu));
                    charMenu.addButton("Level -1", () => levelUpGearRelative(character, -1, charMenu));
                }
            }
            else
            {
                var gearByTypeForLevels = Global.Instance.AllGear
                    .GroupBy(g => g.GearType)
                    .OrderBy(g => g.Key.ToString());

                foreach (var gearGroup in gearByTypeForLevels)
                {
                    var typeName = gearGroup.Key.ToString();
                    levelup.addButton($"Set {typeName} Levels to 30", () => setAllGearInTypeToLevel(gearGroup.ToList(), 30));
                }
                levelup.addButton("Set Character Levels to 30", () => setAllCharictersToLevel(30));
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateProgressionMenu: {ex.Message}");
        }
    }

    public static void unlockEverything(MM2Button b = null)
    {
        try
        {
            if (Global.Instance == null || PlayerData.Instance == null)
            {
                return;
            }

            CheatsMenu.giveAllResources();

            foreach (var gear in Global.Instance.AllGear)
            {
                PlayerData.CollectGear(gear);
                PlayerData.UnlockGear(gear);
                var gearData = PlayerData.GetGearData(gear);
                gearData.Collect();
                gearData.Unlock();
                SetGearLevel(gear, 30);
            }

            foreach (var character in Global.Instance.Characters)
            {
                PlayerData.CollectGear(character);
                PlayerData.UnlockGear(character);
                var characterData = PlayerData.GetGearData(character);
                characterData.Collect();
                characterData.Unlock();
                SetGearLevel(character, 30);
            }

            UpgradesMenu.giveAllUpgrades();
            UpgradesMenu.giveAllCosmetics();
            unlockLockedSkills();

            b?.changeColour(MenuStyle.Success);
            SparrohPlugin.SendTextChatMessageToClient("Everything has been unlocked.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in unlockEverything: {ex.Message}");
        }
    }

    public static void giveMissingUpgrades(MM2Button b = null)
    {
        try
        {
            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    if ((upgrade.UpgradeType == Upgrade.Type.Invalid || upgrade.UpgradeType == Upgrade.Type.Cosmetic) || (PlayerData.GetUnlockedInstances(upgrade) != null && PlayerData.GetUnlockedInstances(upgrade).Instances != null && PlayerData.GetUnlockedInstances(upgrade).Instances.Count > 0))
                    {
                        continue;
                    }
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }
            SparrohPlugin.SendTextChatMessageToClient("All missing upgrades are added silently.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveMissingUpgrades: {ex.Message}");
        }
    }

    public static void collectGear(IUpgradable gear, MM2Button b = null)
    {
        try
        {
            PlayerData.CollectGear(gear);
            PlayerData.GetGearData(gear).Collect();
            if (b != null)
            {
                b.changeColour(Color.yellow);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in collectGear: {ex.Message}");
        }
    }

    public static void unlockGear(IUpgradable gear, MM2Button b = null)
    {
        try
        {
            PlayerData.UnlockGear(gear);
            PlayerData.GetGearData(gear).Unlock();
            if (b != null)
            {
                b.changeColour(Color.green);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in unlockGear: {ex.Message}");
        }
    }

    public static void levelUpAllWeapons(int level, MM2Button b = null)
    {
        try
        {
            var allGear = Global.Instance.AllGear;
            foreach (var gear in allGear)
            {
                levelUpWeapon(gear, level);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in levelUpAllWeapons: {ex.Message}");
        }
    }

    public static void levelUpWeapon(IUpgradable gear, int level, MM2Button b = null)
    {
        try
        {
            SetGearLevel(gear, level);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in levelUpWeapon: {ex.Message}");
        }
    }

    public static void levelUpGearRelative(IUpgradable gear, int delta, MenuMod2Menu menu = null, MM2Button b = null)
    {
        try
        {
            var gearData = PlayerData.GetGearData(gear);
            if (gearData.IsUnlocked)
            {
                var levelField = gearData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                int currentLevel = (int)levelField.GetValue(gearData);
                int newLevel = Math.Max(0, currentLevel + delta);
                levelField.SetValue(gearData, newLevel);
                if (menu != null)
                {
                    string newName = $"{gear.Info.Name} (Level {newLevel})";
                    menu.thisButton.changeName(newName);
                    menu.menuName = newName;
                    var backButton = menu.buttons.FirstOrDefault(btn => btn.name == "Back");
                    if (backButton != null)
                    {
                        backButton.changePrefix($"[{menu.menuName}]\n");
                    }
                }
                if (b != null)
                {
                    b.updateText();
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in levelUpGearRelative: {ex.Message}");
        }
    }

    public static int getGearLevel(IUpgradable gear)
    {
        try
        {
            var gearData = PlayerData.GetGearData(gear);
            if (gearData.IsUnlocked)
            {
                var levelField = gearData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                return (int)levelField.GetValue(gearData);
            }
            return 0;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in getGearLevel: {ex.Message}");
            return 0;
        }
    }

    public static void setAllCharictersToLevel(int level, MM2Button b = null)
    {
        try
        {
            var allCharicters = Global.Instance.Characters;
            foreach (var employee in allCharicters)
            {
                var employeeData = PlayerData.GetGearData(employee);
                if (employeeData.IsUnlocked)
                {
                    var levelField = employeeData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                    int currentLevel = (int)levelField.GetValue(employeeData);
                    levelField.SetValue(employeeData, Math.Max(level, currentLevel));
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in setAllCharictersToLevel: {ex.Message}");
        }
    }

    public static void setAllGearInTypeToLevel(IEnumerable<IUpgradable> gearList, int level, MM2Button b = null)
    {
        try
        {
            foreach (var gear in gearList)
            {
                var gearData = PlayerData.GetGearData(gear);
                if (gearData.IsUnlocked)
                {
                    var levelField = gearData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                    levelField.SetValue(gearData, level);
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in setAllGearInTypeToLevel: {ex.Message}");
        }
    }

    private static void SetGearLevel(IUpgradable gear, int level)
    {
        var gearData = PlayerData.GetGearData(gear);
        if (gearData == null || !gearData.IsUnlocked)
        {
            return;
        }

        var levelField = gearData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
        if (levelField == null)
        {
            return;
        }

        int currentLevel = (int)levelField.GetValue(gearData);
        levelField.SetValue(gearData, Math.Max(level, currentLevel));
    }

    public static void giveAllSkills(MM2Button b = null)
    {
        try
        {
            var allCharicters = Global.Instance.Characters;
            foreach (var employee in allCharicters)
            {
                var skillTree = employee.SkillTree;
                SkillTreeUpgradeUI[] upgrades = skillTree.GetComponentsInChildren<SkillTreeUpgradeUI>();
                foreach (var upgrade in upgrades)
                {
                    UpgradeInstance upgradeInstance = PlayerData.CollectInstance(employee, upgrade.Upgrade, PlayerData.UnlockFlags.None);
                    upgradeInstance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    upgradeInstance.Unlock(false);
                    PlayerData instance = PlayerData.Instance;
                    int totalSkillPointsSpent = instance.TotalSkillPointsSpent;
                    instance.TotalSkillPointsSpent = totalSkillPointsSpent + 1;
                    skillTree.Refresh();
                }

            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveAllSkills: {ex.Message}");
        }
    }

    public static void unlockLockedSkills(MM2Button b = null)
    {
        try
        {
            var allCharicters = Global.Instance.Characters;
            foreach (var employee in allCharicters)
            {
                var skillTree = employee.SkillTree;
                SkillTreeUpgradeUI[] upgrades = skillTree.GetComponentsInChildren<SkillTreeUpgradeUI>();
                foreach (var upgrade in upgrades)
                {
                    if (PlayerData.GetUpgradeInfo(employee, upgrade.Upgrade).TotalInstancesCollected > 0)
                    {
                        continue;
                    }
                    UpgradeInstance upgradeInstance = PlayerData.CollectInstance(employee, upgrade.Upgrade, PlayerData.UnlockFlags.None);
                    upgradeInstance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    upgradeInstance.Unlock(false);
                    PlayerData instance = PlayerData.Instance;
                    int totalSkillPointsSpent = instance.TotalSkillPointsSpent;
                    instance.TotalSkillPointsSpent = totalSkillPointsSpent + 1;
                    skillTree.Refresh();
                }

            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in unlockLockedSkills: {ex.Message}");
        }
    }
}
