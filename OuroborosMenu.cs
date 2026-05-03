using System;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Pigeon.Movement;
using UnityEngine;

public static class OuroborosMenu
{
    public static void CreateOuroborosMenu(MenuMod2Menu parentMenu)
    {
        try
        {
            const string debugPattern =
                @"(_test_|_dev_|_wip|debug|temp|placeholder|todo|_old|_backup|_copy|\.skinasset$|^test_|roachard)";
            MenuMod2Menu ouroborosMenu = new MenuMod2Menu("OUROBOROS", parentMenu);

            var gearByType = Global.Instance.AllGear
                .Where(g => g.Info.Upgrades.Count > 0)
                .GroupBy(g => g.GearType)
                .OrderBy(g => g.Key.ToString());

            foreach (var gearGroup in gearByType)
            {
                var typeName = gearGroup.Key.ToString();
                var gearTypeMenu = new MenuMod2Menu("Ouro " + typeName, ouroborosMenu);

                foreach (var gear in gearGroup.OrderBy(g => g.Info.Name))
                {
                    var gearInfo = gear.Info;
                    var individualGearMenu = new MenuMod2Menu("Ouro " + gearInfo.Name, gearTypeMenu);

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

            MenuMod2Menu charactersMenu = new MenuMod2Menu("Ouro Characters", ouroborosMenu);
            foreach (var character in Global.Instance.Characters)
            {
                var gearInfo = character.Info;
                var individualCharMenu = new MenuMod2Menu("Ouro " + gearInfo.Name, charactersMenu);

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

            MenuMod2Menu specificGeneric = new MenuMod2Menu("Ouro Universal", charactersMenu);

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
                SparrohPlugin.Logger.LogError($"Exception in ouroboros universal upgrade menu creation: {ex.Message}\n{ex.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateOuroborosMenu: {ex.Message}");
        }
    }

    public static void giveCosmetic(Upgrade upgrade, IUpgradable gear, MM2Button b = null)
    {
        try
        {
            var iUpgrade = new UpgradeInstance(upgrade, gear);
            PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
            SetRented(iUpgrade);
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
            SetRented(iUpgrade);
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
            SetRented(upgradeInstance);
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
            SetRented(upgradeInstance);
            upgradeInstance.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            upgradeInstance.Unlock(true);
            ((PlayerUpgrade)upgrade).Apply(Player.LocalPlayer, upgradeInstance);
            SparrohPlugin.SendTextChatMessageToClient("Ouroboros universal upgrade added silently.");
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in giveUniversalUpgrade: {ex.Message}");
        }
    }

    private static void SetRented(UpgradeInstance upgradeInstance)
    {
        try
        {
            PropertyInfo removeProp = typeof(UpgradeInstance).GetProperty("RemoveAfterMission", BindingFlags.Public | BindingFlags.Instance);
            if (removeProp != null)
            {
                removeProp.SetValue(upgradeInstance, true);
                SparrohPlugin.Logger.LogInfo($"Set RemoveAfterMission to true for {upgradeInstance.Upgrade.Name}");
            }
            else
            {
                SparrohPlugin.Logger.LogWarning("RemoveAfterMission property not found");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Failed to set RemoveAfterMission: {ex.Message}");
        }
        PlayerData.Instance.rentedUpgrades.Add(upgradeInstance);
        SparrohPlugin.Logger.LogInfo($"Added to rentedUpgrades. Count: {PlayerData.Instance.rentedUpgrades.Count}");
    }
}
