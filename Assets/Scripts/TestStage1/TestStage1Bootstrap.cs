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
        }
    }

    private static void EnsureManagerForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != SceneName || GameObject.Find(ManagerName) != null)
        {
            return;
        }

        var managerObject = new GameObject(ManagerName);
        managerObject.AddComponent<TestStage1HostBetaJoints>();
        managerObject.AddComponent<TestStage1UI>();
    }
}
