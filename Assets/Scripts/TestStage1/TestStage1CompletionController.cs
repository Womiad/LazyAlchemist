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
    private const string CompleteUiBackgroundName = "BG";
    private static readonly Vector2 CompleteUiDesignSize = new Vector2(2000f, 1500f);

    private readonly List<GameObject> completeUis = new List<GameObject>();
    private readonly List<CompleteUiBinding> completeUiBindings = new List<CompleteUiBinding>();
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
        if (completeUiVisible)
        {
            RefreshCompleteUiLayout();
        }
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

            RegisterCompleteUiBinding(completeUi);

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
        RefreshCompleteUiLayout();
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

        if (shouldShow)
        {
            RefreshCompleteUiLayout();
        }
    }

    private void RefreshCompleteUiLayout()
    {
        foreach (var binding in completeUiBindings)
        {
            binding.ApplyResponsiveScale(CompleteUiDesignSize);
        }
    }

    private void RegisterCompleteUiBinding(GameObject completeUi)
    {
        var rectTransform = completeUi.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        StretchToParent(rectTransform);
        completeUiBindings.Add(new CompleteUiBinding(rectTransform));
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
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
        TestStage1Bootstrap.ResetRuntimeState();

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

    private sealed class CompleteUiBinding
    {
        private readonly RectTransform root;
        private readonly RectTransform[] directChildren;
        private readonly Vector3[] baseScales;
        private readonly bool[] stretchChildren;
        private Vector2 lastRootSize;

        public CompleteUiBinding(RectTransform root)
        {
            this.root = root;
            directChildren = new RectTransform[root.childCount];
            baseScales = new Vector3[root.childCount];
            stretchChildren = new bool[root.childCount];

            for (var i = 0; i < root.childCount; i++)
            {
                directChildren[i] = root.GetChild(i) as RectTransform;
                baseScales[i] = directChildren[i] == null ? Vector3.one : directChildren[i].localScale;
                stretchChildren[i] = IsFullscreenBackground(directChildren[i]);

                if (stretchChildren[i])
                {
                    StretchToParent(directChildren[i]);
                }
            }
        }

        public void ApplyResponsiveScale(Vector2 designSize)
        {
            if (root == null || designSize.x <= 0f || designSize.y <= 0f)
            {
                return;
            }

            var rootSize = root.rect.size;
            if (rootSize == lastRootSize)
            {
                return;
            }

            lastRootSize = rootSize;
            var scale = Mathf.Min(rootSize.x / designSize.x, rootSize.y / designSize.y);
            scale = Mathf.Max(0.01f, scale);

            for (var i = 0; i < directChildren.Length; i++)
            {
                if (directChildren[i] != null)
                {
                    if (stretchChildren[i])
                    {
                        directChildren[i].localScale = Vector3.one;
                        StretchToParent(directChildren[i]);
                    }
                    else
                    {
                        directChildren[i].localScale = baseScales[i] * scale;
                    }
                }
            }
        }

        private static bool IsFullscreenBackground(RectTransform rectTransform)
        {
            return rectTransform != null
                && string.Equals(rectTransform.name, CompleteUiBackgroundName, StringComparison.Ordinal);
        }
    }
}
