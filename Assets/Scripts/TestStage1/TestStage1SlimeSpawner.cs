using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1SlimeSpawner : MonoBehaviour
{
    private const string SpawnPointName = "SlimeSpawnPoint";
    private const string SlimesRootName = "Slimes";
    private const float DefaultSpawnInterval = 3f;

    [SerializeField] private float spawnInterval = DefaultSpawnInterval;
    [SerializeField] private int maxSpawnedSlimes = 10;
    [SerializeField] private bool deactivateSourceSlimes = true;

    private readonly List<Transform> slimeTemplates = new List<Transform>();
    private Transform spawnPoint;
    private Transform slimesRoot;
    private float nextSpawnTime;
    private bool initialized;

    private void Update()
    {
        if (!ShouldRunSpawner())
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (!initialized)
        {
            TryInitialize();
        }

        if (!initialized || !TestStage1HandState.HasSlimeBaitInLeftHand)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime || CountActiveSpawnedSlimes() >= maxSpawnedSlimes)
        {
            return;
        }

        SpawnRandomSlime();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private bool ShouldRunSpawner()
    {
        if (TestStage1DebugMode.IsEnabled)
        {
            return TestStage1DebugMode.IsServer;
        }

        return NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsListening
            || NetworkManager.Singleton.IsServer;
    }

    private void TryInitialize()
    {
        spawnPoint = FindObjectInActiveScene(SpawnPointName)?.transform;
        slimesRoot = FindObjectInActiveScene(SlimesRootName)?.transform;

        slimeTemplates.Clear();
        if (slimesRoot != null)
        {
            for (var i = 0; i < slimesRoot.childCount; i++)
            {
                var child = slimesRoot.GetChild(i);
                if (child.name.EndsWith("(Clone)", System.StringComparison.Ordinal))
                {
                    continue;
                }

                slimeTemplates.Add(child);
                if (deactivateSourceSlimes)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        initialized = spawnPoint != null && slimesRoot != null && slimeTemplates.Count > 0;
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnRandomSlime()
    {
        if (slimeTemplates.Count == 0 || spawnPoint == null || slimesRoot == null)
        {
            initialized = false;
            return;
        }

        var template = slimeTemplates[Random.Range(0, slimeTemplates.Count)];
        if (template == null)
        {
            slimeTemplates.Remove(template);
            initialized = false;
            return;
        }

        var instance = Instantiate(
            template.gameObject,
            spawnPoint.position,
            spawnPoint.rotation,
            slimesRoot
        );

        instance.name = template.name;
        instance.tag = "Untagged";
        instance.SetActive(true);
    }

    private int CountActiveSpawnedSlimes()
    {
        if (slimesRoot == null)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < slimesRoot.childCount; i++)
        {
            var child = slimesRoot.GetChild(i);
            if (slimeTemplates.Contains(child) || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static GameObject FindObjectInActiveScene(string objectName)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            var match = FindChildByName(rootObject.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (string.Equals(parent.name, objectName, System.StringComparison.Ordinal))
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var match = FindChildByName(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
