using Unity.Netcode;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    public LobbyUIManager uiManager;

    public void OnHost()
    {
        Debug.Log("Start Host");
        NetworkManager.Singleton.StartHost();
        StartCoroutine(WaitForConnection());
    }

    public void OnClient()
    {
        Debug.Log("Start Client");
        NetworkManager.Singleton.StartClient();
        StartCoroutine(WaitForConnection());
    }

    System.Collections.IEnumerator WaitForConnection()
    {
        // 等到真的連線成功
        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return null;
        }

        uiManager.ShowLobby();
    }
}