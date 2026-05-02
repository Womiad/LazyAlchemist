using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class TestStage1UI : MonoBehaviour
{
    private const string ChangeColorMessageName = "TestStage1.ChangeBetaJointsHue";

    private readonly Button[] colorButtons = new Button[4];
    private Text statusText;
    private bool initialized;
    private bool hostHandlerRegistered;
    private bool mobileLandscapeApplied;

    private void Update()
    {
        EnsureUi();
        RefreshUi();
    }

    private void OnDestroy()
    {
        UnregisterHostHandler();
    }

    private void EnsureUi()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        RegisterHostHandlerIfNeeded();
        ApplyMobileClientOrientationIfNeeded();

        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureEventSystem();

        var canvasObject = new GameObject(
            "TestStage1Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        statusText = CreateText(
            "StatusText",
            canvasObject.transform,
            new Vector2(0f, -120f),
            new Vector2(680f, 120f),
            30,
            TextAnchor.MiddleCenter
        );

        CreateColorButtons(canvasObject.transform);
    }

    private void RefreshUi()
    {
        if (!initialized || NetworkManager.Singleton == null)
        {
            return;
        }

        var isClientOnly = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost;

        foreach (var button in colorButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.gameObject.SetActive(isClientOnly);
            button.interactable = isClientOnly;
        }

        if (statusText == null)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Host: Beta_Joints receives client color changes.";
        }
        else if (isClientOnly)
        {
            statusText.text = "Tap a color to change the host Beta_Joints material.";
        }
        else
        {
            statusText.text = "Waiting for network state...";
        }
    }

    private void ApplyMobileClientOrientationIfNeeded()
    {
        if (mobileLandscapeApplied
            || !Application.isMobilePlatform
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsClient
            || NetworkManager.Singleton.IsHost)
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

    private void CreateColorButtons(Transform parent)
    {
        var labels = new[] { "Red", "Yellow", "Green", "Blue" };
        var colors = new[]
        {
            new Color32(235, 50, 45, 245),
            new Color32(245, 205, 45, 245),
            new Color32(55, 210, 85, 245),
            new Color32(55, 110, 235, 245)
        };

        const float spacing = 24f;
        var size = new Vector2(210f, 110f);
        var totalWidth = colors.Length * size.x + (colors.Length - 1) * spacing;
        var startX = -totalWidth * 0.5f + size.x * 0.5f;

        for (var i = 0; i < colorButtons.Length; i++)
        {
            var colorIndex = (byte)i;
            var position = new Vector2(startX + i * (size.x + spacing), -320f);

            colorButtons[i] = CreateButton(
                $"SetBetaJoints{labels[i]}Button",
                parent,
                position,
                size,
                labels[i],
                colors[i]
            );
            colorButtons[i].onClick.AddListener(() => OnColorClicked(colorIndex));
        }
    }

    private void OnColorClicked(byte colorIndex)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
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

        if (statusText != null)
        {
            statusText.text = "Sent Beta_Joints color request.";
        }
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
        TestStage1HostBetaJoints.Instance?.ApplyPresetColor(colorIndex);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule)
        );
    }

    private static Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        TextAnchor alignment
    )
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        string label,
        Color32 backgroundColor
    )
    {
        var buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        var button = buttonObject.GetComponent<Button>();

        var text = CreateText(
            "Label",
            buttonObject.transform,
            Vector2.zero,
            sizeDelta,
            32,
            TextAnchor.MiddleCenter
        );
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = Vector2.zero;
        text.color = new Color32(20, 20, 20, 255);
        text.text = label;

        return button;
    }
}
