using UnityEngine;
using System.Collections;

public class SlimeBounceCurve : MonoBehaviour
{
    [Header("Bounce Settings")]
    public float height = 0.5f;
    public float duration = 0.5f; // 上升+下降總時間

    [Header("Curve (像動畫關鍵幀)")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Timing")]
    public float repeatInterval = 1.5f;
    public float startDelay = 0f;

    private Vector3 originalLocalPos;

    void Start()
    {
        originalLocalPos = transform.localPosition;
        StartCoroutine(BounceLoop());
    }

    IEnumerator BounceLoop()
    {
        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            yield return new WaitForSeconds(repeatInterval);
            yield return StartCoroutine(PlayBounce());
        }
    }

    IEnumerator PlayBounce()
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 用曲線控制進度（關鍵！）
            float curveValue = curve.Evaluate(t);

            // 上升再下降（拋物線感）
            float yOffset = Mathf.Sin(curveValue * Mathf.PI) * height;

            transform.localPosition = originalLocalPos + Vector3.up * yOffset;

            yield return null;
        }

        transform.localPosition = originalLocalPos;
    }
}