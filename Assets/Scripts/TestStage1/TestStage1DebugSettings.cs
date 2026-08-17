using UnityEngine;

public enum TestStage1DebugRole
{
    Off,
    Host,
    Client
}

[CreateAssetMenu(menuName = "Lazy Alchemist/Test Stage 1 Debug Settings")]
public sealed class TestStage1DebugSettings : ScriptableObject
{
    public TestStage1DebugRole role = TestStage1DebugRole.Off;
    public string stageScene = "TestStage1_VR";
}
