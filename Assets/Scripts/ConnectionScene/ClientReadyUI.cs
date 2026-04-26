using UnityEngine;

public class ClientReadyUI : MonoBehaviour
{
    bool isReady = false;

    public void OnClickReady()
    {
        isReady = !isReady;

        LobbyManager.Instance.SetReadyServerRpc(isReady);
    }
}