using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class TestStage1SlimeSmallController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float currentHealth = 30f;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(120f, 14f);
    [SerializeField] private float healthBarScale = 0.01f;

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
    private Canvas healthCanvas;

    private void Awake()
    {
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

        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        spawnPosition = transform.position;
        PickNewWanderTarget();
        ScheduleNextJump();
        CreateHealthBar();
        UpdateHealthBar();
    }

    private void FixedUpdate()
    {
        MoveTowardWanderTarget();
        TryJump();
    }

    private void LateUpdate()
    {
        FaceHealthBarToCamera();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthBar();
    }

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
            Die();
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
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.rectTransform.anchorMin = Vector2.zero;
        healthFill.rectTransform.anchorMax = Vector2.one;
        healthFill.rectTransform.offsetMin = new Vector2(2f, 2f);
        healthFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
    }

    private static Image CreateBarImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private void UpdateHealthBar()
    {
        if (healthFill == null)
        {
            return;
        }

        healthFill.fillAmount = maxHealth <= 0f ? 0f : currentHealth / maxHealth;
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
        gameObject.SetActive(false);
    }
}
