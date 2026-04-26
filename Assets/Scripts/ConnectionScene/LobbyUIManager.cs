using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    public GameObject connectionUI;
    public GameObject hostUI;
    public GameObject clientUI;
    [SerializeField] private TMP_Text connectionStatusText;

    public void ShowLobby()
    {
        connectionUI.SetActive(false);
        SetStatus(string.Empty);

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

    public void ShowConnectionFailed(string message)
    {
        connectionUI.SetActive(true);
        hostUI.SetActive(false);
        clientUI.SetActive(false);
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (connectionStatusText == null)
        {
            return;
        }

        connectionStatusText.text = message;
    }
}
