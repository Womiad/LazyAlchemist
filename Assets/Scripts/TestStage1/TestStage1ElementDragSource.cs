using UnityEngine;
using UnityEngine.EventSystems;

public class TestStage1ElementDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private TestStage1ElementDragController controller;
    private RectTransform rectTransform;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Vector2 originalAnchoredPosition;
    private Vector3 originalLocalScale;
    private Quaternion originalLocalRotation;
    private int originalSiblingIndex;
    private string slotName;
    private string itemName;
    private bool isBlankItem;

    public void Initialize(
        TestStage1ElementDragController owner,
        string sourceSlotName,
        string sourceItemName,
        Canvas rootCanvas,
        bool sourceIsBlankItem)
    {
        controller = owner;
        slotName = sourceSlotName;
        itemName = sourceItemName;
        canvas = rootCanvas;
        isBlankItem = sourceIsBlankItem;
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null)
        {
            return;
        }

        originalParent = rectTransform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalLocalScale = rectTransform.localScale;
        originalLocalRotation = rectTransform.localRotation;

        if (canvas != null)
        {
            rectTransform.SetParent(canvas.transform, true);
            rectTransform.SetAsLastSibling();
        }

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null)
        {
            return;
        }

        var canvasTransform = canvas == null ? null : canvas.transform as RectTransform;
        if (canvasTransform != null
            && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var worldPoint))
        {
            rectTransform.position = worldPoint;
            return;
        }

        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller != null)
        {
            if (isBlankItem)
            {
                controller.TryRemoveBlankItem(slotName, eventData);
            }
            else
            {
                controller.TryDrop(slotName, itemName, eventData);
            }
        }

        RestoreOriginalTransform();
    }

    private void RestoreOriginalTransform()
    {
        if (rectTransform == null || originalParent == null)
        {
            return;
        }

        rectTransform.SetParent(originalParent, false);
        rectTransform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalAnchoredPosition;
        rectTransform.localScale = originalLocalScale;
        rectTransform.localRotation = originalLocalRotation;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }
}
