using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1WaterCrystalCollectionController : MonoBehaviour
{
    private const string CollectWaterCrystalMessageName = "TestStage1.CollectWaterCrystal";
    private const string HostRootName = "Host3D";
    private const string HostBodyName = "Beta_Joints";
    private const string CrystalWaterName = "Crystal_water";
    private const string TaskBoardName = "taskboard";
    private const string Task2Name = "Task2";
    private const string Task2CompletedText = "V 收集水元素礦物";
    private const string Task2IncompleteText = "收集水元素礦物";
    private const float CollectDistance = 3f;

    public static event Action<bool> CompletedChanged;
    public static bool IsCompleted { get; private set; }

    private Transform hostRoot;
    private Transform hostBody;
    private GameObject waterCrystal;
    private TMP_Text task2Text;
    private bool initialized;
    private bool handlerRegistered;
    private bool waterCrystalCollected;

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
            CollectWaterCrystal();
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
        waterCrystal = FindObjectInActiveScene(CrystalWaterName);
        task2Text = FindTask2Text();

        if (task2Text != null)
        {
            task2Text.text = waterCrystalCollected ? Task2CompletedText : Task2IncompleteText;
        }

        if (hostRoot == null || waterCrystal == null || task2Text == null)
        {
            return;
        }

        initialized = true;
        ApplyCollectedState(waterCrystalCollected);
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
            && !waterCrystalCollected
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsServer
            && TestStage1HandState.RightHandTool == TestStage1RightHandTool.Pickaxe
            && TestStage1HandState.ElementPower == TestStage1ElementPower.Wind
            && waterCrystal != null
            && waterCrystal.activeInHierarchy
            && hostRoot != null
            && Vector3.Distance(GetHostWorldCenter(), GetWorldCenter(waterCrystal)) <= CollectDistance;
    }

    private void CollectWaterCrystal()
    {
        ApplyCollectedState(true);
        BroadcastWaterCrystalCollected();
    }

    private void ApplyCollectedState(bool isCollected)
    {
        waterCrystalCollected = isCollected;
        SetCompletion(isCollected);

        if (waterCrystal != null && waterCrystal.activeSelf == isCollected)
        {
            waterCrystal.SetActive(!isCollected);
        }

        if (task2Text != null)
        {
            task2Text.text = isCollected ? Task2CompletedText : Task2IncompleteText;
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

    private void BroadcastWaterCrystalCollected()
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
                CollectWaterCrystalMessageName,
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
            CollectWaterCrystalMessageName,
            OnCollectWaterCrystalMessageReceived
        );
        handlerRegistered = true;
    }

    private void UnregisterHandler()
    {
        if (!handlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CollectWaterCrystalMessageName);
        handlerRegistered = false;
    }

    private static void OnCollectWaterCrystalMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool isCollected);
        var controller = FindFirstObjectByType<TestStage1WaterCrystalCollectionController>();
        controller?.ApplyCollectedState(isCollected);
    }

    private static TMP_Text FindTask2Text()
    {
        var taskBoard = FindObjectInActiveScene(TaskBoardName);
        var task2 = taskBoard == null ? null : FindChildByName(taskBoard.transform, Task2Name);
        return task2 == null ? null : task2.GetComponent<TMP_Text>();
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
