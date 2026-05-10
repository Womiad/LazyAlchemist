using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    public GameObject connectionUI;
    public GameObject hostUI;
    public GameObject clientUI;

    [SerializeField] private string clientAddressPrefsKey = "LazyAlchemist.ClientAddress";

    private InputField clientAddressInput;
    private Text connectionStatusText;
    private Text connectionHintText;
    private Text hostClientStateText;
    private Text hostStartHintText;
    private Text clientStateText;
    private Text clientReadyHintText;
    private string latestClientAddress;
    private bool hasLocalClientReadyOverride;
    private bool localClientReadyOverride;

    private void Awake()
    {
        ApplyMobileLandscapeOrientation();
        EnsureConnectionWidgets();
    }

    private void Update()
    {
        RefreshLobbyState();
    }

    public string GetClientAddress(string fallbackAddress)
    {
        EnsureConnectionWidgets();

        if (clientAddressInput == null)
        {
            return fallbackAddress;
        }

        CommitClientAddressInput();

        var value = NormalizeClientAddress(clientAddressInput.text);
        if (string.IsNullOrEmpty(value))
        {
            value = !string.IsNullOrWhiteSpace(latestClientAddress)
                ? latestClientAddress
                : fallbackAddress;
            clientAddressInput.text = value;
        }

        latestClientAddress = value;
        SaveClientAddress(value);
        return value;
    }

    public void SetClientAddress(string value)
    {
        EnsureConnectionWidgets();

        if (clientAddressInput == null)
        {
            return;
        }

        latestClientAddress = NormalizeClientAddress(value);
        clientAddressInput.text = latestClientAddress;
        SaveClientAddress(latestClientAddress);
    }

    public void ShowLobby(string statusMessage = "")
    {
        EnsureLobbyWidgets();
        connectionUI.SetActive(false);
        SetStatus(statusMessage);

        if (NetworkManager.Singleton.IsHost)
        {
            hostUI.SetActive(true);
            clientUI.SetActive(false);
        }
        else
        {
            hostUI.SetActive(false);
            clientUI.SetActive(true);
        }

        RefreshLobbyState();
    }

    public void ShowConnectionScreen(string message = "")
    {
        EnsureConnectionWidgets();
        connectionUI.SetActive(true);
        hostUI.SetActive(false);
        clientUI.SetActive(false);
        SetStatus(message);
    }

    public void ShowConnectionFailed(string message)
    {
        ShowConnectionScreen(message);
    }

    public void SetConnectionHint(string message)
    {
        EnsureConnectionWidgets();

        if (connectionHintText == null)
        {
            return;
        }

        connectionHintText.text = message;
    }

    public void ShowStartBlockedByClientReady()
    {
        EnsureLobbyWidgets();

        if (hostStartHintText != null)
        {
            hostStartHintText.text = "Client must press Ready before Start.";
            hostStartHintText.color = new Color32(255, 205, 90, 255);
        }

        RefreshLobbyState();
    }

    public void ShowClientReadyFeedback(bool ready)
    {
        EnsureLobbyWidgets();
        hasLocalClientReadyOverride = true;
        localClientReadyOverride = ready;
        SetClientReadyPrompt(ready);
    }

    private void EnsureConnectionWidgets()
    {
        if (connectionUI == null || clientAddressInput != null)
        {
            return;
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font == null)
        {
            Debug.LogWarning("Built-in font not found. Connection UI widgets were not created.");
            return;
        }

        var savedAddress = PlayerPrefs.GetString(clientAddressPrefsKey, string.Empty);
        latestClientAddress = string.IsNullOrWhiteSpace(savedAddress) ? "192.168.0.10" : savedAddress;

        clientAddressInput = CreateInputField(
            "ClientAddressInput",
            connectionUI.transform,
            font,
            latestClientAddress,
            "Host IP",
            new Vector2(0f, -55f),
            new Vector2(280f, 36f)
        );
        clientAddressInput.onValueChanged.AddListener(HandleClientAddressChanged);
        clientAddressInput.onEndEdit.AddListener(HandleClientAddressChanged);

        connectionHintText = CreateStandaloneText(
            "ConnectionHintText",
            connectionUI.transform,
            font,
            new Vector2(0f, -95f),
            new Vector2(520f, 36f),
            14,
            TextAnchor.MiddleCenter,
            new Color32(220, 220, 220, 255)
        );

        connectionStatusText = CreateStandaloneText(
            "ConnectionStatusText",
            connectionUI.transform,
            font,
            new Vector2(0f, -132f),
            new Vector2(540f, 72f),
            16,
            TextAnchor.UpperCenter,
            Color.white
        );

        SetConnectionHint("Enter the host PC IP on the client device.");
        SetStatus(string.Empty);
    }

    private void EnsureLobbyWidgets()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font == null)
        {
            Debug.LogWarning("Built-in font not found. Lobby status widgets were not created.");
            return;
        }

        if (hostUI != null && hostClientStateText == null)
        {
            hostClientStateText = CreateStandaloneText(
                "HostClientStateText",
                hostUI.transform,
                font,
                new Vector2(0f, 95f),
                new Vector2(520f, 44f),
                20,
                TextAnchor.MiddleCenter,
                Color.white
            );

            hostStartHintText = CreateStandaloneText(
                "HostStartHintText",
                hostUI.transform,
                font,
                new Vector2(0f, 50f),
                new Vector2(560f, 52f),
                17,
                TextAnchor.MiddleCenter,
                new Color32(255, 205, 90, 255)
            );
        }

        if (clientUI != null && clientStateText == null)
        {
            clientStateText = CreateStandaloneText(
                "ClientStateText",
                clientUI.transform,
                font,
                new Vector2(0f, 95f),
                new Vector2(520f, 48f),
                20,
                TextAnchor.MiddleCenter,
                Color.white
            );

            clientReadyHintText = CreateStandaloneText(
                "ClientReadyHintText",
                clientUI.transform,
                font,
                new Vector2(0f, 50f),
                new Vector2(560f, 52f),
                17,
                TextAnchor.MiddleCenter,
                new Color32(255, 205, 90, 255)
            );
        }
    }

    private void RefreshLobbyState()
    {
        if (LobbyManager.Instance == null || NetworkManager.Singleton == null)
        {
            return;
        }

        var ready = LobbyManager.Instance.IsClientReady();
        var localDisplayReady = ready;

        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost && hasLocalClientReadyOverride)
        {
            localDisplayReady = localClientReadyOverride;

            if (ready == localClientReadyOverride)
            {
                hasLocalClientReadyOverride = false;
            }
        }

        var stateText = ready ? "Client state: Ready" : "Client state: Not Ready";
        var stateColor = ready
            ? new Color32(100, 235, 135, 255)
            : new Color32(255, 205, 90, 255);
        var localStateColor = localDisplayReady
            ? new Color32(100, 235, 135, 255)
            : new Color32(255, 205, 90, 255);

        if (hostClientStateText != null)
        {
            hostClientStateText.text = stateText;
            hostClientStateText.color = stateColor;
        }

        if (clientStateText != null)
        {
            clientStateText.text = localDisplayReady ? "Your state: Ready" : "Your state: Not Ready";
            clientStateText.color = localStateColor;
            SetClientReadyPrompt(localDisplayReady);
        }

        if (hostStartHintText != null && ready)
        {
            hostStartHintText.text = "Ready to start.";
            hostStartHintText.color = new Color32(100, 235, 135, 255);
        }
        else if (hostStartHintText != null
                 && (string.IsNullOrEmpty(hostStartHintText.text) || hostStartHintText.text == "Ready to start."))
        {
            hostStartHintText.text = "Waiting for client Ready.";
            hostStartHintText.color = new Color32(255, 205, 90, 255);
        }
    }

    private void SetClientReadyPrompt(bool ready)
    {
        if (clientReadyHintText == null)
        {
            return;
        }

        if (ready)
        {
            clientReadyHintText.text = "Ready sent. Waiting for host to start.";
            clientReadyHintText.color = new Color32(100, 235, 135, 255);
        }
        else
        {
            clientReadyHintText.text = "Not Ready. Press Ready when you are prepared.";
            clientReadyHintText.color = new Color32(255, 205, 90, 255);
        }
    }

    private void SaveClientAddress(string value)
    {
        PlayerPrefs.SetString(clientAddressPrefsKey, value);
        PlayerPrefs.Save();
    }

    private static void ApplyMobileLandscapeOrientation()
    {
        if (!Application.isMobilePlatform)
        {
            return;
        }

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    private void HandleClientAddressChanged(string value)
    {
        latestClientAddress = NormalizeClientAddress(value);
    }

    private void CommitClientAddressInput()
    {
        if (clientAddressInput == null)
        {
            return;
        }

        clientAddressInput.DeactivateInputField();
        clientAddressInput.ForceLabelUpdate();

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == clientAddressInput.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private static string NormalizeClientAddress(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void SetStatus(string message)
    {
        if (connectionStatusText == null)
        {
            return;
        }

        connectionStatusText.text = message;
    }

    private static InputField CreateInputField(
        string objectName,
        Transform parent,
        Font font,
        string textValue,
        string placeholderValue,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        var root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
        root.transform.SetParent(parent, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = root.GetComponent<Image>();
        image.color = new Color32(255, 255, 255, 245);

        var inputField = root.GetComponent<InputField>();
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.contentType = InputField.ContentType.Standard;

        var text = CreateChildText(
            "Text",
            root.transform,
            font,
            new Color32(32, 32, 32, 255),
            TextAnchor.MiddleLeft
        );

        var placeholder = CreateChildText(
            "Placeholder",
            root.transform,
            font,
            new Color32(120, 120, 120, 180),
            TextAnchor.MiddleLeft
        );
        placeholder.text = placeholderValue;
        placeholder.fontStyle = FontStyle.Italic;

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.text = textValue;
        return inputField;
    }

    private static Text CreateStandaloneText(
        string objectName,
        Transform parent,
        Font font,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        TextAnchor alignment,
        Color color
    )
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text CreateChildText(
        string objectName,
        Transform parent,
        Font font,
        Color color,
        TextAnchor alignment
    )
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 6f);
        rect.offsetMax = new Vector2(-12f, -6f);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = 18;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        return text;
    }
}
