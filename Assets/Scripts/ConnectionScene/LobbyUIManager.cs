using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private const float MaxUiScaleMultiplier = 1.55f;
    private const string WoodBackgroundName = "wood";
    private const string LogoName = "logo";

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
    private ResponsiveUiBinding connectionUiBinding;
    private ResponsiveUiBinding lobbyUiBinding;
    private CoverImageBinding woodBackgroundBinding;
    private ContainedResponsiveUiBinding logoBinding;
    private Canvas cachedCanvas;
    private string latestClientAddress;
    private bool hasLocalClientReadyOverride;
    private bool localClientReadyOverride;

    private void Awake()
    {
        ApplyMobileLandscapeOrientation();
        ConfigureCanvasScaler();
        EnsureConnectionWidgets();
        EnsureResponsiveBindings();
        RefreshResponsiveLayout();
    }

    private void Update()
    {
        RefreshResponsiveLayout();
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
        RefreshResponsiveLayout();
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

        RefreshResponsiveLayout();
    }

    private void ConfigureCanvasScaler()
    {
        var canvas = GetUiCanvas();
        var canvasScaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            return;
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.scaleFactor = 1f;
    }

    private void EnsureResponsiveBindings()
    {
        var canvas = GetUiCanvas();

        if (connectionUiBinding == null && connectionUI != null)
        {
            connectionUiBinding = ResponsiveUiBinding.TryCreate(connectionUI.transform);
        }

        if (lobbyUiBinding == null)
        {
            var lobbyRoot = hostUI != null && hostUI.transform.parent != null
                ? hostUI.transform.parent
                : clientUI != null ? clientUI.transform.parent : null;
            lobbyUiBinding = ResponsiveUiBinding.TryCreate(lobbyRoot);
        }

        if (woodBackgroundBinding == null)
        {
            woodBackgroundBinding = CoverImageBinding.TryCreate(FindDirectCanvasChild(canvas, WoodBackgroundName));
        }

        if (logoBinding == null)
        {
            logoBinding = ContainedResponsiveUiBinding.TryCreate(FindDirectCanvasChild(canvas, LogoName));
        }
    }

    private void RefreshResponsiveLayout()
    {
        EnsureResponsiveBindings();

        var rootSize = GetCanvasSize();
        if (rootSize.x <= 0f || rootSize.y <= 0f)
        {
            return;
        }

        var scaleMultiplier = Mathf.Min(rootSize.x / ReferenceResolution.x, rootSize.y / ReferenceResolution.y);
        scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.45f, MaxUiScaleMultiplier);

        woodBackgroundBinding?.Apply(rootSize);
        logoBinding?.Apply(scaleMultiplier, rootSize);
        connectionUiBinding?.Apply(scaleMultiplier);
        lobbyUiBinding?.Apply(scaleMultiplier);
    }

    private Vector2 GetCanvasSize()
    {
        var canvas = GetUiCanvas();
        var canvasRect = canvas == null ? null : canvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            return canvasRect.rect.size;
        }

        return new Vector2(Screen.width, Screen.height);
    }

    private Canvas GetUiCanvas()
    {
        if (cachedCanvas != null)
        {
            return cachedCanvas;
        }

        cachedCanvas = FindCanvasFromObject(connectionUI)
            ?? FindCanvasFromObject(hostUI)
            ?? FindCanvasFromObject(clientUI)
            ?? FindFirstObjectByType<Canvas>();
        return cachedCanvas;
    }

    private static Canvas FindCanvasFromObject(GameObject target)
    {
        return target == null ? null : target.GetComponentInParent<Canvas>(true);
    }

    private static Transform FindDirectCanvasChild(Canvas canvas, string childName)
    {
        if (canvas == null)
        {
            return null;
        }

        var canvasTransform = canvas.transform;
        for (var i = 0; i < canvasTransform.childCount; i++)
        {
            var child = canvasTransform.GetChild(i);
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
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

    private sealed class ResponsiveUiBinding
    {
        private readonly RectTransform rectTransform;
        private readonly Vector3 baseScale;
        private readonly Vector2 baseAnchoredPosition;
        private float lastScaleMultiplier = -1f;

        private ResponsiveUiBinding(RectTransform rectTransform)
        {
            this.rectTransform = rectTransform;
            baseScale = rectTransform.localScale;
            baseAnchoredPosition = rectTransform.anchoredPosition;
        }

        public static ResponsiveUiBinding TryCreate(Transform target)
        {
            var rectTransform = target as RectTransform;
            return rectTransform == null ? null : new ResponsiveUiBinding(rectTransform);
        }

        public void Apply(float scaleMultiplier)
        {
            if (rectTransform == null || Mathf.Approximately(lastScaleMultiplier, scaleMultiplier))
            {
                return;
            }

            lastScaleMultiplier = scaleMultiplier;
            rectTransform.localScale = baseScale * scaleMultiplier;
            rectTransform.anchoredPosition = baseAnchoredPosition * scaleMultiplier;
        }
    }

    private sealed class CoverImageBinding
    {
        private readonly RectTransform rectTransform;
        private readonly Vector2 baseSize;
        private Vector2 lastRootSize;

        private CoverImageBinding(RectTransform rectTransform)
        {
            this.rectTransform = rectTransform;
            baseSize = rectTransform.sizeDelta;
        }

        public static CoverImageBinding TryCreate(Transform target)
        {
            var rectTransform = target as RectTransform;
            return rectTransform == null ? null : new CoverImageBinding(rectTransform);
        }

        public void Apply(Vector2 rootSize)
        {
            if (rectTransform == null || rootSize == lastRootSize || baseSize.x <= 0f || baseSize.y <= 0f)
            {
                return;
            }

            lastRootSize = rootSize;
            var scale = Mathf.Max(rootSize.x / baseSize.x, rootSize.y / baseSize.y);
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = baseSize * scale;
        }
    }

    private sealed class ContainedResponsiveUiBinding
    {
        private const float EdgePadding = 64f;
        private const float MaxScreenWidthRatio = 0.7f;
        private const float MaxScreenHeightRatio = 0.52f;

        private readonly RectTransform rectTransform;
        private readonly Vector3 baseScale;
        private readonly Vector2 baseAnchoredPosition;
        private readonly Vector2 baseSize;
        private float lastAppliedScale = -1f;
        private Vector2 lastRootSize;

        private ContainedResponsiveUiBinding(RectTransform rectTransform)
        {
            this.rectTransform = rectTransform;
            baseScale = rectTransform.localScale;
            baseAnchoredPosition = rectTransform.anchoredPosition;
            baseSize = rectTransform.sizeDelta;
        }

        public static ContainedResponsiveUiBinding TryCreate(Transform target)
        {
            var rectTransform = target as RectTransform;
            return rectTransform == null ? null : new ContainedResponsiveUiBinding(rectTransform);
        }

        public void Apply(float scaleMultiplier, Vector2 rootSize)
        {
            if (rectTransform == null || rootSize.x <= 0f || rootSize.y <= 0f)
            {
                return;
            }

            var containedScaleMultiplier = GetContainedScaleMultiplier(scaleMultiplier, rootSize);
            if (Mathf.Approximately(lastAppliedScale, containedScaleMultiplier) && rootSize == lastRootSize)
            {
                return;
            }

            lastAppliedScale = containedScaleMultiplier;
            lastRootSize = rootSize;
            rectTransform.localScale = baseScale * containedScaleMultiplier;
            rectTransform.anchoredPosition = baseAnchoredPosition * containedScaleMultiplier;
        }

        private float GetContainedScaleMultiplier(float requestedScaleMultiplier, Vector2 rootSize)
        {
            var basePixelSize = new Vector2(
                Mathf.Abs(baseSize.x * baseScale.x),
                Mathf.Abs(baseSize.y * baseScale.y)
            );

            var maxScaleX = GetAxisScaleLimit(rootSize.x, basePixelSize.x, Mathf.Abs(baseAnchoredPosition.x));
            var maxScaleY = GetAxisScaleLimit(rootSize.y, basePixelSize.y, Mathf.Abs(baseAnchoredPosition.y));
            var maxSizeScaleX = rootSize.x * MaxScreenWidthRatio / basePixelSize.x;
            var maxSizeScaleY = rootSize.y * MaxScreenHeightRatio / basePixelSize.y;
            return Mathf.Min(requestedScaleMultiplier, maxScaleX, maxScaleY, maxSizeScaleX, maxSizeScaleY);
        }

        private static float GetAxisScaleLimit(float rootLength, float baseLength, float baseCenterOffset)
        {
            var halfAvailableLength = Mathf.Max(1f, rootLength * 0.5f - EdgePadding);
            var baseHalfExtentFromCenter = baseCenterOffset + baseLength * 0.5f;
            return baseHalfExtentFromCenter <= 0f
                ? 1f
                : Mathf.Max(0.01f, halfAvailableLength / baseHalfExtentFromCenter);
        }
    }
}
