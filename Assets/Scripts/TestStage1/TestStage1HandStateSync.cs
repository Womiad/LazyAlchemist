using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1HandStateSync : MonoBehaviour
{
    private const string ChangeLeftHandSlimeBaitMessageName = "TestStage1.ChangeLeftHandSlimeBait";
    private const string SlimeBait3DName = "SlimeBait3D";

    private Transform slimeBait3D;
    private readonly System.Collections.Generic.List<GameObject> slimeBaitLightObjects = new System.Collections.Generic.List<GameObject>();
    private bool initialized;
    private bool hostHandlerRegistered;
    private bool clientHandlerRegistered;

    private void Update()
    {
        RegisterHostHandlerIfNeeded();
        RegisterClientHandlerIfNeeded();

        if (!initialized)
        {
            TryInitialize();
        }
        else
        {
            ApplySlimeBaitVisibility(TestStage1HandState.HasSlimeBaitInLeftHand);
        }
    }

    private void OnDestroy()
    {
        UnregisterHostHandler();
        UnregisterClientHandler();
    }

    public static void RequestHostLeftHandSlimeBait(bool hasSlimeBait)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            ApplyLeftHandSlimeBaitToActiveController(hasSlimeBait);
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            ApplyLeftHandSlimeBaitToActiveController(hasSlimeBait);
            BroadcastLeftHandSlimeBait(hasSlimeBait);
            return;
        }

        ApplyLeftHandSlimeBaitToActiveController(hasSlimeBait);

        using var writer = new FastBufferWriter(1, Allocator.Temp);
        writer.WriteValueSafe(hasSlimeBait);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            ChangeLeftHandSlimeBaitMessageName,
            NetworkManager.ServerClientId,
            writer
        );
    }

    private void TryInitialize()
    {
        slimeBait3D = FindObjectInActiveScene(SlimeBait3DName)?.transform;
        if (slimeBait3D == null)
        {
            return;
        }

        slimeBaitLightObjects.Clear();
        CollectLightObjects(slimeBait3D, slimeBaitLightObjects);
        initialized = true;
        ApplySlimeBaitVisibility(TestStage1HandState.HasSlimeBaitInLeftHand);
    }

    private void ApplyLeftHandSlimeBait(bool hasSlimeBait)
    {
        TestStage1HandState.SetLeftHandSlimeBait(hasSlimeBait);

        if (!initialized)
        {
            TryInitialize();
            return;
        }

        ApplySlimeBaitVisibility(hasSlimeBait);
    }

    private void ApplySlimeBaitVisibility(bool isVisible)
    {
        if (slimeBait3D == null)
        {
            initialized = false;
            return;
        }

        if (!slimeBait3D.gameObject.activeSelf)
        {
            slimeBait3D.gameObject.SetActive(true);
        }

        foreach (var renderer in slimeBait3D.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = isVisible;
        }

        foreach (var light in slimeBait3D.GetComponentsInChildren<Light>(true))
        {
            light.enabled = isVisible;
        }

        foreach (var lightObject in slimeBaitLightObjects)
        {
            if (lightObject != null && lightObject.activeSelf != isVisible)
            {
                lightObject.SetActive(isVisible);
            }
        }

        foreach (var particleSystem in slimeBait3D.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (isVisible)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        foreach (var behaviour in slimeBait3D.GetComponentsInChildren<Behaviour>(true))
        {
            if (string.Equals(behaviour.GetType().Name, "VisualEffect", StringComparison.Ordinal))
            {
                behaviour.enabled = isVisible;
            }
        }
    }

    private static void CollectLightObjects(Transform parent, System.Collections.Generic.List<GameObject> results)
    {
        if (parent.GetComponent<Light>() != null
            || parent.name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            results.Add(parent.gameObject);
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            CollectLightObjects(parent.GetChild(i), results);
        }
    }

    private void RegisterHostHandlerIfNeeded()
    {
        if (hostHandlerRegistered || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            ChangeLeftHandSlimeBaitMessageName,
            OnChangeLeftHandSlimeBaitMessageReceived
        );
        hostHandlerRegistered = true;
    }

    private void RegisterClientHandlerIfNeeded()
    {
        if (clientHandlerRegistered
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsClient
            || NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            ChangeLeftHandSlimeBaitMessageName,
            OnBroadcastLeftHandSlimeBaitMessageReceived
        );
        clientHandlerRegistered = true;
    }

    private void UnregisterHostHandler()
    {
        if (!hostHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ChangeLeftHandSlimeBaitMessageName);
        hostHandlerRegistered = false;
    }

    private void UnregisterClientHandler()
    {
        if (!clientHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ChangeLeftHandSlimeBaitMessageName);
        clientHandlerRegistered = false;
    }

    private static void OnChangeLeftHandSlimeBaitMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool hasSlimeBait);
        ApplyLeftHandSlimeBaitToActiveController(hasSlimeBait);
        BroadcastLeftHandSlimeBait(hasSlimeBait);
    }

    private static void OnBroadcastLeftHandSlimeBaitMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool hasSlimeBait);
        ApplyLeftHandSlimeBaitToActiveController(hasSlimeBait);
    }

    private static void BroadcastLeftHandSlimeBait(bool hasSlimeBait)
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
            writer.WriteValueSafe(hasSlimeBait);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                ChangeLeftHandSlimeBaitMessageName,
                clientId,
                writer
            );
        }
    }

    private static void ApplyLeftHandSlimeBaitToActiveController(bool hasSlimeBait)
    {
        var controller = FindFirstObjectByType<TestStage1HandStateSync>();
        if (controller == null)
        {
            TestStage1HandState.SetLeftHandSlimeBait(hasSlimeBait);
            return;
        }

        controller.ApplyLeftHandSlimeBait(hasSlimeBait);
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
