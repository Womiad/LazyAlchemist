using Unity.Netcode;
using UnityEngine;

public class ReadyButton : MonoBehaviour
{
    public void OnClickReady()
    {
        LobbyManager.Instance.SetReadyServerRpc(true);
    }
}