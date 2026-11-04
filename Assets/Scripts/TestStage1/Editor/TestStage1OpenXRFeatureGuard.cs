using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.XR.OpenXR;

[InitializeOnLoad]
public sealed class TestStage1OpenXRFeatureGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    static TestStage1OpenXRFeatureGuard() => EditorApplication.delayCall += Apply;

    public void OnPreprocessBuild(BuildReport report) => Apply();

    private static void Apply()
    {
        // OpenXRFeature.enabled is immutable while an XR instance is running.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool changed = Configure(BuildTargetGroup.Standalone);
        changed |= Configure(BuildTargetGroup.Android);
        if (changed) AssetDatabase.SaveAssets();
    }

    private static bool Configure(BuildTargetGroup group)
    {
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) return false;

        bool changed = false;
        foreach (var feature in settings.GetFeatures())
        {
            if (feature == null) continue;
            string assemblyName = feature.GetType().Assembly.GetName().Name;
            string typeName = feature.GetType().FullName;
            bool isUnityTouchProfile = typeName ==
                "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile";

            if (isUnityTouchProfile && !feature.enabled)
            {
                feature.enabled = true;
                changed = true;
            }
            else if (assemblyName == "Oculus.VR" && feature.enabled)
            {
                feature.enabled = false;
                changed = true;
            }
        }

        if (changed) EditorUtility.SetDirty(settings);
        return changed;
    }
}
