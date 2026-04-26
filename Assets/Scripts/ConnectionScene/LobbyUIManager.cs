using Unity.Netcode;
using UnityEngine;
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

    private void Awake()
    {
        EnsureConnectionWidgets();
    }

    public string GetClientAddress(string fallbackAddress)
    {
        EnsureConnectionWidgets();

        if (clientAddressInput == null)
        {
            return fallbackAddress;
        }

        var value = clientAddressInput.text.Trim();
        if (string.IsNullOrEmpty(value))
        {
            value = fallbackAddress;
            clientAddressInput.text = value;
        }

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

        clientAddressInput.text = value;
        SaveClientAddress(value);
    }

    public void ShowLobby(string statusMessage = "")
    {
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

        clientAddressInput = CreateInputField(
            "ClientAddressInput",
            connectionUI.transform,
            font,
            string.IsNullOrWhiteSpace(savedAddress) ? "192.168.0.10" : savedAddress,
            "Host IP",
            new Vector2(0f, -55f),
            new Vector2(280f, 36f)
        );

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

    private void SaveClientAddress(string value)
    {
        PlayerPrefs.SetString(clientAddressPrefsKey, value);
        PlayerPrefs.Save();
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
