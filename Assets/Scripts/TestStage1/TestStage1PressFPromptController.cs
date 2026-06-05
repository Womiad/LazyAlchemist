using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1PressFPromptController : MonoBehaviour
{
    private const string PressFName = "PressF";

    private static GameObject prompt;
    private static bool requestedVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        prompt = null;
        requestedVisible = false;
    }

    public static void RequestVisible()
    {
        requestedVisible = true;
    }

    private void LateUpdate()
    {
        EnsurePrompt();

        if (prompt != null && prompt.activeSelf != requestedVisible)
        {
            prompt.SetActive(requestedVisible);
        }

        requestedVisible = false;
    }

    private static void EnsurePrompt()
    {
        if (prompt != null)
        {
            return;
        }

        prompt = FindObjectInActiveScene(PressFName);
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
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
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
