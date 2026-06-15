using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestStage1Bootstrap
{
    private const string SceneName = "TestStage1";
    private const string ManagerName = "TestStage1Manager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureManagerForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName)
        {
            EnsureManagerForActiveScene();
            return;
        }

        ResetRuntimeState();
    }

    private static void EnsureManagerForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
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
}
