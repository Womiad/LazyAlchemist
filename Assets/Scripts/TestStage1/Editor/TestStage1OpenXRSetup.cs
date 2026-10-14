#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

[InitializeOnLoad]
internal static class TestStage1OpenXRSetup
{
    private const string LoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string SettingsFolder = "Assets/XR/Settings";
    private const string SettingsPath = SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset";

    static TestStage1OpenXRSetup()
    {
        EditorApplication.delayCall += Configure;
    }

    private static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var perBuildTarget = GetOrCreatePerBuildTargetSettings();
        // Standalone is always the PC VR host. Android is intentionally not
        // changed here because it can be either a normal phone client or a
        // Quest build. LazyAlchemistBuildWindow selects the Android mode.
        ConfigureTarget(perBuildTarget, BuildTargetGroup.Standalone, true, false);
        AssetDatabase.SaveAssets();
    }

    internal static void ConfigureForBuildTarget(
        BuildTargetGroup targetGroup,
        bool enableXr,
        bool enableMetaQuestFeature)
    {
        var perBuildTarget = GetOrCreatePerBuildTargetSettings();
        ConfigureTarget(perBuildTarget, targetGroup, enableXr, enableMetaQuestFeature);
        AssetDatabase.SaveAssets();
    }

    private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                XRGeneralSettings.k_SettingsKey,
                out var settings)
            && settings != null)
        {
            return settings;
        }

        EnsureFolder("Assets", "XR");
        EnsureFolder("Assets/XR", "Settings");

        settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
        return settings;
    }

    private static void ConfigureTarget(
        XRGeneralSettingsPerBuildTarget perBuildTarget,
        BuildTargetGroup targetGroup,
        bool enableXr,
        bool enableMetaQuestFeature)
    {
        if (!perBuildTarget.HasSettingsForBuildTarget(targetGroup))
        {
            perBuildTarget.CreateDefaultSettingsForBuildTarget(targetGroup);
        }

        if (!perBuildTarget.HasManagerSettingsForBuildTarget(targetGroup))
        {
            perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(targetGroup);
        }

        var manager = perBuildTarget.ManagerSettingsForBuildTarget(targetGroup);
        manager.automaticLoading = enableXr;
        manager.automaticRunning = enableXr;

        var loaderAssigned = XRPackageMetadataStore.IsLoaderAssigned(LoaderTypeName, targetGroup);
        if (enableXr && !loaderAssigned)
        {
            XRPackageMetadataStore.AssignLoader(manager, LoaderTypeName, targetGroup);
        }
        else if (!enableXr && loaderAssigned)
        {
            XRPackageMetadataStore.RemoveLoader(manager, LoaderTypeName, targetGroup);
        }

        if (enableXr)
        {
            FeatureHelpers.RefreshFeatures(targetGroup);
            EnableFeature(targetGroup, OculusTouchControllerProfile.featureId);
        }

        if (enableMetaQuestFeature)
        {
            EnableFeature(targetGroup, MetaQuestFeature.featureId);
        }

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(perBuildTarget);
    }

    private static void EnableFeature(BuildTargetGroup targetGroup, string featureId)
    {
        var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(targetGroup, featureId);
        if (feature != null && !feature.enabled)
        {
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        var fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
