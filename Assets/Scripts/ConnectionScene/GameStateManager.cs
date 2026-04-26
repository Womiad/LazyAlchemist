// using Unity.Netcode;
// using UnityEngine;

// public class LobbyManager : NetworkBehaviour
// {
//     public static LobbyManager Instance;

//     public NetworkVariable<bool> clientReady = new NetworkVariable<bool>(false);

//     void Awake()
//     {
//         Instance = this;
//     }

//     // Client 按 Ready
//     [ServerRpc(RequireOwnership = false)]
//     public void SetReadyServerRpc(bool value)
//     {
//         clientReady.Value = value;
//     }

//     public bool CanStartGame()
//     {
//         return clientReady.Value;
//     }
// }