using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestStage1DebugMode
{
    private const string ResourceName = "TestStage1DebugSettings";
    private const string ConnectionSceneName = "ConnectionScene";

    private static TestStage1DebugSettings settings;
    private static bool hasAutoLaunched;

    public static TestStage1DebugRole Role => Settings == null
        ? TestStage1DebugRole.Off
        : Settings.role;

    public static bool IsEnabled => Role != TestStage1DebugRole.Off;
    public static bool IsHost => IsEnabled
        ? Role == TestStage1DebugRole.Host
        : IsOfflineStage
            || NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    public static bool IsClientOnly => IsEnabled
        ? Role == TestStage1DebugRole.Client
        : NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsClient
            && !NetworkManager.Singleton.IsHost;
    public static bool IsServer => IsEnabled
        ? Role == TestStage1DebugRole.Host
        : IsOfflineStage
            || NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    public static bool IsClient => IsEnabled
        ? true
        : NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;

    private static TestStage1DebugSettings Settings
    {
        get
        {
            if (settings == null)
            {
                settings = Resources.Load<TestStage1DebugSettings>(ResourceName);
            }

            return settings;
        }
    }

    private static bool IsOfflineStage
    {
        get
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var isStage = sceneName == "TestStage1" || sceneName == "TestStage1_VR";
            return isStage
                && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        settings = null;
        hasAutoLaunched = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAutoLaunch(SceneManager.GetActiveScene());

        if (IsEnabled)
        {
            Debug.Log($"TestStage1 single-player debug mode: {Role}");
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoLaunch(scene);
    }

    private static void TryAutoLaunch(Scene scene)
    {
        if (!IsEnabled
            || hasAutoLaunched
            || scene.name != ConnectionSceneName
            || Settings == null
            || string.IsNullOrWhiteSpace(Settings.stageScene))
        {
            return;
        }

        hasAutoLaunched = true;
        SceneManager.LoadScene(Settings.stageScene, LoadSceneMode.Single);
    }
}

public sealed class TestStage1DebugHostControls : MonoBehaviour
{
    private void Start()
    {
        if (TestStage1DebugMode.IsHost)
        {
            Debug.Log(
                "Host debug shortcuts: 1=Empty, 2=Pickaxe, 3=Staff, " +
                "F1=None, F2=Fire, F3=Land, F4=Wind, F5=Water, B=Slime bait.");
        }
    }

    private void Update()
    {
        if (!TestStage1DebugMode.IsHost)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestStage1Tool3DController.RequestHostEmptyTool();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestStage1Tool3DController.RequestHostTool("pickaxe");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestStage1Tool3DController.RequestHostTool("staff");
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            TestStage1UI.RequestHostPresetColor(TestStage1UI.EmptyElementColorIndex);
        }
        else if (Input.GetKeyDown(KeyCode.F2))
        {
            TestStage1UI.RequestHostPresetColor(0);
        }
        else if (Input.GetKeyDown(KeyCode.F3))
        {
            TestStage1UI.RequestHostPresetColor(1);
        }
        else if (Input.GetKeyDown(KeyCode.F4))
        {
            TestStage1UI.RequestHostPresetColor(2);
        }
        else if (Input.GetKeyDown(KeyCode.F5))
        {
            TestStage1UI.RequestHostPresetColor(3);
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            TestStage1HandStateSync.RequestHostLeftHandSlimeBait(
                !TestStage1HandState.HasSlimeBaitInLeftHand);
        }
    }
}
