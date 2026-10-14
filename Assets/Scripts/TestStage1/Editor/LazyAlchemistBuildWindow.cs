#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
internal sealed class LazyAlchemistBuildWindow : EditorWindow
{
    private const string PhoneBuildAndRunRequestPath = "Temp/LazyAlchemist.PhoneBuildAndRun.request";

    private enum BuildProfile
    {
        AndroidPhoneClient,
        MetaQuestVr,
        WindowsPcVrHost
    }

    private BuildProfile profile = BuildProfile.AndroidPhoneClient;
    private bool developmentBuild;

    static LazyAlchemistBuildWindow()
    {
        EditorApplication.delayCall += TryRunRequestedBuild;
    }

    private static void TryRunRequestedBuild()
    {
        if (!File.Exists(PhoneBuildAndRunRequestPath))
        {
            return;
        }

        File.Delete(PhoneBuildAndRunRequestPath);
        Build(BuildProfile.AndroidPhoneClient, false, true);
    }

    [MenuItem("Tools/Lazy Alchemist/Build Profiles...")]
    private static void Open()
    {
        var window = GetWindow<LazyAlchemistBuildWindow>(true, "Lazy Alchemist Build", true);
        window.minSize = new Vector2(430f, 245f);
        window.Show();
    }

    [MenuItem("Tools/Lazy Alchemist/Build/Android Phone Client")]
    private static void BuildPhoneFromMenu() => Build(BuildProfile.AndroidPhoneClient, false, false);

    public static void BuildAndroidPhoneAndRunCommandLine()
    {
        Build(BuildProfile.AndroidPhoneClient, false, true);
    }

    [MenuItem("Tools/Lazy Alchemist/Build/Meta Quest VR")]
    private static void BuildQuestFromMenu() => Build(BuildProfile.MetaQuestVr, false, false);

    [MenuItem("Tools/Lazy Alchemist/Build/Windows PC VR Host")]
    private static void BuildPcVrFromMenu() => Build(BuildProfile.WindowsPcVrHost, false, false);

    private void OnGUI()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Build Profile", EditorStyles.boldLabel);
        profile = (BuildProfile)EditorGUILayout.EnumPopup("Target", profile);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(GetProfileDescription(profile), MessageType.Info);

        EditorGUILayout.Space(8f);
        developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);

        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Build", GUILayout.Height(34f)))
            {
                Build(profile, developmentBuild, false);
            }

            if (GUILayout.Button("Build And Run", GUILayout.Height(34f)))
            {
                Build(profile, developmentBuild, true);
            }
        }
        EditorGUILayout.Space(10f);
    }

    private static string GetProfileDescription(BuildProfile selectedProfile)
    {
        switch (selectedProfile)
        {
            case BuildProfile.AndroidPhoneClient:
                return "Android 手機 Client：關閉 OpenXR，輸出一般 APK。";
            case BuildProfile.MetaQuestVr:
                return "Meta Quest VR：啟用 Android OpenXR 與 Meta Quest 支援，輸出 Quest APK。";
            case BuildProfile.WindowsPcVrHost:
                return "Windows PC VR Host：啟用 Standalone OpenXR，輸出 Windows x64 執行檔。";
            default:
                throw new ArgumentOutOfRangeException(nameof(selectedProfile), selectedProfile, null);
        }
    }

    private static void Build(BuildProfile selectedProfile, bool development, bool autoRun)
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Cannot Build", "Unity 正在編譯腳本，請等編譯完成後再試。", "OK");
            return;
        }

        var isPc = selectedProfile == BuildProfile.WindowsPcVrHost;
        var target = isPc ? BuildTarget.StandaloneWindows64 : BuildTarget.Android;
        var targetGroup = isPc ? BuildTargetGroup.Standalone : BuildTargetGroup.Android;
        var enableXr = selectedProfile != BuildProfile.AndroidPhoneClient;
        var enableQuest = selectedProfile == BuildProfile.MetaQuestVr;

        TestStage1OpenXRSetup.ConfigureForBuildTarget(targetGroup, enableXr, enableQuest);

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
        {
            EditorUtility.DisplayDialog("Build Failed", $"無法切換 Build Target 到 {target}。", "OK");
            return;
        }

        if (!isPc)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        var outputPath = GetOutputPath(selectedProfile);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds");

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build Failed", "Build Settings 中沒有啟用的場景。", "OK");
            return;
        }

        var options = BuildOptions.CompressWithLz4HC;
        if (development)
        {
            options |= BuildOptions.Development;
        }
        if (autoRun)
        {
            options |= BuildOptions.AutoRunPlayer;
        }

        AssetDatabase.SaveAssets();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            targetGroup = targetGroup,
            options = options
        });

        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB)");
            EditorUtility.RevealInFinder(outputPath);
        }
        else
        {
            Debug.LogError($"Build failed: {summary.result}, errors: {summary.totalErrors}");
        }
    }

    private static string GetOutputPath(BuildProfile selectedProfile)
    {
        switch (selectedProfile)
        {
            case BuildProfile.AndroidPhoneClient:
                return "Builds/AndroidPhone/LazyAlchemist_Client.apk";
            case BuildProfile.MetaQuestVr:
                return "Builds/MetaQuest/LazyAlchemist_Quest.apk";
            case BuildProfile.WindowsPcVrHost:
                return "Builds/WindowsPCVR/LazyAlchemist_PC_VR.exe";
            default:
                throw new ArgumentOutOfRangeException(nameof(selectedProfile), selectedProfile, null);
        }
    }
}
#endif
