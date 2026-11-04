using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1ItemInventoryController : MonoBehaviour
{
    public enum SlimeBaitType { None, All, Fire, Land, Wind, Water }
    private static readonly bool TestModeUnlockFireflyOnStart = false;

    private const string ClientUiName = "ClientUI";
    private const string FireflyObjectName = "firefly";
    private const string FireflyButtonName = "firefly_btn";
    private const string LeftHandBlankName = "blank_left";
    private const string SlimeBaitObjectName = "slime_bait";
    private const string SlimeBaitDragObjectName = "slimeBait";
    private const string FireflyStateMessage = "TestStage1.FireflyInventory.v1";

    public static bool HasFirefly { get; private set; }
    public static bool HasSlimeBait { get; private set; }
    public static SlimeBaitType CurrentSlimeBait { get; private set; }
    public static event Action InventoryChanged;

    private readonly List<GameObject> fireflyObjects = new List<GameObject>();
    private readonly List<GameObject> slimeBaitObjects = new List<GameObject>();

    private bool initialized;
    private bool fireflyHandlerRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ResetRuntimeState();
        InventoryChanged = null;
    }

    public static void ResetRuntimeState()
    {
        HasFirefly = false;
        HasSlimeBait = false;
        CurrentSlimeBait = SlimeBaitType.None;
        InventoryChanged?.Invoke();
    }

    private void Update()
    {
        RegisterFireflyHandler();
        if (!initialized)
        {
            TryInitialize();
        }
    }

    public static void SetFireflyAcquired(bool isAcquired)
    {
        if (HasFirefly == isAcquired)
        {
            return;
        }

        HasFirefly = isAcquired;
        InventoryChanged?.Invoke();
        BroadcastFireflyState(isAcquired);
    }

    public static void SetSlimeBaitAcquired(bool isAcquired)
    {
        SetSlimeBait(isAcquired ? SlimeBaitType.All : SlimeBaitType.None);
    }

    public static void SetSlimeBait(SlimeBaitType type)
    {
        var isAcquired = type != SlimeBaitType.None;
        if (HasSlimeBait == isAcquired && CurrentSlimeBait == type)
        {
            return;
        }

        HasSlimeBait = isAcquired;
        CurrentSlimeBait = type;
        InventoryChanged?.Invoke();
    }

    private void OnEnable()
    {
        InventoryChanged += RefreshVisibility;
    }

    private void OnDisable()
    {
        InventoryChanged -= RefreshVisibility;
        var network = NetworkManager.Singleton;
        if (fireflyHandlerRegistered && network != null)
            network.CustomMessagingManager.UnregisterNamedMessageHandler(FireflyStateMessage);
        fireflyHandlerRegistered = false;
    }

    private void RegisterFireflyHandler()
    {
        var network = NetworkManager.Singleton;
        if (fireflyHandlerRegistered || network == null || !network.IsClient || network.IsServer) return;
        network.CustomMessagingManager.RegisterNamedMessageHandler(FireflyStateMessage, ReceiveFireflyState);
        fireflyHandlerRegistered = true;
    }

    private static void BroadcastFireflyState(bool acquired)
    {
        var network = NetworkManager.Singleton;
        if (network == null || !network.IsServer) return;
        foreach (ulong id in network.ConnectedClientsIds)
        {
            if (id == NetworkManager.ServerClientId) continue;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(acquired);
            network.CustomMessagingManager.SendNamedMessage(FireflyStateMessage, id, writer);
        }
    }

    private static void ReceiveFireflyState(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool acquired);
        if (HasFirefly == acquired) return;
        HasFirefly = acquired;
        InventoryChanged?.Invoke();
    }

    private void TryInitialize()
    {
        var clientUi = FindObjectInActiveScene(ClientUiName);
        if (clientUi == null)
        {
            return;
        }

        fireflyObjects.Clear();
        slimeBaitObjects.Clear();
        CollectNamedObjects(clientUi.transform, fireflyObjects, FireflyObjectName, FireflyButtonName);
        CollectNamedObjects(
            clientUi.transform,
            slimeBaitObjects,
            ShouldControlSlimeBaitObject,
            SlimeBaitObjectName,
            SlimeBaitDragObjectName
        );

        initialized = true;
        if (TestModeUnlockFireflyOnStart)
        {
            HasFirefly = true;
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (!initialized)
        {
            return;
        }

        SetObjectsActive(fireflyObjects, HasFirefly);
        SetObjectsActive(slimeBaitObjects, HasSlimeBait);
    }

    private static void SetObjectsActive(List<GameObject> objects, bool isActive)
    {
        foreach (var target in objects)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }

    private static void CollectNamedObjects(Transform parent, List<GameObject> results, params string[] objectNames)
    {
        CollectNamedObjects(parent, results, null, objectNames);
    }

    private static void CollectNamedObjects(
        Transform parent,
        List<GameObject> results,
        Predicate<Transform> shouldCollect,
        params string[] objectNames)
    {
        foreach (var objectName in objectNames)
        {
            if (string.Equals(parent.name, objectName, StringComparison.Ordinal)
                && (shouldCollect == null || shouldCollect(parent)))
            {
                results.Add(parent.gameObject);
                break;
            }
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            CollectNamedObjects(parent.GetChild(i), results, shouldCollect, objectNames);
        }
    }

    private static bool ShouldControlSlimeBaitObject(Transform target)
    {
        return !IsChildOfNamedObject(target, LeftHandBlankName);
    }

    private static bool IsChildOfNamedObject(Transform target, string parentName)
    {
        var current = target.parent;
        while (current != null)
        {
            if (string.Equals(current.name, parentName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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
