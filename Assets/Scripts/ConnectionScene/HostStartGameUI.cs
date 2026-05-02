using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostStartGameUI : MonoBehaviour
{
    public string sceneToLoad = "TestStage1";
    public void OnClickStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (!LobbyManager.Instance.CanStartGame())
        {
            Debug.Log("Client 還沒 Ready");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneToLoad,
            LoadSceneMode.Single
        );
    }
}