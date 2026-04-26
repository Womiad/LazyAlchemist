using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostStartGameUI : MonoBehaviour
{
    public void OnClickStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (!LobbyManager.Instance.CanStartGame())
        {
            Debug.Log("Client 還沒 Ready");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            "TestPlayScene",
            LoadSceneMode.Single
        );
    }
}