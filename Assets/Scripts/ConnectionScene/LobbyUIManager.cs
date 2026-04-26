using Unity.Netcode;
using UnityEngine;

public class LobbyUIManager : MonoBehaviour
{
    public GameObject connectionUI;
    public GameObject hostUI;
    public GameObject clientUI;

    public void ShowLobby()
    {
        connectionUI.SetActive(false);

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
}