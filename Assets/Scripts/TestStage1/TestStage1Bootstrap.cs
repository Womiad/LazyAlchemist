using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Invector.vCharacterController;

public static class TestStage1Bootstrap
{
    private const string SceneName = "TestStage1";
    private const string VrSceneName = "TestStage1_VR";
    private const string ManagerName = "TestStage1Manager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureManagerForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsStageScene(scene.name))
        {
            EnsureManagerForActiveScene();
            return;
        }

        ResetRuntimeState();
    }

    private static void EnsureManagerForActiveScene()
    {
        var activeSceneName = SceneManager.GetActiveScene().name;
        if (!IsStageScene(activeSceneName))
        {
            return;
        }

        var managerObject = GameObject.Find(ManagerName);
        if (managerObject == null)
        {
            managerObject = new GameObject(ManagerName);
        }

        EnsureComponent<TestStage1HostBetaJoints>(managerObject);
        EnsureComponent<TestStage1ClientPresentation>(managerObject);
        EnsureComponent<TestStage1UI>(managerObject);
        EnsureComponent<TestStage1HandStateSync>(managerObject);
        EnsureComponent<TestStage1Tool3DController>(managerObject);
        if (Object.FindFirstObjectByType<TestStage1StaffMagicAttackController>() == null)
        {
            managerObject.AddComponent<TestStage1StaffMagicAttackController>();
        }

        EnsureComponent<TestStage1ItemInventoryController>(managerObject);
        EnsureComponent<TestStage1PressFPromptController>(managerObject);
        EnsureComponent<TestStage1FireflyCollectionController>(managerObject);
        EnsureComponent<TestStage1WaterCrystalCollectionController>(managerObject);
        EnsureComponent<TestStage1LandCrystalCollectionController>(managerObject);
        EnsureComponent<TestStage1BookUIController>(managerObject);
        EnsureComponent<TestStage1ElementDragController>(managerObject);
        EnsureComponent<TestStage1CraftingTableController>(managerObject);
        EnsureComponent<TestStage1SlimeSpawner>(managerObject);
        EnsureComponent<TestStage1SlimeHuntController>(managerObject);
        EnsureComponent<TestStage1CompletionController>(managerObject);

        if (TestStage1DebugMode.IsEnabled)
        {
            EnsureComponent<TestStage1DebugHostControls>(managerObject);
        }

        if (activeSceneName == VrSceneName)
        {
            EnsureComponent<TestStage1VRColliderOptimizer>(managerObject);
            ConfigureVrHost();
        }
    }

    public static void ResetRuntimeState()
    {
        TestStage1HandState.ResetRuntimeState();
        TestStage1ItemInventoryController.ResetRuntimeState();
        TestStage1FireflyCollectionController.ResetRuntimeState();
        TestStage1WaterCrystalCollectionController.ResetRuntimeState();
        TestStage1SlimeHuntController.ResetRuntimeState();
        TestStage1PressFPromptController.ResetRuntimeState();
    }

    private static void EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target.GetComponent<T>() == null)
        {
            target.AddComponent<T>();
        }
    }

    private static bool IsStageScene(string sceneName)
    {
        return sceneName == SceneName || sceneName == VrSceneName;
    }

    private static void ConfigureVrHost()
    {
        var hostRoot = GameObject.Find("Host3D");
        var player = hostRoot == null
            ? null
            : hostRoot.GetComponentInChildren<vThirdPersonController>(true);

        if (player == null)
        {
            Debug.LogWarning("TestStage1 VR could not find the Host3D player controller.");
            return;
        }

        var isHost = TestStage1DebugMode.IsHost;
        foreach (var input in player.GetComponents<vThirdPersonInput>())
        {
            if (input is not TestStage1VRHostController)
            {
                input.enabled = false;
            }
        }

        if (isHost && player.GetComponent<TestStage1VRHostController>() == null)
        {
            player.gameObject.AddComponent<TestStage1VRHostController>();
        }
    }
}
