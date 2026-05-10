using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private Coroutine startClientCoroutine;

    public void OnHost()
    {
        if (!TryGetTransport(out var transport))
        {
            return;
        }

        var localIpList = GetLocalIPv4Addresses().ToList();
        var localIps = localIpList.Count > 0 ? string.Join(", ", localIpList) : "No LAN IPv4 found";
        var primaryLocalIp = localIpList.Count > 0 ? localIpList[0] : clientConnectAddress;

        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        Debug.Log($"Start Host on port {port}");
        Debug.Log($"LAN IPs: {localIps}");

        clientConnectAddress = primaryLocalIp;
        uiManager?.SetClientAddress(clientConnectAddress);
        uiManager?.SetConnectionHint($"Host IP: {localIps}  Port: {port}");
        uiManager?.ShowConnectionScreen($"Hosting on {localIps}:{port}");

        NetworkManager.Singleton.StartHost();
        BeginWaitingForConnection();
    }

    public void OnClient()
    {
        if (startClientCoroutine != null)
        {
            StopCoroutine(startClientCoroutine);
        }

        startClientCoroutine = StartCoroutine(StartClientWithLatestAddress());
    }

    private IEnumerator StartClientWithLatestAddress()
    {
        if (!TryGetTransport(out var transport))
        {
            startClientCoroutine = null;
            yield break;
        }

        clientConnectAddress = uiManager != null
            ? uiManager.GetClientAddress(clientConnectAddress)
            : clientConnectAddress;

        if (string.IsNullOrWhiteSpace(clientConnectAddress))
        {
            uiManager?.ShowConnectionFailed("Host IP is empty.");
            startClientCoroutine = null;
            yield break;
        }

        yield return ShutdownActiveNetworkIfNeeded();

        if (!TryGetTransport(out transport))
        {
            startClientCoroutine = null;
            yield break;
        }

        transport.SetConnectionData(clientConnectAddress, port);
        Debug.Log($"Start Client -> {clientConnectAddress}:{port}");

        uiManager?.SetConnectionHint($"Client target: {clientConnectAddress}:{port}");
        uiManager?.ShowConnectionScreen($"Connecting to {clientConnectAddress}:{port}...");

        NetworkManager.Singleton.StartClient();
        BeginWaitingForConnection();
        startClientCoroutine = null;
    }

    private void OnDisable()
    {
        if (startClientCoroutine != null)
        {
            StopCoroutine(startClientCoroutine);
            startClientCoroutine = null;
        }

        UnregisterCallbacks();
    }

    private IEnumerator ShutdownActiveNetworkIfNeeded()
    {
        if (NetworkManager.Singleton == null)
        {
            yield break;
        }

        if (waitForConnectionCoroutine != null)
        {
            StopCoroutine(waitForConnectionCoroutine);
            waitForConnectionCoroutine = null;
        }

        UnregisterCallbacks();

        if (!NetworkManager.Singleton.IsClient
            && !NetworkManager.Singleton.IsServer
            && !NetworkManager.Singleton.IsListening
            && !NetworkManager.Singleton.ShutdownInProgress)
        {
            yield break;
        }

        if (!NetworkManager.Singleton.ShutdownInProgress)
        {
            NetworkManager.Singleton.Shutdown();
        }

        while (NetworkManager.Singleton != null
               && (NetworkManager.Singleton.IsClient
                   || NetworkManager.Singleton.IsServer
                   || NetworkManager.Singleton.IsListening
                   || NetworkManager.Singleton.ShutdownInProgress))
        {
            yield return null;
        }
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

        var modeLabel = NetworkManager.Singleton.IsHost ? "Host connected" : "Client connected";
        uiManager?.ShowLobby($"{modeLabel} on port {port}");
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
        var privateAddresses = new List<string>();
        var otherAddresses = new List<string>();

        foreach (var address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            {
                var addressString = address.ToString();

                if (IsPrivateLanAddress(address))
                {
                    privateAddresses.Add(addressString);
                }
                else
                {
                    otherAddresses.Add(addressString);
                }
            }
        }

        foreach (var address in privateAddresses)
        {
            yield return address;
        }

        foreach (var address in otherAddresses)
        {
            yield return address;
        }
    }

    private static bool IsPrivateLanAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (bytes.Length != 4)
        {
            return false;
        }

        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        return false;
    }
}
