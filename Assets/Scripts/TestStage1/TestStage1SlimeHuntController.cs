using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1SlimeHuntController : MonoBehaviour
{
    private const string SlimeHuntProgressMessageName = "TestStage1.SlimeHuntProgress";
    private const string TaskBoardName = "taskboard";
    private const string Task3Name = "Task3";
    private const int RequiredDefeatCount = 10;

    public static event Action<bool> CompletedChanged;
    public static bool IsCompleted { get; private set; }

    private TMP_Text task3Text;
    private int defeatedCount;
    private bool initialized;
    private bool clientHandlerRegistered;
    private bool serverConnectionCallbackRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsCompleted = false;
        CompletedChanged = null;
    }

    private void OnEnable()
    {
        TestStage1SlimeSmallController.Defeated += OnSlimeDefeated;
    }

    private void Update()
    {
        RegisterClientHandlerIfNeeded();
        RegisterServerConnectionCallbackIfNeeded();

        if (!initialized)
        {
            TryInitialize();
        }
    }

    private void OnDisable()
    {
        TestStage1SlimeSmallController.Defeated -= OnSlimeDefeated;
    }

    private void OnDestroy()
    {
        UnregisterClientHandler();
        UnregisterServerConnectionCallback();
        TestStage1SlimeSmallController.Defeated -= OnSlimeDefeated;
    }

    private void TryInitialize()
    {
        task3Text = FindTask3Text();
        if (task3Text == null)
        {
            return;
        }

        initialized = true;
        ApplyProgress(defeatedCount);
    }

    private void OnSlimeDefeated(TestStage1SlimeSmallController slime)
    {
        if (!ShouldHostCountDefeat())
        {
            return;
        }

        ApplyProgress(Mathf.Min(RequiredDefeatCount, defeatedCount + 1));
        BroadcastProgress();
    }

    private static bool ShouldHostCountDefeat()
    {
        return NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsListening
            || NetworkManager.Singleton.IsServer;
    }

    private void ApplyProgress(int count)
    {
        defeatedCount = Mathf.Clamp(count, 0, RequiredDefeatCount);
        SetCompletion(defeatedCount >= RequiredDefeatCount);

        if (task3Text == null)
        {
            task3Text = FindTask3Text();
        }

        if (task3Text != null)
        {
            task3Text.text = defeatedCount >= RequiredDefeatCount
                ? $"V 討伐史萊姆 ({RequiredDefeatCount}/{RequiredDefeatCount})"
                : $"討伐史萊姆 ({defeatedCount}/{RequiredDefeatCount})";
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

    private void BroadcastProgress()
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

            SendProgressToClient(clientId);
        }
    }

    private void SendProgressToClient(ulong clientId)
    {
        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        writer.WriteValueSafe(defeatedCount);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            SlimeHuntProgressMessageName,
            clientId,
            writer
        );
    }

    private void RegisterClientHandlerIfNeeded()
    {
        if (clientHandlerRegistered || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            SlimeHuntProgressMessageName,
            OnSlimeHuntProgressMessageReceived
        );
        clientHandlerRegistered = true;
    }

    private void UnregisterClientHandler()
    {
        if (!clientHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(SlimeHuntProgressMessageName);
        clientHandlerRegistered = false;
    }

    private void RegisterServerConnectionCallbackIfNeeded()
    {
        if (serverConnectionCallbackRegistered
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        serverConnectionCallbackRegistered = true;
    }

    private void UnregisterServerConnectionCallback()
    {
        if (!serverConnectionCallbackRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        serverConnectionCallbackRegistered = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null
            || clientId == NetworkManager.ServerClientId
            || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        SendProgressToClient(clientId);
    }

    private static void OnSlimeHuntProgressMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        var controller = FindFirstObjectByType<TestStage1SlimeHuntController>();
        controller?.ApplyProgress(count);
    }

    private static TMP_Text FindTask3Text()
    {
        var taskBoard = FindObjectInActiveScene(TaskBoardName);
        var task3 = taskBoard == null ? null : FindChildByName(taskBoard.transform, Task3Name);
        return task3 == null ? null : task3.GetComponent<TMP_Text>();
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
