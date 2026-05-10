using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostStartGameUI : MonoBehaviour
{
    [SerializeField] private LobbyUIManager uiManager;
    public string sceneToLoad = "TestStage1";

    public void OnClickStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (!LobbyManager.Instance.CanStartGame())
        {
            Debug.Log("Client 還沒 Ready");
            GetUiManager()?.ShowStartBlockedByClientReady();
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneToLoad,
            LoadSceneMode.Single
        );
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
