using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CartoonCaveKitUrpMaterialFixer
{
    private const string RootFolder = "Assets/Cartoon Cave Kit";
    private const string CompletedKey = "LazyAlchemist.CartoonCaveKitUrpMaterialFixer.Completed";

    static CartoonCaveKitUrpMaterialFixer()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (SessionState.GetBool(CompletedKey, false))
            return;

        SessionState.SetBool(CompletedKey, true);
        ConvertMaterials();
    }

    [MenuItem("Tools/Lazy Alchemist/Fix Cartoon Cave Kit Materials")]
    private static void ConvertMaterials()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (litShader == null)
        {
            Debug.LogWarning("Cartoon Cave Kit URP material fixer skipped: URP Lit shader was not found.");
            return;
        }

        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { RootFolder });
        var changedCount = 0;

        foreach (var guid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Standard Assets effect materials use custom projector, image effect, flare, and toon shaders.
            // Those need manual replacement if used; the cave meshes themselves are handled below.
            if (path.Contains("/Standard Assets/"))
                continue;

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !ShouldConvert(material))
                continue;

            var mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var mainScale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
            var mainOffset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var isTransparent = IsTransparent(material, path);

            material.shader = isTransparent && unlitShader != null ? unlitShader : litShader;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", mainTexture);
                material.SetTextureScale("_BaseMap", mainScale);
                material.SetTextureOffset("_BaseMap", mainOffset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", mainTexture);
                material.SetTextureScale("_MainTex", mainScale);
                material.SetTextureOffset("_MainTex", mainOffset);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (isTransparent)
                ConfigureTransparent(material);
            else
                ConfigureOpaque(material);

            EditorUtility.SetDirty(material);
            changedCount++;
        }

        if (changedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"Cartoon Cave Kit URP material fixer converted {changedCount} material(s).");
        }
    }

    private static bool ShouldConvert(Material material)
    {
        var shaderName = material.shader != null ? material.shader.name : string.Empty;

        return string.IsNullOrEmpty(shaderName)
            || shaderName == "Hidden/InternalErrorShader"
            || shaderName == "Standard"
            || shaderName.StartsWith("Legacy Shaders/");
    }

    private static bool IsTransparent(Material material, string path)
    {
        if (path.Contains("/FX/"))
            return true;

        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") > 0.5f)
            return true;

        if (material.HasProperty("_Color") && material.GetColor("_Color").a < 0.99f)
            return true;

        return false;
    }

    private static void ConfigureOpaque(Material material)
    {
        SetFloatIfExists(material, "_Surface", 0f);
        SetFloatIfExists(material, "_Blend", 0f);
        SetFloatIfExists(material, "_AlphaClip", 0f);
        SetFloatIfExists(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfExists(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        SetFloatIfExists(material, "_ZWrite", 1f);

        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void ConfigureTransparent(Material material)
    {
        SetFloatIfExists(material, "_Surface", 1f);
        SetFloatIfExists(material, "_Blend", 0f);
        SetFloatIfExists(material, "_AlphaClip", 0f);
        SetFloatIfExists(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfExists(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfExists(material, "_ZWrite", 0f);

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private static void SetFloatIfExists(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }
}
