using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Activates resources offered by any of the three quests and distributes them among authored spawn points.</summary>
public class TestStage1ResourceSpawner : MonoBehaviour
{
    private readonly List<Transform> fireflyPoints = new List<Transform>();
    private readonly List<Pose> fireflySpawnPoses = new List<Pose>();
    private readonly List<Transform> mineralPoints = new List<Transform>();
    private readonly List<GameObject> fireflies = new List<GameObject>();
    private readonly List<GameObject> mineralSources = new List<GameObject>();
    private readonly List<GameObject> spawnedMinerals = new List<GameObject>();
    private GameObject activeFirefly;
    private TestStage1QuestKind lastKind = (TestStage1QuestKind)255;
    private TestStage1QuestElement lastElement = (TestStage1QuestElement)255;
    private float nextMineralMaintenanceTime;
    private bool initialized;

    private void OnEnable() { TestStage1QuestSystem.QuestChanged += RefreshForQuest; }
    private void OnDisable() { TestStage1QuestSystem.QuestChanged -= RefreshForQuest; }
    private void Update()
    {
        if (!initialized) Initialize();
        if (!initialized) return;
        if (lastKind != TestStage1QuestSystem.CurrentKind || lastElement != TestStage1QuestSystem.CurrentElement) RefreshForQuest();
        if (activeFirefly != null && TestStage1QuestSystem.IsQuest(TestStage1QuestKind.Firefly)) TryCollectFirefly();
        if (Time.time >= nextMineralMaintenanceTime)
        {
            nextMineralMaintenanceTime = Time.time + .25f;
            if (TestStage1QuestSystem.IsQuest(TestStage1QuestKind.Mining))
                EnsureMinerals(TestStage1QuestSystem.CurrentElement);
            else if (TestStage1QuestSystem.TryGetOfferedQuestElement(TestStage1QuestKind.Mining, out var offeredElement))
                EnsureMinerals(offeredElement);
        }
    }

    private void Initialize()
    {
        fireflyPoints.Clear(); mineralPoints.Clear(); fireflies.Clear(); mineralSources.Clear();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Collect(root.transform);
        // Existing resources double as authored points when explicit *SpawnPoint objects are absent.
        if (fireflyPoints.Count == 0) foreach (var item in fireflies) fireflyPoints.Add(item.transform);
        if (mineralPoints.Count == 0) foreach (var item in mineralSources) mineralPoints.Add(item.transform);
        fireflySpawnPoses.Clear();
        foreach (var point in fireflyPoints) fireflySpawnPoses.Add(new Pose(point.position, point.rotation));
        initialized = fireflies.Count > 0 && mineralSources.Count > 0;
        if (initialized) RefreshForQuest();
    }

    private void Collect(Transform node)
    {
        var lower = node.name.ToLowerInvariant();
        if (lower.Contains("fireflyspawnpoint") || lower.Contains("firefly_spawn")) fireflyPoints.Add(node);
        else if (lower.Contains("mineralspawnpoint") || lower.Contains("crystalspawnpoint") || lower.Contains("crystal_spawn")) mineralPoints.Add(node);
        else if (!IsUi(node) && lower.Contains("firefly") && node.GetComponentInChildren<ParticleSystem>(true) != null) fireflies.Add(node.gameObject);
        else if (!IsUi(node) && (lower.StartsWith("crystal_") || lower.StartsWith("mineral_"))) mineralSources.Add(node.gameObject);
        for (var i = 0; i < node.childCount; i++) Collect(node.GetChild(i));
    }

    private void RefreshForQuest()
    {
        if (!initialized) return;
        lastKind = TestStage1QuestSystem.CurrentKind; lastElement = TestStage1QuestSystem.CurrentElement;
        foreach (var firefly in fireflies) if (firefly != null) firefly.SetActive(false);
        activeFirefly = null;
        PruneSpawnedMinerals();
        foreach (var mineral in mineralSources) if (mineral != null) mineral.SetActive(false);

        if (TestStage1QuestSystem.HasOfferedQuest(TestStage1QuestKind.Firefly)) SpawnFirefly();
        if (TestStage1QuestSystem.IsQuest(TestStage1QuestKind.Mining))
            EnsureMinerals(TestStage1QuestSystem.CurrentElement);
        else if (TestStage1QuestSystem.TryGetOfferedQuestElement(TestStage1QuestKind.Mining, out var miningElement))
            EnsureMinerals(miningElement);
    }

    private void SpawnFirefly()
    {
        if (fireflies.Count == 0 || fireflySpawnPoses.Count == 0) return;
        activeFirefly = fireflies[0];
        var point = fireflySpawnPoses[UnityEngine.Random.Range(0, fireflySpawnPoses.Count)];
        activeFirefly.transform.SetPositionAndRotation(point.position, point.rotation);
        activeFirefly.SetActive(true);
    }

    private void EnsureMinerals(TestStage1QuestElement targetElement)
    {
        if (mineralSources.Count == 0 || mineralPoints.Count == 0) return;
        PruneSpawnedMinerals();
        bool hasTarget = spawnedMinerals.Exists(item => item != null && item.activeInHierarchy
            && item.GetComponent<TestStage1MineableMineral>()?.Element == targetElement);
        if (!hasTarget && spawnedMinerals.Count >= 2)
        {
            var replaced = spawnedMinerals.Find(item => item != null
                && item.GetComponent<TestStage1MineableMineral>()?.Element != targetElement);
            if (replaced != null) { spawnedMinerals.Remove(replaced); Destroy(replaced); }
        }
        var points = new List<Transform>(mineralPoints);
        points.RemoveAll(point => spawnedMinerals.Exists(item => item != null && item.activeInHierarchy
            && Vector3.SqrMagnitude(item.transform.position - point.position) < .25f));
        Shuffle(points);
        var count = Mathf.Min(Mathf.Max(0, 2 - spawnedMinerals.Count), points.Count);
        for (var i = 0; i < count; i++)
        {
            GameObject source;
            TestStage1QuestElement spawnedElement;
            if (!hasTarget)
            {
                source = FindMineralSource(targetElement) ?? mineralSources[0];
                spawnedElement = targetElement;
                hasTarget = true;
            }
            else
            {
                source = mineralSources[UnityEngine.Random.Range(0, mineralSources.Count)];
                spawnedElement = GuessElement(source.name);
            }
            var instance = Instantiate(source, points[i].position, points[i].rotation, source.transform.parent);
            instance.name = source.name + "_Quest";
            instance.SetActive(true);
            var mineable = instance.GetComponent<TestStage1MineableMineral>() ?? instance.AddComponent<TestStage1MineableMineral>();
            mineable.Configure(spawnedElement);
            spawnedMinerals.Add(instance);
        }
    }

    private void TryCollectFirefly()
    {
        if (!IsAuthority()) return;
        if (TestStage1HandState.RightHandTool != TestStage1RightHandTool.None) return;
        var host = GameObject.Find("Host3D");
        var vrController = FindFirstObjectByType<TestStage1VRHostController>();
        Transform playerTransform = vrController != null ? vrController.transform : host?.transform;
        if (playerTransform == null || Vector3.Distance(playerTransform.position, activeFirefly.transform.position) > 3f) return;
        TestStage1PressFPromptController.RequestVisible();
        bool grabbedInVr = TestStage1VRHostController.TryConsumeGrabNear(activeFirefly.transform.position, .65f);
        var hostDistance = Vector3.Distance(playerTransform.position, activeFirefly.transform.position);
        bool controllerGrabbedNearby = hostDistance <= 2.25f && TestStage1VRInput.GrabPressedThisFrame;
        if (!Input.GetKeyDown(KeyCode.F) && !TestStage1VRInput.CollectPressedThisFrame && !grabbedInVr && !controllerGrabbedNearby) return;
        activeFirefly.SetActive(false); activeFirefly = null;
        TestStage1ItemInventoryController.SetFireflyAcquired(true);
        TestStage1QuestSystem.ReportFireflyCollected();
    }

    private GameObject FindMineralSource(TestStage1QuestElement element)
    {
        foreach (var source in mineralSources) if (GuessElement(source.name) == element) return source;
        return null;
    }
    private void PruneSpawnedMinerals() { spawnedMinerals.RemoveAll(item => item == null || !item.activeInHierarchy); }
    private static void Shuffle<T>(IList<T> list) { for (var i = list.Count - 1; i > 0; i--) { var j = UnityEngine.Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); } }
    private static bool IsUi(Transform node) { for (var p = node; p != null; p = p.parent) if (p.GetComponent<Canvas>() != null || p.name == "taskboard") return true; return false; }
    private static bool IsAuthority() { var nm = Unity.Netcode.NetworkManager.Singleton; return TestStage1DebugMode.IsEnabled ? TestStage1DebugMode.IsServer : nm == null || !nm.IsListening || nm.IsServer; }
    public static TestStage1QuestElement GuessElement(string value)
    {
        value = value.ToLowerInvariant();
        if (value.Contains("flame") || value.Contains("fire")) return TestStage1QuestElement.Fire;
        if (value.Contains("land") || value.Contains("earth")) return TestStage1QuestElement.Land;
        if (value.Contains("wind")) return TestStage1QuestElement.Wind;
        if (value.Contains("water")) return TestStage1QuestElement.Water;
        return TestStage1QuestElement.Any;
    }
}
