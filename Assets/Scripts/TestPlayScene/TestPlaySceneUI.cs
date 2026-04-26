using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class TestPlaySceneUI : MonoBehaviour
{
    private const string ChangeColorMessageName = "TestPlayScene.ChangeColor";

    private Button colorButton;
    private Text statusText;
    private bool initialized;
    private bool hostHandlerRegistered;

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

        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureEventSystem();

        var canvasObject = new GameObject(
            "TestPlaySceneCanvas",
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
            new Vector2(560f, 120f),
            30,
            TextAnchor.MiddleCenter
        );

        colorButton = CreateButton(
            "ChangeColorButton",
            canvasObject.transform,
            new Vector2(0f, -320f),
            new Vector2(360f, 120f),
            "Change Color"
        );
        colorButton.onClick.AddListener(OnChangeColorClicked);
    }

    private void RefreshUi()
    {
        if (!initialized || NetworkManager.Singleton == null)
        {
            return;
        }

        var isClientOnly = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost;

        if (colorButton != null)
        {
            colorButton.gameObject.SetActive(isClientOnly);
            colorButton.interactable = isClientOnly;
        }

        if (statusText == null)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Host: use WASD to move the cube.";
        }
        else if (isClientOnly)
        {
            statusText.text = "Tap the button to change the host cube color.";
        }
        else
        {
            statusText.text = "Waiting for network state...";
        }
    }

    private void OnChangeColorClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        if (NetworkManager.Singleton.IsHost)
        {
            TestPlaySceneHostCube.Instance?.ApplyRandomColor();
            return;
        }

        using var writer = new FastBufferWriter(1, Allocator.Temp);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            ChangeColorMessageName,
            NetworkManager.ServerClientId,
            writer
        );

        if (statusText != null)
        {
            statusText.text = "Sent color change request.";
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
        TestPlaySceneHostCube.Instance?.ApplyRandomColor();
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
        string label
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
        image.color = new Color32(255, 255, 255, 235);

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
        text.color = new Color32(30, 30, 30, 255);
        text.text = label;

        return button;
    }
}
