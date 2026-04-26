using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameButton : MonoBehaviour
{
    public void OnClickStart()
    {
        if (LobbyManager.Instance.CanStartGame())
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                "GameScene",
                LoadSceneMode.Single
            );
        }
    }
}