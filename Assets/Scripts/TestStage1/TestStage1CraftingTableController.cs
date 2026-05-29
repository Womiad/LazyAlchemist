using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestStage1CraftingTableController : MonoBehaviour
{
    private const string ClientUiName = "ClientUI";
    private const string CraftingTableName = "craftingTable";
    private const string PageName = "craftingTable_page";
    private const string CloseButtonName = "X";

    private static readonly string[] InputBlankNames = { "blank1", "blank2", "blank3" };
    private static readonly string[] ItemButtonNames =
    {
        "ele_flame_btn",
        "ele_land_btn",
        "ele_wind_btn",
        "ele_water_btn",
        "firefly_btn"
    };

    private readonly List<Recipe> recipes = new List<Recipe>();
    private readonly Dictionary<string, Texture> itemTexturesById = new Dictionary<string, Texture>();
    private readonly string[] inputItems = new string[InputBlankNames.Length];
    private readonly RawImage[] inputBlanks = new RawImage[InputBlankNames.Length];
    private readonly Button[] inputBlankButtons = new Button[InputBlankNames.Length];
    private readonly List<ButtonRegistration> buttonRegistrations = new List<ButtonRegistration>();

    private GameObject page;
    private RawImage outputBlank;
    private Button outputButton;
    private Button openButton;
    private Button closeButton;
    private Recipe currentRecipe;
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
            openButton.onClick.RemoveListener(OpenPage);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePage);
        }

        foreach (var registration in buttonRegistrations)
        {
            if (registration.Button != null)
            {
                registration.Button.onClick.RemoveListener(registration.Action);
            }
        }

        buttonRegistrations.Clear();
    }

    private void TryInitialize()
    {
        var clientUi = FindObjectInActiveScene(ClientUiName);
        if (clientUi == null)
        {
            return;
        }

        var pageTransform = FindChildByName(clientUi.transform, PageName);
        var craftingTable = FindChildByName(clientUi.transform, CraftingTableName);
        if (pageTransform == null || craftingTable == null)
        {
            return;
        }

        page = pageTransform.gameObject;
        openButton = EnsureButton(craftingTable.gameObject);
        closeButton = FindButton(pageTransform, CloseButtonName);
        if (openButton == null || closeButton == null)
        {
            Debug.LogWarning("TestStage1 crafting table UI is missing craftingTable or X button setup.");
            return;
        }

        for (var i = 0; i < InputBlankNames.Length; i++)
        {
            var blank = FindChildByName(pageTransform, InputBlankNames[i]);
            if (blank == null || !TryGetRawImage(blank.gameObject, out inputBlanks[i]))
            {
                Debug.LogWarning($"TestStage1 crafting table is missing input blank: {InputBlankNames[i]}");
                return;
            }

            inputBlankButtons[i] = EnsureButton(blank.gameObject);
            inputBlankButtons[i].interactable = true;
            inputBlankButtons[i].targetGraphic = inputBlanks[i];
            var inputIndex = i;
            AddButtonListener(inputBlankButtons[i], () => RemoveInputItem(inputIndex));
        }

        var outputBlankTransform = FindChildByName(pageTransform, "blank4");
        if (outputBlankTransform == null || !TryGetRawImage(outputBlankTransform.gameObject, out outputBlank))
        {
            Debug.LogWarning("TestStage1 crafting table is missing output blank: blank4");
            return;
        }

        CacheNamedTextures(clientUi.transform);
        if (itemTexturesById.TryGetValue("slimeBait", out var slimeBaitTexture))
        {
            itemTexturesById["slime_bait"] = slimeBaitTexture;
        }
        else if (itemTexturesById.TryGetValue("slime_bait", out slimeBaitTexture))
        {
            itemTexturesById["slimeBait"] = slimeBaitTexture;
        }

        outputButton = EnsureButton(outputBlankTransform.gameObject);
        outputButton.interactable = true;
        outputButton.targetGraphic = outputBlank;
        AddButtonListener(outputButton, CollectRecipeResult);

        foreach (var buttonName in ItemButtonNames)
        {
            var itemButtonTransform = FindChildByName(pageTransform, buttonName);
            var itemButton = itemButtonTransform == null ? null : itemButtonTransform.GetComponent<Button>();
            if (itemButton == null)
            {
                Debug.LogWarning($"TestStage1 crafting table is missing item button: {buttonName}");
                return;
            }

            var itemId = GetItemIdFromButtonName(buttonName);
            itemTexturesById[itemId] = FindDisplayTexture(itemButtonTransform);
            itemButton.interactable = true;
            AddButtonListener(itemButton, () => AddInputItem(itemId));
        }

        recipes.Clear();
        recipes.AddRange(CreateRecipeBook());

        openButton.interactable = true;
        closeButton.interactable = true;
        AddButtonListener(openButton, OpenPage);
        AddButtonListener(closeButton, ClosePage);

        ClearCrafting();
        ClosePage();
        initialized = true;
    }

    private void OpenPage()
    {
        if (page != null)
        {
            page.SetActive(true);
        }
    }

    private void ClosePage()
    {
        if (page != null)
        {
            page.SetActive(false);
        }

        ClearCrafting();
    }

    private void AddInputItem(string itemId)
    {
        if (string.Equals(itemId, "firefly", StringComparison.Ordinal) && !TestStage1ItemInventoryController.HasFirefly)
        {
            return;
        }

        var targetIndex = Array.FindIndex(inputItems, string.IsNullOrEmpty);
        if (targetIndex < 0)
        {
            return;
        }

        inputItems[targetIndex] = itemId;
        SetBlankTexture(inputBlanks[targetIndex], GetTextureForItem(itemId));
        RefreshRecipeResult();
    }

    private void CollectRecipeResult()
    {
        if (currentRecipe == null)
        {
            return;
        }

        if (string.Equals(currentRecipe.OutputItemId, "slime_bait", StringComparison.Ordinal)
            || string.Equals(currentRecipe.OutputItemId, "slimeBait", StringComparison.Ordinal))
        {
            TestStage1ItemInventoryController.SetSlimeBaitAcquired(true);
        }

        ClearCrafting();
    }

    private void AddButtonListener(Button button, UnityAction action)
    {
        button.onClick.AddListener(action);
        buttonRegistrations.Add(new ButtonRegistration(button, action));
    }

    private void RemoveInputItem(int inputIndex)
    {
        if (inputIndex < 0 || inputIndex >= inputItems.Length || string.IsNullOrEmpty(inputItems[inputIndex]))
        {
            return;
        }

        for (var i = inputIndex; i < inputItems.Length - 1; i++)
        {
            inputItems[i] = inputItems[i + 1];
        }

        inputItems[inputItems.Length - 1] = null;
        RefreshInputBlanks();
        RefreshRecipeResult();
    }

    private void ClearCrafting()
    {
        for (var i = 0; i < inputItems.Length; i++)
        {
            inputItems[i] = null;
            SetBlankTexture(inputBlanks[i], null);
        }

        currentRecipe = null;
        SetBlankTexture(outputBlank, null);
    }

    private void RefreshInputBlanks()
    {
        for (var i = 0; i < inputItems.Length; i++)
        {
            SetBlankTexture(inputBlanks[i], GetTextureForItem(inputItems[i]));
        }
    }

    private void RefreshRecipeResult()
    {
        currentRecipe = FindMatchingRecipe();
        if (currentRecipe == null)
        {
            SetBlankTexture(outputBlank, null);
            return;
        }

        var outputTexture = GetTextureForItem(currentRecipe.OutputItemId);
        if (outputTexture == null)
        {
            Debug.LogWarning($"TestStage1 crafting recipe matched, but output texture is missing: {currentRecipe.OutputItemId}");
        }

        SetBlankTexture(outputBlank, outputTexture);
        ApplyRecipeReward(currentRecipe);
    }

    private static void ApplyRecipeReward(Recipe recipe)
    {
        if (recipe == null)
        {
            return;
        }

        if (string.Equals(recipe.OutputItemId, "slime_bait", StringComparison.Ordinal)
            || string.Equals(recipe.OutputItemId, "slimeBait", StringComparison.Ordinal))
        {
            TestStage1ItemInventoryController.SetSlimeBaitAcquired(true);
        }
    }

    private Recipe FindMatchingRecipe()
    {
        foreach (var recipe in recipes)
        {
            if (recipe.Matches(inputItems))
            {
                return recipe;
            }
        }

        return null;
    }

    private Texture GetTextureForItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && itemTexturesById.TryGetValue(itemId, out var texture)
            ? texture
            : null;
    }

    private static void SetBlankTexture(RawImage blank, Texture texture)
    {
        if (blank == null)
        {
            return;
        }

        blank.texture = texture;
        blank.color = texture == null ? Color.clear : Color.white;
        blank.raycastTarget = true;
    }

    private void CacheNamedTextures(Transform root)
    {
        var rawImages = root.GetComponentsInChildren<RawImage>(true);
        foreach (var rawImage in rawImages)
        {
            if (rawImage == null || rawImage.texture == null)
            {
                continue;
            }

            itemTexturesById[rawImage.gameObject.name] = rawImage.texture;
        }
    }

    private static Texture FindDisplayTexture(Transform itemButton)
    {
        var rawImages = itemButton.GetComponentsInChildren<RawImage>(true);
        foreach (var rawImage in rawImages)
        {
            if (rawImage != null && rawImage.texture != null)
            {
                return rawImage.texture;
            }
        }

        return null;
    }

    private static string GetItemIdFromButtonName(string buttonName)
    {
        return buttonName.EndsWith("_btn", StringComparison.Ordinal)
            ? buttonName.Substring(0, buttonName.Length - "_btn".Length)
            : buttonName;
    }

    private static IEnumerable<Recipe> CreateRecipeBook()
    {
        // Add future recipes here.
        // Inputs are order-insensitive.
        // Output id should match an existing RawImage object name in craftingTable_page
        // or one of the button item ids, so the controller can reuse that texture.
        //
        // Example:
        // yield return new Recipe("output_item_id", "input_a", "input_b", "input_c");
        yield return new Recipe("slime_bait", "firefly", "ele_wind", "ele_water");
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

    private static bool TryGetRawImage(GameObject target, out RawImage rawImage)
    {
        rawImage = target.GetComponent<RawImage>();
        if (rawImage != null)
        {
            return true;
        }

        rawImage = target.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        return true;
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

    private sealed class Recipe
    {
        public readonly string OutputItemId;
        private readonly string[] inputItemIds;

        public Recipe(string outputItemId, params string[] inputItemIds)
        {
            OutputItemId = outputItemId;
            this.inputItemIds = inputItemIds;
        }

        public bool Matches(string[] selectedInputItems)
        {
            if (selectedInputItems == null || selectedInputItems.Length != inputItemIds.Length)
            {
                return false;
            }

            var remainingInputs = new List<string>(selectedInputItems);
            foreach (var inputItemId in inputItemIds)
            {
                var matchIndex = remainingInputs.FindIndex(item =>
                    string.Equals(item, inputItemId, StringComparison.Ordinal));
                if (matchIndex < 0)
                {
                    return false;
                }

                remainingInputs.RemoveAt(matchIndex);
            }

            return true;
        }
    }

    private readonly struct ButtonRegistration
    {
        public readonly Button Button;
        public readonly UnityAction Action;

        public ButtonRegistration(Button button, UnityAction action)
        {
            Button = button;
            Action = action;
        }
    }
}
