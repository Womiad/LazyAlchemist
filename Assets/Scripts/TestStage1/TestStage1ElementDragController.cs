using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestStage1ElementDragController : MonoBehaviour
{
    private const string ClientUiName = "ClientUI";

    private readonly SlotBinding[] slots =
    {
        new SlotBinding(
            "elements",
            "blank_right_ele",
            "elements_blank",
            new[]
            {
                new ItemBinding("ele_flame", 0),
                new ItemBinding("ele_land", 1),
                new ItemBinding("ele_wind", 2),
                new ItemBinding("ele_water", 3)
            },
            true
        ),
        new SlotBinding(
            "tools",
            "blank_right_tool",
            "tools",
            new[]
            {
                new ItemBinding("staff"),
                new ItemBinding("pickaxe")
            },
            false
        )
    };

    private readonly ObjectSlotBinding[] objectSlots =
    {
        new ObjectSlotBinding("slime_bait", "blank_left", "slime_bait", "slime_bait")
    };

    private Canvas canvas;
    private Transform clientUiRoot;
    private bool initialized;

    private void OnEnable()
    {
        TestStage1ItemInventoryController.InventoryChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        TestStage1ItemInventoryController.InventoryChanged -= OnInventoryChanged;
    }

    private void Update()
    {
        if (initialized)
        {
            TryInitializePendingObjectSlots();
            return;
        }

        TryInitialize();
    }

    public bool TryDrop(string slotName, string itemName, PointerEventData eventData)
    {
        var objectSlot = FindObjectSlot(slotName);
        if (objectSlot != null)
        {
            return TryDropObjectSlot(objectSlot, itemName, eventData);
        }

        var slot = FindSlot(slotName);
        if (slot == null
            || eventData == null
            || slot.DropZone == null
            || !slot.TryGetItem(itemName, out var item)
            || !IsPointerInside(slot.DropZone, eventData))
        {
            return false;
        }

        slot.ShowBlankItem(item.ObjectName);

        if (slot.SendHostColorChange && item.ColorIndex.HasValue)
        {
            TestStage1UI.RequestHostPresetColor(item.ColorIndex.Value);
        }

        return true;
    }

    public void TryRemoveBlankItem(string slotName, PointerEventData eventData)
    {
        var objectSlot = FindObjectSlot(slotName);
        if (objectSlot != null)
        {
            TryRemoveObjectSlot(objectSlot, eventData);
            return;
        }

        var slot = FindSlot(slotName);
        if (slot == null || slot.DropZone == null || eventData == null || IsPointerInside(slot.DropZone, eventData))
        {
            return;
        }

        slot.HideBlankItems();

        if (slot.SendHostColorChange)
        {
            TestStage1UI.RequestHostPresetColor(TestStage1UI.EmptyElementColorIndex);
        }
    }

    private void TryInitialize()
    {
        var clientUi = FindObjectInActiveScene(ClientUiName);
        if (clientUi == null)
        {
            return;
        }

        clientUiRoot = clientUi.transform;
        canvas = clientUi.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            canvas = clientUi.GetComponentInParent<Canvas>();
        }

        foreach (var slot in slots)
        {
            if (!TryInitializeSlot(clientUi.transform, slot))
            {
                return;
            }
        }

        initialized = true;
        TryInitializePendingObjectSlots();
    }

    private void TryInitializePendingObjectSlots()
    {
        if (clientUiRoot == null)
        {
            return;
        }

        foreach (var objectSlot in objectSlots)
        {
            if (!objectSlot.Initialized)
            {
                TryInitializeObjectSlot(clientUiRoot, objectSlot);
            }
        }
    }

    private void OnInventoryChanged()
    {
        if (!initialized || TestStage1ItemInventoryController.HasSlimeBait)
        {
            return;
        }

        foreach (var objectSlot in objectSlots)
        {
            objectSlot.HideBlankItem();
        }
    }

    private bool TryInitializeSlot(Transform clientUi, SlotBinding slot)
    {
        var dropZoneTransform = FindChildByName(clientUi, slot.DropZoneName);
        if (dropZoneTransform == null)
        {
            return false;
        }

        slot.DropZone = dropZoneTransform as RectTransform;
        if (slot.DropZone == null)
        {
            return false;
        }

        var blankContainer = FindChildByName(slot.DropZone, slot.BlankContainerName);
        if (blankContainer == null)
        {
            return false;
        }

        slot.CacheBlankItems(blankContainer);
        var sourceContainer = FindSourceContainer(clientUi, slot);
        if (sourceContainer == null)
        {
            return false;
        }

        foreach (var item in slot.Items)
        {
            var source = FindDirectChildByName(sourceContainer, item.ObjectName);
            if (source == null)
            {
                Debug.LogWarning($"TestStage1 source item is missing: {item.ObjectName}");
                return false;
            }

            SetupDragSource(source.gameObject, slot.SourceContainerName, item.ObjectName, false);
        }

        foreach (var blankObject in slot.BlankItemObjects)
        {
            SetupDragSource(blankObject, slot.SourceContainerName, blankObject.name, true);
        }

        slot.HideBlankItems();
        return true;
    }

    private bool TryInitializeObjectSlot(Transform clientUi, ObjectSlotBinding slot)
    {
        var dropZoneTransform = FindChildByName(clientUi, slot.DropZoneName);
        var sourceTransform = dropZoneTransform == null
            ? null
            : FindChildByNameOutsideParent(clientUi, slot.SourceObjectName, dropZoneTransform);
        var blankTransform = dropZoneTransform == null
            ? null
            : FindDirectChildByName(dropZoneTransform, slot.BlankObjectName);
        if (dropZoneTransform == null || sourceTransform == null)
        {
            return false;
        }

        slot.DropZone = dropZoneTransform as RectTransform;
        slot.SourceObject = sourceTransform.gameObject;
        slot.BlankObject = blankTransform == null ? null : blankTransform.gameObject;
        if (slot.DropZone == null || slot.BlankObject == null)
        {
            return false;
        }

        var sourceButton = slot.SourceObject.GetComponent<Button>();
        if (sourceButton != null)
        {
            sourceButton.interactable = true;
        }

        SetupDragSource(slot.SourceObject, slot.SourceContainerName, slot.ItemName, false);
        SetupDragSource(slot.BlankObject, slot.SourceContainerName, slot.ItemName, true);
        slot.HideBlankItem();
        slot.Initialized = true;
        return true;
    }

    private void SetupDragSource(GameObject source, string slotName, string itemName, bool isBlankItem)
    {
        var graphic = source.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }

        foreach (var childGraphic in source.GetComponentsInChildren<Graphic>(true))
        {
            if (childGraphic != null && childGraphic.gameObject != source)
            {
                childGraphic.raycastTarget = false;
            }
        }

        var dragSource = source.GetComponent<TestStage1ElementDragSource>();
        if (dragSource == null)
        {
            dragSource = source.AddComponent<TestStage1ElementDragSource>();
        }

        dragSource.Initialize(this, slotName, itemName, canvas, isBlankItem);
    }

    private Transform FindSourceContainer(Transform root, SlotBinding slot)
    {
        var containers = new List<Transform>();
        CollectChildrenByName(root, slot.SourceContainerName, containers);

        foreach (var container in containers)
        {
            if (container == slot.DropZone || container.IsChildOf(slot.DropZone))
            {
                continue;
            }

            var hasAllElements = true;
            foreach (var item in slot.Items)
            {
                if (FindDirectChildByName(container, item.ObjectName) == null)
                {
                    hasAllElements = false;
                    break;
                }
            }

            if (hasAllElements)
            {
                return container;
            }
        }

        return null;
    }

    private SlotBinding FindSlot(string slotName)
    {
        foreach (var slot in slots)
        {
            if (string.Equals(slot.SourceContainerName, slotName, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    private ObjectSlotBinding FindObjectSlot(string slotName)
    {
        foreach (var slot in objectSlots)
        {
            if (string.Equals(slot.SourceContainerName, slotName, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    private bool TryDropObjectSlot(ObjectSlotBinding slot, string itemName, PointerEventData eventData)
    {
        if (!TestStage1ItemInventoryController.HasSlimeBait
            || eventData == null
            || slot.DropZone == null
            || !string.Equals(slot.ItemName, itemName, StringComparison.Ordinal)
            || !IsPointerInside(slot.DropZone, eventData))
        {
            return false;
        }

        slot.ShowBlankItem();
        return true;
    }

    private void TryRemoveObjectSlot(ObjectSlotBinding slot, PointerEventData eventData)
    {
        if (slot.DropZone == null || eventData == null || IsPointerInside(slot.DropZone, eventData))
        {
            return;
        }

        slot.HideBlankItem();
    }

    private static Texture FindDisplayTexture(Transform itemRoot)
    {
        var rawImages = itemRoot.GetComponentsInChildren<RawImage>(true);
        foreach (var rawImage in rawImages)
        {
            if (rawImage != null && rawImage.texture != null)
            {
                return rawImage.texture;
            }
        }

        return null;
    }

    private static bool IsPointerInside(RectTransform rectTransform, PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    private static void CollectChildrenByName(Transform parent, string objectName, List<Transform> matches)
    {
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
        {
            matches.Add(parent);
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            CollectChildrenByName(parent.GetChild(i), objectName, matches);
        }
    }

    private static Transform FindDirectChildByName(Transform parent, string objectName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
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

    private static Transform FindChildByNameOutsideParent(Transform parent, string objectName, Transform excludedParent)
    {
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal)
            && (excludedParent == null || (parent != excludedParent && !parent.IsChildOf(excludedParent))))
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var match = FindChildByNameOutsideParent(parent.GetChild(i), objectName, excludedParent);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class SlotBinding
    {
        public readonly string SourceContainerName;
        public readonly string DropZoneName;
        public readonly string BlankContainerName;
        public readonly ItemBinding[] Items;
        public readonly bool SendHostColorChange;
        public readonly List<GameObject> BlankItemObjects = new List<GameObject>();

        public RectTransform DropZone;

        public SlotBinding(
            string sourceContainerName,
            string dropZoneName,
            string blankContainerName,
            ItemBinding[] items,
            bool sendHostColorChange)
        {
            SourceContainerName = sourceContainerName;
            DropZoneName = dropZoneName;
            BlankContainerName = blankContainerName;
            Items = items;
            SendHostColorChange = sendHostColorChange;
        }

        public void CacheBlankItems(Transform blankContainer)
        {
            BlankItemObjects.Clear();

            foreach (var item in Items)
            {
                var blankItem = FindDirectChildByName(blankContainer, item.ObjectName);
                if (blankItem == null)
                {
                    Debug.LogWarning($"TestStage1 blank item is missing: {item.ObjectName}");
                    continue;
                }

                BlankItemObjects.Add(blankItem.gameObject);
            }
        }

        public bool TryGetItem(string itemName, out ItemBinding item)
        {
            foreach (var candidate in Items)
            {
                if (string.Equals(candidate.ObjectName, itemName, StringComparison.Ordinal))
                {
                    item = candidate;
                    return true;
                }
            }

            item = default;
            return false;
        }

        public void HideBlankItems()
        {
            foreach (var itemObject in BlankItemObjects)
            {
                itemObject.SetActive(false);
            }
        }

        public void ShowBlankItem(string itemName)
        {
            foreach (var itemObject in BlankItemObjects)
            {
                itemObject.SetActive(string.Equals(itemObject.name, itemName, StringComparison.Ordinal));
            }
        }
    }

    private sealed class ObjectSlotBinding
    {
        public readonly string SourceContainerName;
        public readonly string DropZoneName;
        public readonly string ItemName;
        public readonly string SourceObjectName;
        public readonly string BlankObjectName;

        public RectTransform DropZone;
        public GameObject SourceObject;
        public GameObject BlankObject;
        public bool Initialized;

        public ObjectSlotBinding(
            string sourceContainerName,
            string dropZoneName,
            string itemName,
            string blankObjectName)
        {
            SourceContainerName = sourceContainerName;
            DropZoneName = dropZoneName;
            ItemName = itemName;
            SourceObjectName = itemName;
            BlankObjectName = blankObjectName;
        }

        public void HideBlankItem()
        {
            if (BlankObject == null)
            {
                return;
            }

            BlankObject.SetActive(false);
        }

        public void ShowBlankItem()
        {
            if (BlankObject == null)
            {
                return;
            }

            BlankObject.SetActive(true);
        }
    }

    private readonly struct ItemBinding
    {
        public readonly string ObjectName;
        public readonly byte? ColorIndex;

        public ItemBinding(string objectName, byte colorIndex)
        {
            ObjectName = objectName;
            ColorIndex = colorIndex;
        }

        public ItemBinding(string objectName)
        {
            ObjectName = objectName;
            ColorIndex = null;
        }
    }
}
