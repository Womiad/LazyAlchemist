using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestStage1BookUIController : MonoBehaviour
{
    private const string ClientUiName = "ClientUI";
    private const string BookIconName = "book_icon";
    private const string BookImagesName = "book_images";
    private const string PreviousButtonName = "<-";
    private const string NextButtonName = "->";
    private const string CloseButtonName = "X";

    private static readonly string[] PageNames = { "p1", "p2", "p3" };

    private GameObject bookImages;
    private GameObject[] pages;
    private Button openButton;
    private Button previousButton;
    private Button nextButton;
    private Button closeButton;
    private int pageIndex;
    private bool initialized;

    private void Update()
    {
        if (initialized)
        {
            return;
        }

        TryInitialize();
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenBook);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(PreviousPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextPage);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseBook);
        }
    }

    private void TryInitialize()
    {
        var clientUi = FindObjectInActiveScene(ClientUiName);
        if (clientUi == null)
        {
            return;
        }

        var bookIcon = FindChildByName(clientUi.transform, BookIconName);
        var bookImagesTransform = FindChildByName(clientUi.transform, BookImagesName);
        if (bookIcon == null || bookImagesTransform == null)
        {
            return;
        }

        bookImages = bookImagesTransform.gameObject;
        pages = new GameObject[PageNames.Length];

        for (var i = 0; i < PageNames.Length; i++)
        {
            var page = FindChildByName(bookImagesTransform, PageNames[i]);
            if (page == null)
            {
                Debug.LogWarning($"TestStage1 book UI is missing page object: {PageNames[i]}");
                return;
            }

            pages[i] = page.gameObject;
        }

        openButton = EnsureButton(bookIcon.gameObject);
        previousButton = FindButton(bookImagesTransform, PreviousButtonName);
        nextButton = FindButton(bookImagesTransform, NextButtonName);
        closeButton = FindButton(bookImagesTransform, CloseButtonName);

        if (openButton == null || previousButton == null || nextButton == null || closeButton == null)
        {
            Debug.LogWarning("TestStage1 book UI is missing book_icon, <-, ->, or X button setup.");
            return;
        }

        openButton.onClick.AddListener(OpenBook);
        previousButton.onClick.AddListener(PreviousPage);
        nextButton.onClick.AddListener(NextPage);
        closeButton.onClick.AddListener(CloseBook);

        initialized = true;
        pageIndex = 0;
        CloseBook();
    }

    private void OpenBook()
    {
        if (!initialized)
        {
            return;
        }

        pageIndex = 0;
        bookImages.SetActive(true);
        RefreshPages();
    }

    private void CloseBook()
    {
        if (bookImages != null)
        {
            bookImages.SetActive(false);
        }
    }

    private void PreviousPage()
    {
        if (!initialized)
        {
            return;
        }

        var previousPageIndex = pageIndex;
        pageIndex = Mathf.Max(0, pageIndex - 1);
        if (pageIndex != previousPageIndex)
        {
            TestStage1ClientUiAudio.PlayPageTurn();
        }

        RefreshPages();
    }

    private void NextPage()
    {
        if (!initialized)
        {
            return;
        }

        var previousPageIndex = pageIndex;
        pageIndex = Mathf.Min(pages.Length - 1, pageIndex + 1);
        if (pageIndex != previousPageIndex)
        {
            TestStage1ClientUiAudio.PlayPageTurn();
        }

        RefreshPages();
    }

    private void RefreshPages()
    {
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == pageIndex);
        }

        if (previousButton != null)
        {
            previousButton.interactable = pageIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = pageIndex < pages.Length - 1;
        }
    }

    private static Button FindButton(Transform parent, string objectName)
    {
        var target = FindChildByName(parent, objectName);
        return target == null ? null : target.GetComponent<Button>();
    }

    private static Button EnsureButton(GameObject target)
    {
        var button = target.GetComponent<Button>();
        if (button != null)
        {
            return button;
        }

        button = target.AddComponent<Button>();
        button.targetGraphic = target.GetComponent<Graphic>();
        return button;
    }

    private static GameObject FindObjectInActiveScene(string objectName)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            var match = FindChildByName(rootObject.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var match = FindChildByName(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
