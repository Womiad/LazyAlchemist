using System;

public enum TestStage1GameMode : byte
{
    Countdown = 0,
    QuestCount = 1
}

/// <summary>Settings selected by the host in the connection scene. Static by design so scene changes preserve them.</summary>
public static class TestStage1GameSettings
{
    public static TestStage1GameMode Mode { get; private set; } = TestStage1GameMode.QuestCount;
    public static float CountdownSeconds { get; private set; } = 600f;
    public static int RequiredQuestCount { get; private set; } = 5;

    public static void ConfigureCountdown(float seconds)
    {
        Mode = TestStage1GameMode.Countdown;
        CountdownSeconds = Math.Max(10f, seconds);
    }

    public static void ConfigureQuestCount(int count)
    {
        Mode = TestStage1GameMode.QuestCount;
        RequiredQuestCount = Math.Max(1, count);
    }
}
