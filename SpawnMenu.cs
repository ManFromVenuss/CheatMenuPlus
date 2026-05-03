using System;
using BepInEx.Logging;
using System.Linq;
using Pigeon.Movement;
using UnityEngine;

public static class SpawnMenu
{
    public static void CreateSpawnMenu(MenuMod2Menu parentMenu)
    {
        MenuMod2Menu spawnMenu = new MenuMod2Menu("SPAWN", parentMenu);

        MenuMod2Menu enemySpawnMenu = new MenuMod2Menu("Enemy", spawnMenu);

        MenuMod2Menu spawnBossMenu = new MenuMod2Menu("BOSSES", enemySpawnMenu);
        spawnBossMenu.addButton("Spawn Amalgamation",
            () => spawnEnemy(
                Global.Instance.EnemyClasses.FirstOrDefault(x => x.APIName == "amalgamation")));
        spawnBossMenu.addButton("Spawn Cranius",
            () => spawnEnemy(
                Global.Instance.EnemyClasses.FirstOrDefault(x => x.APIName == "cranius")));

        MenuMod2Menu standardEnemyMenu = new MenuMod2Menu("STANDARD", enemySpawnMenu);
        foreach (var enemyClass in Global.Instance.EnemyClasses)
        {
            if (enemyClass != null && !string.IsNullOrEmpty(enemyClass.Name))
            {
                standardEnemyMenu.addButton($"Spawn {enemyClass.Name}",
                    () => spawnEnemy(enemyClass));
            }
        }

        MenuMod2Menu vehicleSpawnMenu = new MenuMod2Menu("Vehicle", spawnMenu);
        vehicleSpawnMenu.addButton("Spawn Dart", () => spawnObject("Kart"));
        vehicleSpawnMenu.addButton("Spawn WheelBox", () => spawnObject("WheelBox"));

        MenuMod2Menu objectSpawnMenu = new MenuMod2Menu("Object", spawnMenu);
        objectSpawnMenu.addButton("Saxitos", () => spawnObject("SaxitosBag"));
        objectSpawnMenu.addButton("Radio", () => spawnObject("Jukebox"));
        objectSpawnMenu.addButton("Barrel", () => spawnObject("HoldableBarrel"));
        objectSpawnMenu.addButton("Dummy", () => spawnObject("TrainingDummy"));
        objectSpawnMenu.addButton("Box", () => spawnObject("NoteBlock"));
        objectSpawnMenu.addButton("Bomb", () => spawnObject("ExplosiveHotPotato"));
        objectSpawnMenu.addButton("Bear", () => spawnObject("BearPhys"));
        objectSpawnMenu.addButton("Toilet Paper", () => spawnObject("TP"));
        objectSpawnMenu.addButton("Milk", () => spawnObject("MilkJug"));
    }

    public static void spawnObject(string name)
    {
        try
        {
            Vector3 position = Player.LocalPlayer.gameObject.transform.position;
            Vector3 lookDirection = Player.LocalPlayer.PlayerLook.Camera.transform.forward;
            if (Physics.Raycast(position, lookDirection, out RaycastHit hit, 100000))
            {
                GameObject gobject = SparrohPlugin.findObjectByName(name);
                if (gobject == null)
                {
                    return;
                }
                GameObject spawned = UnityEngine.Object.Instantiate(gobject);
                spawned.transform.position = hit.point;
                if (!FindValidSpawnPos(ref spawned))
                {
                    SparrohPlugin.SendTextChatMessageToClient("Could not find valid spawn position in look direction.");
                    return;
                }
                spawned.GetComponent<Unity.Netcode.NetworkObject>()?.Spawn(true);
            }
            else
            {
                SparrohPlugin.SendTextChatMessageToClient("Invalid look position.");
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnObject: {ex.Message}");
        }
    }

    public static bool FindValidSpawnPos(ref GameObject obj)
    {
        try
        {
            Vector3 startPos = obj.transform.position;
            if (obj == null)
                return false;

            obj.SetActive(false);

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                UnityEngine.Object.Destroy(obj);
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            Vector3 halfExtents = bounds.extents;
            Quaternion rotation = obj.transform.rotation;

            Vector3 offset = bounds.center - obj.transform.position;
            Vector3 checkPosition = startPos;

            const int maxAttempts = 100;
            const float step = 1f;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 testCenter = checkPosition + offset;
                if (!Physics.CheckBox(testCenter, halfExtents, rotation))
                {
                    obj.transform.position = checkPosition;
                    obj.SetActive(true);
                    return true;
                }

                checkPosition += Vector3.up * step;
            }

            UnityEngine.Object.Destroy(obj);
            return false;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in FindValidSpawnPos: {ex.Message}");
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
            return false;
        }
    }

    public static void spawnSwarm(int size)
    {
        try
        {
            var em = SparrohPlugin.GetEnemyManager();
            if (em != null)
                em.SpawnSwarm_ServerRpc(size);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnSwarm: {ex.Message}");
        }
    }

    public static void spawnEnemy(EnemyClass enemyClass, Vector3 pos = default)
    {
        try
        {
            var em = SparrohPlugin.GetEnemyManager();
            if (em != null)
            {
                if (pos == default)
                    em.SpawnEnemy_Server(enemyClass);
                else
                    em.SpawnEnemy_Server(pos, enemyClass);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnEnemy (EnemyClass): {ex.Message}");
        }
    }

    public static void spawnEnemy(EnemyClassGroup enemyClassGroup, Vector3 pos = default)
    {
        try
        {
            var em = SparrohPlugin.GetEnemyManager();
            if (em != null)
            {
                if (pos == default)
                    em.SpawnEnemy_Server(enemyClassGroup);
                else
                    em.SpawnEnemy_Server(pos, enemyClassGroup);
            }
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Exception in spawnEnemy (EnemyClassGroup): {ex.Message}");
        }
    }
}
