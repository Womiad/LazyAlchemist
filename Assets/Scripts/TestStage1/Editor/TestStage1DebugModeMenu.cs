#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

internal static class TestStage1DebugModeMenu
{
    private const string SettingsPath = "Assets/Resources/TestStage1DebugSettings.asset";
    private const string MenuRoot = "Lazy Alchemist/Debug Mode/";

    [MenuItem(MenuRoot + "Off", false, 100)]
    private static void SelectOff() => SetRole(TestStage1DebugRole.Off);

    [MenuItem(MenuRoot + "Host", false, 101)]
    private static void SelectHost() => SetRole(TestStage1DebugRole.Host);

    [MenuItem(MenuRoot + "Client", false, 102)]
    private static void SelectClient() => SetRole(TestStage1DebugRole.Client);

    [MenuItem(MenuRoot + "Off", true)]
    private static bool ValidateOff() => SetCheckmark(TestStage1DebugRole.Off);

    [MenuItem(MenuRoot + "Host", true)]
    private static bool ValidateHost() => SetCheckmark(TestStage1DebugRole.Host);

    [MenuItem(MenuRoot + "Client", true)]
    private static bool ValidateClient() => SetCheckmark(TestStage1DebugRole.Client);

    private static void SetRole(TestStage1DebugRole role)
    {
        var settings = AssetDatabase.LoadAssetAtPath<TestStage1DebugSettings>(SettingsPath);
        if (settings == null)
        {
            Debug.LogError($"Missing debug settings asset at {SettingsPath}.");
            return;
        }

        Undo.RecordObject(settings, "Change TestStage1 Debug Role");
        settings.role = role;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"TestStage1 debug role set to {role}.");
    }

    private static bool SetCheckmark(TestStage1DebugRole role)
    {
        var settings = AssetDatabase.LoadAssetAtPath<TestStage1DebugSettings>(SettingsPath);
        Menu.SetChecked(MenuRoot + role, settings != null && settings.role == role);
        return true;
    }
}
#endif
