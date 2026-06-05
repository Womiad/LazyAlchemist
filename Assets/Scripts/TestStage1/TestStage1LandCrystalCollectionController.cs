using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1LandCrystalCollectionController : MonoBehaviour
{
    private const string CollectLandCrystalMessageName = "TestStage1.CollectLandCrystal";
    private const string HostRootName = "Host3D";
    private const string HostBodyName = "Beta_Joints";
    private const string CrystalLandName = "Crystal_land";
    private const float CollectDistance = 3f;

    private Transform hostRoot;
    private Transform hostBody;
    private GameObject landCrystal;
    private bool initialized;
    private bool handlerRegistered;
    private bool landCrystalCollected;

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
            CollectLandCrystal();
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
        landCrystal = FindObjectInActiveScene(CrystalLandName);

        if (hostRoot == null || landCrystal == null)
        {
            return;
        }

        initialized = true;
        ApplyCollectedState(landCrystalCollected);
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
            && !landCrystalCollected
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsServer
            && TestStage1HandState.RightHandTool == TestStage1RightHandTool.Pickaxe
            && TestStage1HandState.ElementPower == TestStage1ElementPower.Fire
            && landCrystal != null
            && landCrystal.activeInHierarchy
            && hostRoot != null
            && Vector3.Distance(GetHostWorldCenter(), GetWorldCenter(landCrystal)) <= CollectDistance;
    }

    private void CollectLandCrystal()
    {
        ApplyCollectedState(true);
        BroadcastLandCrystalCollected();
    }

    private void ApplyCollectedState(bool isCollected)
    {
        landCrystalCollected = isCollected;

        if (landCrystal != null && landCrystal.activeSelf == isCollected)
        {
            landCrystal.SetActive(!isCollected);
        }
    }

    private void BroadcastLandCrystalCollected()
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
                CollectLandCrystalMessageName,
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
            CollectLandCrystalMessageName,
            OnCollectLandCrystalMessageReceived
        );
        handlerRegistered = true;
    }

    private void UnregisterHandler()
    {
        if (!handlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CollectLandCrystalMessageName);
        handlerRegistered = false;
    }

    private static void OnCollectLandCrystalMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool isCollected);
        var controller = FindFirstObjectByType<TestStage1LandCrystalCollectionController>();
        controller?.ApplyCollectedState(isCollected);
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
