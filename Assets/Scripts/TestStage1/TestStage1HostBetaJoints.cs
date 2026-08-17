using Unity.Netcode;
using UnityEngine;

public class TestStage1HostBetaJoints : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static TestStage1HostBetaJoints Instance { get; private set; }

    [SerializeField] private string targetObjectName = "Beta_Joints";
    [SerializeField, Range(0f, 5f)] private float emissionIntensity = 1.75f;
    [SerializeField] private Renderer targetRenderer;

    private MaterialPropertyBlock propertyBlock;
    private Color currentColor = Color.white;
    private bool hasAppliedAnyPreset;

    private void Awake()
    {
        Instance = this;
        propertyBlock = new MaterialPropertyBlock();
        ResolveTargetRenderer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (IsHost())
        {
            ApplyPresetColor(TestStage1UI.EmptyElementColorIndex);
        }
    }

    private void Update()
    {
        if (IsHost() && !hasAppliedAnyPreset)
        {
            ApplyPresetColor(TestStage1UI.EmptyElementColorIndex);
        }
    }

    public void ApplyPresetColor(byte colorIndex)
    {
        if (!IsHost())
        {
            return;
        }

        ResolveTargetRenderer();
        if (targetRenderer == null)
        {
            Debug.LogWarning("TestStage1HostBetaJoints could not find Beta_Joints renderer.");
            return;
        }

        currentColor = GetPresetColor(colorIndex);
        currentColor.a = 1f;
        ApplyColor(currentColor);
        hasAppliedAnyPreset = true;
    }

    private void ResolveTargetRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        var targetObject = GameObject.Find(targetObjectName);
        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
        }
    }

    private void CaptureCurrentBaseColor()
    {
        ResolveTargetRenderer();
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            currentColor = Color.white;
            return;
        }

        var material = targetRenderer.sharedMaterial;
        if (material.HasProperty(BaseColorId))
        {
            currentColor = material.GetColor(BaseColorId);
        }
        else if (material.HasProperty(ColorId))
        {
            currentColor = material.GetColor(ColorId);
        }
        else
        {
            currentColor = Color.white;
        }

        ApplyColor(currentColor);
    }

    private void ApplyColor(Color color)
    {
        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetColor(EmissionColorId, color * emissionIntensity);
        targetRenderer.SetPropertyBlock(propertyBlock);

        foreach (var material in targetRenderer.materials)
        {
            if (material == null)
            {
                continue;
            }

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    private static Color GetPresetColor(byte colorIndex)
    {
        return colorIndex switch
        {
            0 => new Color(1f, 0.08f, 0.04f, 1f),
            1 => new Color(1f, 0.86f, 0.05f, 1f),
            2 => new Color(0.05f, 0.9f, 0.18f, 1f),
            3 => new Color(0.08f, 0.36f, 1f, 1f),
            TestStage1UI.EmptyElementColorIndex => Color.black,
            _ => Color.white
        };
    }

    private static bool IsHost()
    {
        return TestStage1DebugMode.IsHost;
    }
}
