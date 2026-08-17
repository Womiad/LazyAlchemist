using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class TestStage1UI : MonoBehaviour
{
    private const string ChangeColorMessageName = "TestStage1.ChangeBetaJointsHue";
    public const byte EmptyElementColorIndex = 4;

    private bool hostHandlerRegistered;
    private bool mobileLandscapeApplied;

    private void Update()
    {
        EnsureUi();
    }

    private void OnDestroy()
    {
        UnregisterHostHandler();
    }

    private void EnsureUi()
    {
        if (TestStage1DebugMode.IsEnabled)
        {
            if (TestStage1DebugMode.IsClientOnly)
            {
                ApplyMobileClientOrientationIfNeeded();
            }
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        RegisterHostHandlerIfNeeded();

        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            ApplyMobileClientOrientationIfNeeded();
        }
    }

    private void ApplyMobileClientOrientationIfNeeded()
    {
        if (mobileLandscapeApplied
            || !Application.isMobilePlatform
            || (!TestStage1DebugMode.IsClientOnly
                && (NetworkManager.Singleton == null
                    || !NetworkManager.Singleton.IsClient
                    || NetworkManager.Singleton.IsHost)))
        {
            return;
        }

        mobileLandscapeApplied = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    public static void RequestHostPresetColor(byte colorIndex)
    {
        if (TestStage1DebugMode.IsEnabled)
        {
            TestStage1HandState.SetElementPower(TestStage1HandState.ElementFromColorIndex(colorIndex));
            TestStage1HostBetaJoints.Instance?.ApplyPresetColor(colorIndex);
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            TestStage1HandState.SetElementPower(TestStage1HandState.ElementFromColorIndex(colorIndex));
            TestStage1HostBetaJoints.Instance?.ApplyPresetColor(colorIndex);
            return;
        }

        using var writer = new FastBufferWriter(1, Allocator.Temp);
        writer.WriteValueSafe(colorIndex);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            ChangeColorMessageName,
            NetworkManager.ServerClientId,
            writer
        );
    }

    private void RegisterHostHandlerIfNeeded()
    {
        if (hostHandlerRegistered || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            ChangeColorMessageName,
            OnChangeColorMessageReceived
        );
        hostHandlerRegistered = true;
    }

    private void UnregisterHostHandler()
    {
        if (!hostHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ChangeColorMessageName);
        hostHandlerRegistered = false;
    }

    private static void OnChangeColorMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte colorIndex);
        TestStage1HandState.SetElementPower(TestStage1HandState.ElementFromColorIndex(colorIndex));
        TestStage1HostBetaJoints.Instance?.ApplyPresetColor(colorIndex);
    }
}
