using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class TestStage1SlimeSmallController : MonoBehaviour
{
    private const float DefaultMaxHealth = 100f;

    [Header("Health")]
    [SerializeField] private float maxHealth = DefaultMaxHealth;
    [SerializeField] private float currentHealth = DefaultMaxHealth;
    [SerializeField] private TestStage1ElementPower elementPower = TestStage1ElementPower.None;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(120f, 14f);
    [SerializeField] private float healthBarScale = 0.01f;
    [SerializeField] private float healthBarAnimationSeconds = 1f;

    [Header("Damage Text")]
    [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 2.05f, 0f);
    [SerializeField] private float damageTextSeconds = 1f;
    [SerializeField] private float damageTextRiseDistance = 0.65f;
    [SerializeField] private int normalDamageTextSize = 36;
    [SerializeField] private int bigDamageTextSize = 58;

    [Header("Wander")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float wanderRadius = 4f;
    [SerializeField] private Vector2 waitBetweenMoves = new Vector2(0.8f, 2.2f);
    [SerializeField] private float targetReachedDistance = 0.25f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 4.5f;
    [SerializeField] private Vector2 jumpInterval = new Vector2(1.8f, 3.5f);
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckExtraDistance = 0.15f;

    private Rigidbody rb;
    private Collider bodyCollider;
    private Vector3 spawnPosition;
    private Vector3 wanderTarget;
    private float nextMoveDecisionTime;
    private float nextJumpTime;
    private Image healthFill;
    private RectTransform healthFillRect;
    private Canvas healthCanvas;
    private Coroutine healthBarAnimation;
    private bool isDying;
    private bool defeatedNotified;

    public TestStage1ElementPower ElementPower => elementPower;
    public static event System.Action<TestStage1SlimeSmallController> Defeated;

    private void Awake()
    {
        if (!IsPrimaryController())
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        bodyCollider = GetComponent<Collider>();

        if (bodyCollider == null)
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 0.9f;
            capsule.center = new Vector3(0f, 0.45f, 0f);
            bodyCollider = capsule;
        }

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        maxHealth = DefaultMaxHealth;
        currentHealth = maxHealth;
        elementPower = ResolveElementPower();
        spawnPosition = transform.position;
        PickNewWanderTarget();
        ScheduleNextJump();
        CreateHealthBar();
        UpdateHealthBar(false);
    }

    private void FixedUpdate()
    {
        if (currentHealth <= 0f)
        {
            return;
        }

        MoveTowardWanderTarget();
        TryJump();
    }

    private void LateUpdate()
    {
        FaceHealthBarToCamera();
    }

    public void TakeDamage(float amount)
    {
        if (amount < 0f || currentHealth <= 0f)
        {
            return;
        }

        ShowDamageText(amount);

        if (amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar(true);

        if (currentHealth <= 0f)
        {
            StartCoroutine(DieAfterHealthBarAnimation());
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthBar(true);
    }

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        UpdateHealthBar(true);

        if (currentHealth <= 0f)
        {
            StartCoroutine(DieAfterHealthBarAnimation());
        }
    }

    private void MoveTowardWanderTarget()
    {
        var currentPosition = rb.position;
        var flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);
        var flatTarget = new Vector3(wanderTarget.x, 0f, wanderTarget.z);
        var toTarget = flatTarget - flatCurrent;

        if (toTarget.sqrMagnitude <= targetReachedDistance * targetReachedDistance
            || Time.time >= nextMoveDecisionTime)
        {
            PickNewWanderTarget();
            return;
        }

        var direction = toTarget.normalized;
        var nextPosition = currentPosition + direction * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);

        if (direction.sqrMagnitude > 0.001f)
        {
            var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, lookRotation, 8f * Time.fixedDeltaTime));
        }
    }

    private void TryJump()
    {
        if (Time.time < nextJumpTime)
        {
            return;
        }

        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            ScheduleNextJump();
        }
        else
        {
            nextJumpTime = Time.time + 0.2f;
        }
    }

    private void PickNewWanderTarget()
    {
        var randomCircle = Random.insideUnitCircle * wanderRadius;
        wanderTarget = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
        nextMoveDecisionTime = Time.time + Random.Range(waitBetweenMoves.x, waitBetweenMoves.y);
    }

    private void ScheduleNextJump()
    {
        nextJumpTime = Time.time + Random.Range(jumpInterval.x, jumpInterval.y);
    }

    private bool IsGrounded()
    {
        var bounds = bodyCollider.bounds;
        var origin = bounds.center;
        var rayDistance = bounds.extents.y + groundCheckExtraDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void CreateHealthBar()
    {
        if (healthCanvas != null)
        {
            return;
        }

        var canvasObject = new GameObject("HealthBar");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = healthBarOffset;
        canvasObject.transform.localScale = Vector3.one * healthBarScale;

        healthCanvas = canvasObject.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        healthCanvas.sortingOrder = 20;

        var canvasRect = healthCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize;

        var background = CreateBarImage("Background", canvasObject.transform, new Color(0.15f, 0.04f, 0.04f, 0.85f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        healthFill = CreateBarImage("Fill", background.transform, new Color(0.25f, 0.9f, 0.32f, 0.95f));
        healthFillRect = healthFill.rectTransform;
        healthFillRect.anchorMin = Vector2.zero;
        healthFillRect.anchorMax = Vector2.one;
        healthFillRect.offsetMin = new Vector2(2f, 2f);
        healthFillRect.offsetMax = new Vector2(-2f, -2f);
    }

    private static Image CreateBarImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private void UpdateHealthBar(bool animate)
    {
        if (healthFillRect == null)
        {
            return;
        }

        var targetFill = maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        if (!animate || healthBarAnimationSeconds <= 0f)
        {
            SetHealthFillWidth(targetFill);
            return;
        }

        if (healthBarAnimation != null)
        {
            StopCoroutine(healthBarAnimation);
        }

        healthBarAnimation = StartCoroutine(AnimateHealthBar(targetFill));
    }

    private IEnumerator AnimateHealthBar(float targetFill)
    {
        var startFill = healthFillRect.anchorMax.x;
        var elapsed = 0f;

        while (elapsed < healthBarAnimationSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / healthBarAnimationSeconds);
            SetHealthFillWidth(Mathf.Lerp(startFill, targetFill, t));
            yield return null;
        }

        SetHealthFillWidth(targetFill);
        healthBarAnimation = null;
    }

    private void SetHealthFillWidth(float fill)
    {
        if (healthFillRect == null)
        {
            return;
        }

        fill = Mathf.Clamp01(fill);
        healthFillRect.anchorMax = new Vector2(fill, 1f);
        healthFillRect.offsetMax = new Vector2(-2f, -2f);
    }

    private void FaceHealthBarToCamera()
    {
        if (healthCanvas == null || Camera.main == null)
        {
            return;
        }

        healthCanvas.transform.rotation = Camera.main.transform.rotation;
    }

    private void Die()
    {
        if (!defeatedNotified)
        {
            defeatedNotified = true;
            Defeated?.Invoke(this);
        }

        gameObject.SetActive(false);
    }

    private IEnumerator DieAfterHealthBarAnimation()
    {
        if (isDying)
        {
            yield break;
        }

        isDying = true;
        yield return new WaitForSeconds(Mathf.Max(0f, healthBarAnimationSeconds));
        Die();
    }

    private void ShowDamageText(float amount)
    {
        StartCoroutine(ShowDamageTextRoutine(amount));
    }

    private IEnumerator ShowDamageTextRoutine(float amount)
    {
        var textObject = new GameObject("DamageText");
        textObject.transform.position = transform.position + damageTextOffset;

        var textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = "-" + Mathf.RoundToInt(amount);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = amount >= 35f ? bigDamageTextSize : normalDamageTextSize;
        textMesh.characterSize = 0.035f;
        textMesh.color = amount >= 35f
            ? new Color(1f, 0.92f, 0.2f, 1f)
            : Color.white;

        var startPosition = textObject.transform.position;
        var endPosition = startPosition + Vector3.up * damageTextRiseDistance;
        var elapsed = 0f;

        while (elapsed < damageTextSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / damageTextSeconds);

            textObject.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            FaceTransformToCamera(textObject.transform);

            var color = textMesh.color;
            color.a = 1f - t;
            textMesh.color = color;

            yield return null;
        }

        Destroy(textObject);
    }

    private static void FaceTransformToCamera(Transform target)
    {
        if (Camera.main == null)
        {
            return;
        }

        target.rotation = Camera.main.transform.rotation;
    }

    private bool IsPrimaryController()
    {
        var controllers = GetComponents<TestStage1SlimeSmallController>();
        return controllers.Length == 0 || controllers[0] == this;
    }

    private TestStage1ElementPower ResolveElementPower()
    {
        if (elementPower != TestStage1ElementPower.None)
        {
            return elementPower;
        }

        var slimeName = name;
        if (slimeName.IndexOf("flame", System.StringComparison.OrdinalIgnoreCase) >= 0
            || slimeName.IndexOf("fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Fire;
        }

        if (slimeName.IndexOf("land", System.StringComparison.OrdinalIgnoreCase) >= 0
            || slimeName.IndexOf("earth", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Land;
        }

        if (slimeName.IndexOf("wind", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Wind;
        }

        if (slimeName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Water;
        }

        return TestStage1ElementPower.None;
    }
}
