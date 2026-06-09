using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1FireflyCollectionController : MonoBehaviour
{
    private const string CollectFireflyMessageName = "TestStage1.CollectFirefly";
    private const string HostRootName = "Host3D";
    private const string HostBodyName = "Beta_Joints";
    private const string PressFName = "PressF";
    private const string TaskBoardName = "taskboard";
    private const string Task1Name = "Task1";
    private const string Task1CompletedText = "V 收集螢火蟲";
    private const string Task1IncompleteText = "收集螢火蟲";
    private const float CollectDistance = 3f;

    public static event Action<bool> CompletedChanged;
    public static bool IsCompleted { get; private set; }

    private Transform hostRoot;
    private Transform hostBody;
    private GameObject sceneFirefly;
    private GameObject pressFPrompt;
    private TMP_Text task1Text;
    private bool initialized;
    private bool handlerRegistered;
    private bool fireflyCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsCompleted = false;
        CompletedChanged = null;
    }

    private void Update()
    {
        RegisterHandlerIfNeeded();

        if (!initialized)
        {
            TryInitialize();
        }

        RefreshPrompt();

        if (CanHostCollect() && Input.GetKeyDown(KeyCode.F))
        {
            CollectFirefly();
        }
    }

    private void OnDestroy()
    {
        UnregisterHandler();
    }

    private void TryInitialize()
    {
        hostRoot = FindObjectInActiveScene(HostRootName)?.transform;
        hostBody = hostRoot == null ? null : FindChildByName(hostRoot, HostBodyName);
        pressFPrompt = FindObjectInActiveScene(PressFName);
        sceneFirefly = FindSceneFirefly();
        task1Text = FindTask1Text();

        if (pressFPrompt != null)
        {
            pressFPrompt.SetActive(false);
        }

        if (task1Text != null)
        {
            task1Text.text = fireflyCollected ? Task1CompletedText : Task1IncompleteText;
        }

        if (hostRoot == null || pressFPrompt == null || sceneFirefly == null || task1Text == null)
        {
            return;
        }

        initialized = true;
        ApplyCollectedState(fireflyCollected);
    }

    private void RefreshPrompt()
    {
        if (CanHostCollect())
        {
            TestStage1PressFPromptController.RequestVisible();
        }
    }

    private bool CanHostCollect()
    {
        return initialized
            && !fireflyCollected
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsServer
            && TestStage1HandState.RightHandTool == TestStage1RightHandTool.None
            && sceneFirefly != null
            && sceneFirefly.activeInHierarchy
            && hostRoot != null
            && Vector3.Distance(GetHostWorldCenter(), GetWorldCenter(sceneFirefly)) <= CollectDistance;
    }

    private void CollectFirefly()
    {
        ApplyCollectedState(true);
        BroadcastFireflyCollected();
    }

    private void ApplyCollectedState(bool isCollected)
    {
        fireflyCollected = isCollected;
        SetCompletion(isCollected);
        TestStage1ItemInventoryController.SetFireflyAcquired(isCollected);

        if (sceneFirefly != null && sceneFirefly.activeSelf == isCollected)
        {
            sceneFirefly.SetActive(!isCollected);
        }

        if (pressFPrompt != null)
        {
            pressFPrompt.SetActive(false);
        }

        if (task1Text != null)
        {
            task1Text.text = isCollected ? Task1CompletedText : Task1IncompleteText;
        }
    }

    private static void SetCompletion(bool isCompleted)
    {
        if (IsCompleted == isCompleted)
        {
            return;
        }

        IsCompleted = isCompleted;
        CompletedChanged?.Invoke(IsCompleted);
    }

    private void BroadcastFireflyCollected()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                continue;
            }

            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(true);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                CollectFireflyMessageName,
                clientId,
                writer
            );
        }
    }

    private void RegisterHandlerIfNeeded()
    {
        if (handlerRegistered || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            CollectFireflyMessageName,
            OnCollectFireflyMessageReceived
        );
        handlerRegistered = true;
    }

    private void UnregisterHandler()
    {
        if (!handlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CollectFireflyMessageName);
        handlerRegistered = false;
    }

    private static void OnCollectFireflyMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool isCollected);
        var controller = FindFirstObjectByType<TestStage1FireflyCollectionController>();
        controller?.ApplyCollectedState(isCollected);
    }

    private static GameObject FindSceneFirefly()
    {
        var scene = SceneManager.GetActiveScene();
        var candidates = new List<GameObject>();

        foreach (var rootObject in scene.GetRootGameObjects())
        {
            CollectFireflyCandidates(rootObject.transform, candidates);
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    private static void CollectFireflyCandidates(Transform parent, List<GameObject> results)
    {
        if (parent.name.IndexOf("firefly", StringComparison.OrdinalIgnoreCase) >= 0
            && parent.GetComponent<RectTransform>() == null
            && parent.GetComponentInChildren<ParticleSystem>(true) != null
            && !IsUnderClientUi(parent)
            && !IsUnderTaskBoard(parent))
        {
            results.Add(parent.gameObject);
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            CollectFireflyCandidates(parent.GetChild(i), results);
        }
    }

    private static TMP_Text FindTask1Text()
    {
        var taskBoard = FindObjectInActiveScene(TaskBoardName);
        var task1 = taskBoard == null ? null : FindChildByName(taskBoard.transform, Task1Name);
        return task1 == null ? null : task1.GetComponent<TMP_Text>();
    }

    private static bool IsUnderClientUi(Transform target)
    {
        var current = target;
        while (current != null)
        {
            if (string.Equals(current.name, "ClientUI", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsUnderTaskBoard(Transform target)
    {
        var current = target;
        while (current != null)
        {
            if (string.Equals(current.name, TaskBoardName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Vector3 GetWorldCenter(GameObject target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(target.transform.position, Vector3.zero);
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds.center : target.transform.position;
    }

    private Vector3 GetHostWorldCenter()
    {
        return hostBody == null ? hostRoot.position : GetWorldCenter(hostBody.gameObject);
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
