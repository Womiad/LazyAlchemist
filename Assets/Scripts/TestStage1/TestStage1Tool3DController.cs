using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1Tool3DController : MonoBehaviour
{
    private const string ChangeToolMessageName = "TestStage1.ChangeTool3D";
    private const string ToolsRootName = "tools3D";
    private const string StaffName = "staff";
    private const string PickaxeName = "pickaxe";

    private Transform toolsRoot;
    private Transform staffTool;
    private Transform pickaxeTool;
    private TestStage1RightHandTool currentTool = TestStage1RightHandTool.None;
    private bool initialized;
    private bool hostHandlerRegistered;

    private void Update()
    {
        RegisterHostHandlerIfNeeded();

        if (!initialized)
        {
            TryInitialize();
        }
        else
        {
            ApplyToolVisibility(currentTool);
        }
    }

    private void OnDestroy()
    {
        UnregisterHostHandler();
    }

    public static void RequestHostTool(string itemName)
    {
        RequestHostTool(TestStage1HandState.ToolFromItemName(itemName));
    }

    public static void RequestHostEmptyTool()
    {
        RequestHostTool(TestStage1RightHandTool.None);
    }

    private static void RequestHostTool(TestStage1RightHandTool tool)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            ApplyToolToActiveController(tool);
            return;
        }

        using var writer = new FastBufferWriter(1, Allocator.Temp);
        writer.WriteValueSafe((byte)tool);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            ChangeToolMessageName,
            NetworkManager.ServerClientId,
            writer
        );
    }

    private void TryInitialize()
    {
        toolsRoot = FindObjectInActiveScene(ToolsRootName)?.transform;
        if (toolsRoot == null)
        {
            return;
        }

        CacheToolObjects();
        if (staffTool == null || pickaxeTool == null)
        {
            Debug.LogWarning("TestStage1 tools3D needs two model children for staff and pickaxe.");
            return;
        }

        initialized = true;
        ApplyToolVisibility(currentTool);
    }

    private void ApplyTool(TestStage1RightHandTool tool)
    {
        currentTool = tool;
        TestStage1HandState.SetRightHandTool(tool);

        if (!initialized)
        {
            TryInitialize();
            return;
        }

        ApplyToolVisibility(tool);
    }

    private void CacheToolObjects()
    {
        staffTool = FindDirectChildByName(toolsRoot, StaffName);
        pickaxeTool = FindDirectChildByName(toolsRoot, PickaxeName);

        if (staffTool != null && pickaxeTool != null)
        {
            return;
        }

        var modelChildren = new System.Collections.Generic.List<Transform>();
        for (var i = 0; i < toolsRoot.childCount; i++)
        {
            var child = toolsRoot.GetChild(i);
            if (HasVisibleModel(child))
            {
                modelChildren.Add(child);
            }
        }

        if (staffTool == null && modelChildren.Count > 0)
        {
            staffTool = modelChildren[0];
        }

        if (pickaxeTool == null && modelChildren.Count > 1)
        {
            pickaxeTool = modelChildren[1];
        }
    }

    private void ApplyToolVisibility(TestStage1RightHandTool tool)
    {
        SetToolVisible(staffTool, tool == TestStage1RightHandTool.Staff);
        SetToolVisible(pickaxeTool, tool == TestStage1RightHandTool.Pickaxe);
    }

    private void RegisterHostHandlerIfNeeded()
    {
        if (hostHandlerRegistered || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            ChangeToolMessageName,
            OnChangeToolMessageReceived
        );
        hostHandlerRegistered = true;
    }

    private void UnregisterHostHandler()
    {
        if (!hostHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ChangeToolMessageName);
        hostHandlerRegistered = false;
    }

    private static void OnChangeToolMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte toolValue);
        ApplyToolToActiveController((TestStage1RightHandTool)toolValue);
    }

    private static void ApplyToolToActiveController(TestStage1RightHandTool tool)
    {
        var controller = FindFirstObjectByType<TestStage1Tool3DController>();
        controller?.ApplyTool(IsKnownTool(tool) ? tool : TestStage1RightHandTool.None);
    }

    private static bool IsKnownTool(TestStage1RightHandTool tool)
    {
        return tool == TestStage1RightHandTool.None
            || tool == TestStage1RightHandTool.Pickaxe
            || tool == TestStage1RightHandTool.Staff;
    }

    private static bool HasVisibleModel(Transform target)
    {
        return target != null
            && target.GetComponentInChildren<Renderer>(true) != null;
    }

    private static void SetToolVisible(Transform target, bool isVisible)
    {
        if (target == null)
        {
            return;
        }

        if (target.gameObject.activeSelf != true)
        {
            target.gameObject.SetActive(true);
        }

        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = isVisible;
        }

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

    private static Transform FindDirectChildByName(Transform parent, string objectName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }
}
