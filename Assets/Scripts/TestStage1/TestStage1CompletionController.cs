using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestStage1CompletionController : MonoBehaviour
{
    private const string CompleteUiName = "CompleteUI";
    private const string ConnectionSceneName = "ConnectionScene";

    private readonly List<GameObject> completeUis = new List<GameObject>();
    private readonly List<Button> homeButtons = new List<Button>();
    private bool initialized;
    private bool subscribed;
    private bool completeUiVisible;

    private void OnEnable()
    {
        SubscribeTaskEvents();
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
        }

        RefreshCompleteUi();
    }

    private void OnDisable()
    {
        UnsubscribeTaskEvents();
        RemoveButtonListeners();
    }

    private void OnDestroy()
    {
        UnsubscribeTaskEvents();
        RemoveButtonListeners();
    }

    private void TryInitialize()
    {
        CollectCompleteUis();
        if (completeUis.Count == 0)
        {
            return;
        }

        foreach (var completeUi in completeUis)
        {
            if (completeUi == null)
            {
                continue;
            }

            var buttons = completeUi.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null || homeButtons.Contains(button))
                {
                    continue;
                }

                button.onClick.AddListener(ReturnToConnectionScene);
                homeButtons.Add(button);
            }

            completeUi.SetActive(false);
        }

        initialized = true;
        completeUiVisible = false;
        RefreshCompleteUi();
    }

    private void CollectCompleteUis()
    {
        completeUis.Clear();
        var scene = SceneManager.GetActiveScene();
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            CollectCompleteUis(rootObject.transform);
        }
    }

    private void CollectCompleteUis(Transform parent)
    {
        if (string.Equals(parent.name, CompleteUiName, StringComparison.Ordinal))
        {
            completeUis.Add(parent.gameObject);
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            CollectCompleteUis(parent.GetChild(i));
        }
    }

    private void RefreshCompleteUi()
    {
        if (!initialized)
        {
            return;
        }

        var shouldShow = TestStage1FireflyCollectionController.IsCompleted
            && TestStage1WaterCrystalCollectionController.IsCompleted
            && TestStage1SlimeHuntController.IsCompleted;

        if (completeUiVisible == shouldShow)
        {
            return;
        }

        completeUiVisible = shouldShow;
        foreach (var completeUi in completeUis)
        {
            if (completeUi != null)
            {
                completeUi.SetActive(shouldShow);
            }
        }
    }

    private void SubscribeTaskEvents()
    {
        if (subscribed)
        {
            return;
        }

        TestStage1FireflyCollectionController.CompletedChanged += OnTaskCompletedChanged;
        TestStage1WaterCrystalCollectionController.CompletedChanged += OnTaskCompletedChanged;
        TestStage1SlimeHuntController.CompletedChanged += OnTaskCompletedChanged;
        subscribed = true;
    }

    private void UnsubscribeTaskEvents()
    {
        if (!subscribed)
        {
            return;
        }

        TestStage1FireflyCollectionController.CompletedChanged -= OnTaskCompletedChanged;
        TestStage1WaterCrystalCollectionController.CompletedChanged -= OnTaskCompletedChanged;
        TestStage1SlimeHuntController.CompletedChanged -= OnTaskCompletedChanged;
        subscribed = false;
    }

    private void OnTaskCompletedChanged(bool isCompleted)
    {
        RefreshCompleteUi();
    }

    private void RemoveButtonListeners()
    {
        foreach (var button in homeButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(ReturnToConnectionScene);
            }
        }

        homeButtons.Clear();
    }

    private void ReturnToConnectionScene()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && networkManager.IsServer)
        {
            networkManager.SceneManager.LoadScene(ConnectionSceneName, LoadSceneMode.Single);
            return;
        }

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        SceneManager.LoadScene(ConnectionSceneName, LoadSceneMode.Single);
    }
}
