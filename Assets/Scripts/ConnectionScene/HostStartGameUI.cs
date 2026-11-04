using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostStartGameUI : MonoBehaviour
{
    [SerializeField] private LobbyUIManager uiManager;
    public string sceneToLoad = "TestStage1_VR";
    [SerializeField] private TestStage1GameMode gameMode = TestStage1GameMode.QuestCount;
    [SerializeField, Min(10f)] private float countdownSeconds = 600f;
    [SerializeField, Min(1)] private int requiredQuestCount = 5;
    private string countdownMinutesText = "10";
    private string questCountText = "5";

    public void SetCountdownMode(bool enabled) { if (enabled) gameMode = TestStage1GameMode.Countdown; }
    public void SetQuestMode(bool enabled) { if (enabled) gameMode = TestStage1GameMode.QuestCount; }
    public void SetCountdownMinutes(float minutes) { countdownSeconds = Mathf.Max(10f, minutes * 60f); }
    public void SetRequiredQuestCount(float count) { requiredQuestCount = Mathf.Max(1, Mathf.RoundToInt(count)); }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost
            || NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return;
        var rect = new Rect(24f, 24f, 300f, 180f);
        GUI.Box(rect, "遊戲模式設定");
        if (GUI.Toggle(new Rect(42f, 55f, 120f, 28f), gameMode == TestStage1GameMode.Countdown, "倒數模式")) gameMode = TestStage1GameMode.Countdown;
        if (GUI.Toggle(new Rect(170f, 55f, 120f, 28f), gameMode == TestStage1GameMode.QuestCount, "任務模式")) gameMode = TestStage1GameMode.QuestCount;
        GUI.Label(new Rect(42f, 92f, 125f, 25f), "倒數分鐘：");
        countdownMinutesText = GUI.TextField(new Rect(170f, 90f, 100f, 25f), countdownMinutesText);
        GUI.Label(new Rect(42f, 126f, 125f, 25f), "完成任務數：");
        questCountText = GUI.TextField(new Rect(170f, 124f, 100f, 25f), questCountText);
        if (float.TryParse(countdownMinutesText, out var minutes)) SetCountdownMinutes(minutes);
        if (int.TryParse(questCountText, out var quests)) SetRequiredQuestCount(quests);
    }

    public void OnClickStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (gameMode == TestStage1GameMode.Countdown) TestStage1GameSettings.ConfigureCountdown(countdownSeconds);
        else TestStage1GameSettings.ConfigureQuestCount(requiredQuestCount);

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
