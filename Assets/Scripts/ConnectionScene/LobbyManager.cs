using Unity.Netcode;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance;

    public NetworkVariable<bool> clientReady = new NetworkVariable<bool>(false);

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    // 📱 Client 按 Ready
    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool value)
    {
        clientReady.Value = value;
        Debug.Log("Client Ready: " + value);
    }

    public bool IsClientReady()
    {
        return clientReady.Value;
    }

    // 🧠 Host 判斷能不能開始
    public bool CanStartGame()
    {
        return clientReady.Value;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (clientId != NetworkManager.ServerClientId)
        {
            clientReady.Value = false;
        }
    }
}
