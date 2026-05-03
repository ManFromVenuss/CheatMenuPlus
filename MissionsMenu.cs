using System;
using BepInEx.Logging;
using System.Reflection;
using Pigeon;
using UnityEngine;

public static class MissionsMenu
{
    public static void CreateMissionsMenu(MenuMod2Menu parentMenu)
    {
        try
        {
            MenuMod2Menu missionMenu = new MenuMod2Menu("MISSIONS", parentMenu);

            MenuMod2Menu forceModifierMenu = new MenuMod2Menu("Force Modifier", missionMenu);
            FieldInfo itemsField =
                typeof(WeightedArray<MissionModifier>).GetField("items",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var items = itemsField.GetValue(Global.Instance.MissionModifiers) as Array;
            foreach (var node in items)
            {
                var valueField = node.GetType().GetField("value");
                var modifier = (MissionModifier)valueField.GetValue(node);
                {
                    MM2Button button = null;
                    button = forceModifierMenu.addButton($"{modifier.ModifierName}",
                        () => toggleForceModifier(modifier, button));
                }
            }

            MenuMod2Menu loadMissionMenu = new MenuMod2Menu("Load mission", missionMenu);
            System.Collections.Generic.List<MenuMod2Menu> missionMenus = new System.Collections.Generic.List<MenuMod2Menu>();

            System.Collections.Generic.List<string> specialMissions = new System.Collections.Generic.List<string>
            {
                "Amalgamation Hunt",
                "Ouroboros",
                "Oxythane Breach"
            };

            foreach (var mission in Global.Instance.Missions)
            {
                bool isNormalMission = mission.MissionFlags.HasFlag(MissionFlags.NormalMission) && mission.CanBeSelected();
                bool isSpecialMission = specialMissions.Contains(mission.MissionName);

                if (isNormalMission || isSpecialMission)
                {
                    var thisMenu = new MenuMod2Menu(mission.MissionName, loadMissionMenu);
                    missionMenus.Add(thisMenu);
                    var regions = Global.Instance.Regions;

                    if (isNormalMission)
                    {
                        foreach (var region in regions)
                        {
                            thisMenu.addButton($"{region.RegionName}", () => loadMission(mission, region));
                        }
                    }
                    else if (isSpecialMission)
                    {
                        var defaultRegion = regions.Length > 0 ? regions[0] : null;
                        if (defaultRegion != null)
                        {
                            thisMenu.addButton($"{defaultRegion.RegionName}", () => loadMission(mission, defaultRegion));
                        }
                        else
                        {
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in CreateMissionsMenu: {ex.Message}");
        }
    }

    public static void toggleForceModifier(MissionModifier modifier, MM2Button b = null)
    {
        try
        {
            if (SparrohPlugin.forcedModifiers.Contains(modifier))
            {
                SparrohPlugin.forcedModifiers.Remove(modifier);
                if (b != null)
                {
                    b.changePrefix("");
                    b.changeColour(Color.white);
                }
            }
            else
            {
                SparrohPlugin.forcedModifiers.Add(modifier);
                if (b != null)
                {
                    b.changePrefix("* ");
                    b.changeColour(Color.green);
                }
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in toggleForceModifier: {ex.Message}");
        }
    }

    public static void loadMission(Mission mission, WorldRegion region, MM2Button b = null)
    {
        try
        {
            SceneData sceneData = region.Scenes != null && region.Scenes.Length > 0 ? region.Scenes[0] : default(SceneData);
            MissionData missionData = new MissionData(MissionManager.MissionSeed, mission, region, sceneData, Global.Instance.DefaultMissionContainer, mission.GetAdditionalData());

            if (SparrohPlugin.forcedModifiers != null && SparrohPlugin.forcedModifiers.Count > 0)
            {
                foreach (var modifier in SparrohPlugin.forcedModifiers)
                {
                    var index = Global.Instance.MissionModifiers.IndexOf(modifier);
                    missionData.AddModifier(index, dontDuplicate: true, addFirst: true);
                }
            }

            DropPod.SetMission(missionData);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in loadMission: {ex.Message}");
        }
    }
}
