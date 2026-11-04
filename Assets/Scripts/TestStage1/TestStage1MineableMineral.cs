using UnityEngine;
using UnityEngine.UI;

/// <summary>Two-hit physical pickaxe mining with a world-space health bar and replaceable hit audio.</summary>
public class TestStage1MineableMineral : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 2;
    [SerializeField, Min(.1f)] private float minimumPickaxeSpeed = .4f;
    [SerializeField, Min(.1f)] private float miningRadius = 1.25f;
    [SerializeField, Min(.05f)] private float hitCooldown = .75f;
    [Tooltip("Optional. Replace this clip in the prefab/Inspector with the final mining SFX.")]
    [SerializeField] private AudioClip hitClip;

    private TestStage1QuestElement element;
    private Transform miningHand;
    private Vector3 lastPickaxePosition;
    private float pickaxeSpeed;
    private bool hasLastPickaxePosition;
    private float nextHitTime;
    private int health;
    private Transform healthBar;
    private Image healthFill;
    private AudioSource audioSource;

    public TestStage1QuestElement Element => element;

    public void Configure(TestStage1QuestElement mineralElement) { element = mineralElement; health = maxHealth; BuildHealthBar(); }

    private void Awake()
    {
        health = maxHealth;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; audioSource.playOnAwake = false;
        BuildHealthBar();
    }

    private void Update()
    {
        if (miningHand == null) miningHand = FindMiningHand();
        if (miningHand != null && !hasLastPickaxePosition)
        {
            lastPickaxePosition = miningHand.position;
            hasLastPickaxePosition = true;
            return;
        }
        if (miningHand != null)
        {
            pickaxeSpeed = (miningHand.position - lastPickaxePosition).magnitude / Mathf.Max(Time.deltaTime, .001f);
            lastPickaxePosition = miningHand.position;
        }
        if (Time.time < nextHitTime) return;
        var bounds = GetBounds();
        var vrController = FindFirstObjectByType<TestStage1VRHostController>();
        var host = GameObject.Find("Host3D");
        Transform playerTransform = vrController != null ? vrController.transform : host?.transform;
        bool playerAtMineral = playerTransform != null
            && HorizontalSqrDistance(playerTransform.position, bounds.center) <= miningRadius * miningRadius;
        bool pickaxeWasShaken = miningHand != null && pickaxeSpeed >= minimumPickaxeSpeed;
        bool controllerButtonUsed = TestStage1VRInput.RightGrabHeld;
        bool deliberateButtonSwing = controllerButtonUsed && miningHand != null
            && pickaxeSpeed >= minimumPickaxeSpeed * .5f
            && HorizontalSqrDistance(miningHand.position, bounds.center) <= miningRadius * miningRadius;
        if (!playerAtMineral || (!pickaxeWasShaken && !deliberateButtonSwing)) return;
        Hit();
    }

    private void LateUpdate()
    {
        if (healthBar == null) return;
        var mineralBounds = GetBounds();
        healthBar.position = new Vector3(
            mineralBounds.center.x,
            mineralBounds.max.y + .25f,
            mineralBounds.center.z);
        var camera = Camera.main;
        if (camera != null)
            healthBar.rotation = Quaternion.LookRotation(camera.transform.position - healthBar.position, camera.transform.up);
    }

    private void Hit()
    {
        if (!HasPickaxeEquipped()) return;
        if (!HasElementAdvantage(TestStage1HandState.ElementPower, element)) return;
        nextHitTime = Time.time + hitCooldown;
        health = Mathf.Max(0, health - 1);
        if (hitClip != null) audioSource.PlayOneShot(hitClip);
        else audioSource.PlayOneShot(CreatePlaceholderClip());
        UpdateHealthBar();
        if (health > 0) return;
        gameObject.SetActive(false);
        TestStage1QuestSystem.ReportMineralMined(element);
    }

    private void BuildHealthBar()
    {
        if (healthFill != null) { UpdateHealthBar(); return; }
        var root = new GameObject("MiningHealthBar", typeof(RectTransform), typeof(Canvas));
        healthBar = root.transform; healthBar.SetParent(transform, false); healthBar.localPosition = Vector3.up * 1.5f;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 50;
        var rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(120f, 14f); rootRect.localScale = Vector3.one * .006f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        var backgroundRect = background.GetComponent<RectTransform>(); backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one; backgroundRect.offsetMin = Vector2.zero; backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.08f, .08f, .08f, .9f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        var fillRect = fill.GetComponent<RectTransform>(); fillRect.anchorMin = new Vector2(.03f, .2f); fillRect.anchorMax = new Vector2(.97f, .8f); fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        healthFill = fill.GetComponent<Image>(); healthFill.color = new Color(.15f, .85f, .25f, 1f); healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal;
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthFill == null) return;
        var ratio = Mathf.Clamp01(health / (float)Mathf.Max(1, maxHealth));
        healthFill.fillAmount = ratio;
    }

    private Bounds GetBounds()
    {
        // Particle renderers often have very large bounds and made mining trigger far
        // away from the visible crystal. Only use the actual mineral mesh bounds.
        var renderers = GetComponentsInChildren<Renderer>(); var result = new Bounds(transform.position, Vector3.one * .5f);
        var first = true; foreach (var r in renderers) { if (r is ParticleSystemRenderer || (healthBar != null && r.transform.IsChildOf(healthBar))) continue; if (first) { result = r.bounds; first = false; } else result.Encapsulate(r.bounds); }
        return result;
    }
    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }
    private static bool HasElementAdvantage(TestStage1ElementPower power, TestStage1QuestElement mineral)
    {
        return (power == TestStage1ElementPower.Fire && mineral == TestStage1QuestElement.Land)
            || (power == TestStage1ElementPower.Land && mineral == TestStage1QuestElement.Wind)
            || (power == TestStage1ElementPower.Wind && mineral == TestStage1QuestElement.Water)
            || (power == TestStage1ElementPower.Water && mineral == TestStage1QuestElement.Fire);
    }
    private static bool HasPickaxeEquipped()
    {
        if (TestStage1HandState.RightHandTool == TestStage1RightHandTool.Pickaxe) return true;
        foreach (var item in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!item.gameObject.activeInHierarchy || !string.Equals(item.name, "pickaxe", System.StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true)) if (renderer.enabled) return true;
        }
        return false;
    }
    private static Transform FindMiningHand()
    {
        // In VR the controller anchor is the reliable swing position. The visual
        // pickaxe has an authored pivot/offset and should not define gameplay range.
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.gameObject.activeInHierarchy && t.name == "VRRightControllerTrackingAnchor") return t;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.gameObject.activeInHierarchy
                && (t.name.IndexOf("pickaxe3D", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(t.name, "pickaxe", System.StringComparison.OrdinalIgnoreCase))) return t;
        return null;
    }
    private static AudioClip CreatePlaceholderClip()
    {
        const int sampleRate = 22050, count = 1800; var data = new float[count];
        for (var i = 0; i < count; i++) data[i] = Mathf.Sin(i * .38f) * Mathf.Exp(-i / 240f) * .2f;
        var clip = AudioClip.Create("MiningHit_Placeholder", count, 1, sampleRate, false); clip.SetData(data, 0); return clip;
    }
}
