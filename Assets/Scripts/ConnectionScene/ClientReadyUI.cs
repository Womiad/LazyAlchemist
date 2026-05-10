using UnityEngine;

public class ClientReadyUI : MonoBehaviour
{
    [SerializeField] private LobbyUIManager uiManager;

    bool isReady = false;

    public void OnClickReady()
    {
        isReady = !isReady;

        LobbyManager.Instance.SetReadyServerRpc(isReady);
        GetUiManager()?.ShowClientReadyFeedback(isReady);
    }

    private LobbyUIManager GetUiManager()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<LobbyUIManager>();
        }

        return uiManager;
    }
}
