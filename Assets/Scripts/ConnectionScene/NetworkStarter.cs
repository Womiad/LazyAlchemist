using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    [SerializeField] private LobbyUIManager uiManager;
    [SerializeField] private string clientConnectAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private float connectTimeoutSeconds = 8f;

    private Coroutine waitForConnectionCoroutine;

    public void OnHost()
    {
        if (!TryGetTransport(out var transport))
        {
            return;
        }

        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        Debug.Log($"Start Host on port {port}");
        Debug.Log($"LAN IPs: {string.Join(", ", GetLocalIPv4Addresses())}");
        NetworkManager.Singleton.StartHost();
        BeginWaitingForConnection();
    }

    public void OnClient()
    {
        if (!TryGetTransport(out var transport))
        {
            return;
        }

        transport.SetConnectionData(clientConnectAddress, port);
        Debug.Log($"Start Client -> {clientConnectAddress}:{port}");
        NetworkManager.Singleton.StartClient();
        BeginWaitingForConnection();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void BeginWaitingForConnection()
    {
        UnregisterCallbacks();
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
            return;
        }

        if (waitForConnectionCoroutine != null)
        {
            StopCoroutine(waitForConnectionCoroutine);
        }

        waitForConnectionCoroutine = StartCoroutine(WaitForConnection());
    }

    private IEnumerator WaitForConnection()
    {
        var elapsed = 0f;

        while (!NetworkManager.Singleton.IsConnectedClient && elapsed < connectTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
            yield break;
        }

        Debug.LogWarning($"Connection timed out after {connectTimeoutSeconds:F1}s.");
        uiManager?.ShowConnectionFailed(
            $"Connection timeout. Check host IP {clientConnectAddress} and port {port}."
        );

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
        }

        waitForConnectionCoroutine = null;
        UnregisterCallbacks();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        if (waitForConnectionCoroutine != null)
        {
            StopCoroutine(waitForConnectionCoroutine);
            waitForConnectionCoroutine = null;
        }

        UnregisterCallbacks();
        uiManager?.ShowLobby();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        if (waitForConnectionCoroutine != null)
        {
            StopCoroutine(waitForConnectionCoroutine);
            waitForConnectionCoroutine = null;
        }

        UnregisterCallbacks();
        uiManager?.ShowConnectionFailed(
            $"Disconnected from {clientConnectAddress}:{port}. Check host IP, Wi-Fi, and firewall."
        );
    }

    private void UnregisterCallbacks()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private bool TryGetTransport(out UnityTransport transport)
    {
        transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;

        if (transport != null)
        {
            return true;
        }

        Debug.LogError("UnityTransport not found on NetworkManager.");
        uiManager?.ShowConnectionFailed("UnityTransport not found on NetworkManager.");
        return false;
    }

    private static IEnumerable<string> GetLocalIPv4Addresses()
    {
        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName);

        foreach (var address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            {
                yield return address.ToString();
            }
        }
    }
}
