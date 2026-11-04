using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum TestStage1QuestKind : byte { Firefly, Mining, Hunt }
public enum TestStage1QuestElement : byte { Any, Fire, Land, Wind, Water }

public class TestStage1QuestSystem : MonoBehaviour
{
    private const int Count = 3;
    private const string StateMessage = "TestStage1.QuestState.v3";
    private const string SelectMessage = "TestStage1.SelectQuest.v3";
    private static readonly string[] TaskNames = { "Task1", "Task2", "Task3" };
    private struct Quest { public TestStage1QuestKind Kind; public TestStage1QuestElement Element; public int Progress, Target; public float StartedAt; }
    private static readonly Quest[] quests = new Quest[Count];

    public static event Action QuestChanged, RunCompleted;
    public static int ActiveQuestIndex { get; private set; }
    public static TestStage1QuestKind CurrentKind => quests[ActiveQuestIndex].Kind;
    public static TestStage1QuestElement CurrentElement => quests[ActiveQuestIndex].Element;
    public static int CurrentProgress => quests[ActiveQuestIndex].Progress;
    public static int CurrentTarget => quests[ActiveQuestIndex].Target;
    public static int CompletedQuestCount { get; private set; }
    public static int CollectionQuestCount { get; private set; }
    public static int HuntQuestCount { get; private set; }
    public static float CollectionTotalSeconds { get; private set; }
    public static float HuntTotalSeconds { get; private set; }
    public static float ElapsedSeconds { get; private set; }
    public static float RemainingSeconds { get; private set; }
    public static bool IsRunComplete { get; private set; }
    public static bool HasActiveQuest { get; private set; }

    private readonly List<TMP_Text> texts = new();
    private readonly List<Button> buttons = new();
    private bool initialized, stateRegistered, selectRegistered, connectionRegistered;
    private float nextSync;
    private AudioSource completionAudioSource;
    private static AudioClip completionClip;
    private static int lastCountedSlimeId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ResetRuntimeState(); QuestChanged = null; RunCompleted = null; }
    public static void ResetRuntimeState()
    {
        Array.Clear(quests, 0, quests.Length); ActiveQuestIndex = 0; lastCountedSlimeId = 0;
        CompletedQuestCount = CollectionQuestCount = HuntQuestCount = 0;
        CollectionTotalSeconds = HuntTotalSeconds = ElapsedSeconds = 0f;
        RemainingSeconds = TestStage1GameSettings.CountdownSeconds; IsRunComplete = HasActiveQuest = false;
    }

    private void Update()
    {
        RegisterNetwork(); if (!initialized) Initialize();
        if (!initialized || IsRunComplete || !IsAuthority()) return;
        ElapsedSeconds += Time.deltaTime;
        if (TestStage1GameSettings.Mode == TestStage1GameMode.Countdown)
        { RemainingSeconds = Mathf.Max(0, TestStage1GameSettings.CountdownSeconds - ElapsedSeconds); if (RemainingSeconds <= 0) CompleteRun(); }
        if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + 1; Broadcast(); }
    }
    private void OnDestroy()
    {
        var n = NetworkManager.Singleton; if (n == null) return;
        if (stateRegistered) n.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
        if (selectRegistered) n.CustomMessagingManager.UnregisterNamedMessageHandler(SelectMessage);
        if (connectionRegistered) n.OnClientConnectedCallback -= OnConnected;
    }
    private void Initialize()
    {
        var board = FindObject("taskboard"); if (board == null) return;
        for (int i = 0; i < Count; i++)
        {
            var child = FindChild(board.transform, TaskNames[i]); var text = child == null ? null : child.GetComponent<TMP_Text>(); if (text == null) return;
            texts.Add(text); var button = text.GetComponent<Button>() ?? text.gameObject.AddComponent<Button>(); button.targetGraphic = text;
            int index = i; button.onClick.AddListener(() => SelectQuest(index)); buttons.Add(button);
        }
        initialized = true;
        if (IsAuthority() && !HasActiveQuest) { for (int i = 0; i < Count; i++) quests[i] = Create((TestStage1QuestKind)i); HasActiveQuest = true; Broadcast(); QuestChanged?.Invoke(); }
        RefreshBoard();
    }
    private void RegisterNetwork()
    {
        var n = NetworkManager.Singleton; if (n == null) return;
        if (n.IsClient && !stateRegistered) { n.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState); stateRegistered = true; }
        if (n.IsServer && !selectRegistered) { n.CustomMessagingManager.RegisterNamedMessageHandler(SelectMessage, ReceiveSelection); selectRegistered = true; }
        if (n.IsServer && !connectionRegistered) { n.OnClientConnectedCallback += OnConnected; connectionRegistered = true; }
    }

    public static bool IsQuest(TestStage1QuestKind kind) => HasActiveQuest && !IsRunComplete && CurrentKind == kind;
    public static bool IsMiningElement(TestStage1QuestElement e) => IsQuest(TestStage1QuestKind.Mining) && CurrentElement == e;
    public static bool HasOfferedQuest(TestStage1QuestKind kind)
    {
        if (!HasActiveQuest || IsRunComplete) return false;
        for (int i = 0; i < Count; i++) if (quests[i].Kind == kind) return true;
        return false;
    }
    public static bool TryGetOfferedQuestElement(TestStage1QuestKind kind, out TestStage1QuestElement element)
    {
        for (int i = 0; i < Count; i++)
            if (HasActiveQuest && !IsRunComplete && quests[i].Kind == kind) { element = quests[i].Element; return true; }
        element = TestStage1QuestElement.Any; return false;
    }
    public static void SelectQuest(int index)
    {
        if (index < 0 || index >= Count || IsRunComplete) return; var self = FindFirstObjectByType<TestStage1QuestSystem>(); if (self == null) return;
        if (self.IsAuthority()) self.ApplySelection(index); else self.SendSelection(index);
    }
    public static void ReportFireflyCollected()
    {
        var s = FindAuthorityInstance(); if (s == null || !HasActiveQuest || IsRunComplete) return;
        for (int i = Count - 1; i >= 0 && !IsRunComplete; i--)
            if (quests[i].Kind == TestStage1QuestKind.Firefly) s.Advance(i);
    }
    public static void ReportMineralMined(TestStage1QuestElement e)
    {
        var s = FindAuthorityInstance(); if (s == null || !HasActiveQuest || IsRunComplete) return;
        for (int i = Count - 1; i >= 0 && !IsRunComplete; i--)
            if (quests[i].Kind == TestStage1QuestKind.Mining && quests[i].Element == e) s.Advance(i);
    }
    public static void ReportSlimeDefeated(TestStage1SlimeSmallController slime)
    {
        var s = FindAuthorityInstance();
        if (s == null || !HasActiveQuest || IsRunComplete) return;
        int slimeId = slime == null ? 0 : slime.GetInstanceID();
        if (slimeId != 0 && lastCountedSlimeId == slimeId) return;
        lastCountedSlimeId = slimeId;
        var defeatedElement = slime == null ? TestStage1QuestElement.Any : QuestElementFromPower(slime.ElementPower);
        for (int i = Count - 1; i >= 0 && !IsRunComplete; i--)
            if (quests[i].Kind == TestStage1QuestKind.Hunt
                && (quests[i].Element == TestStage1QuestElement.Any || quests[i].Element == defeatedElement))
                s.Advance(i);
    }
    private static TestStage1QuestSystem FindAuthorityInstance()
    {
        foreach (var system in FindObjectsByType<TestStage1QuestSystem>(FindObjectsSortMode.None))
            if (system != null && system.isActiveAndEnabled && system.IsAuthority()) return system;
        return null;
    }
    private static TestStage1QuestElement QuestElementFromPower(TestStage1ElementPower power) => power switch
    {
        TestStage1ElementPower.Fire => TestStage1QuestElement.Fire,
        TestStage1ElementPower.Land => TestStage1QuestElement.Land,
        TestStage1ElementPower.Wind => TestStage1QuestElement.Wind,
        TestStage1ElementPower.Water => TestStage1QuestElement.Water,
        _ => TestStage1QuestElement.Any
    };
    private void Advance(int questIndex)
    {
        if (questIndex < 0 || questIndex >= Count || IsRunComplete) return;
        var q = quests[questIndex]; q.Progress = Mathf.Min(q.Target, q.Progress + 1); quests[questIndex] = q;
        if (q.Progress < q.Target) { Broadcast(); RefreshBoard(); return; }
        float duration = Mathf.Max(0, Time.time - q.StartedAt);
        if (q.Kind == TestStage1QuestKind.Hunt) { HuntQuestCount++; HuntTotalSeconds += duration; } else { CollectionQuestCount++; CollectionTotalSeconds += duration; }
        CompletedQuestCount++;
        PlayQuestCompleteSound();
        if (TestStage1GameSettings.Mode == TestStage1GameMode.QuestCount && CompletedQuestCount >= TestStage1GameSettings.RequiredQuestCount) { CompleteRun(); return; }
        quests[questIndex] = Create((TestStage1QuestKind)UnityEngine.Random.Range(0, 3)); Broadcast(); RefreshBoard(); QuestChanged?.Invoke();
    }
    private static Quest Create(TestStage1QuestKind kind)
    {
        var q = new Quest { Kind = kind, Element = TestStage1QuestElement.Any, Target = 1, StartedAt = Time.time };
        if (kind == TestStage1QuestKind.Mining) q.Element = (TestStage1QuestElement)UnityEngine.Random.Range(1, 5);
        if (kind == TestStage1QuestKind.Hunt) { q.Target = UnityEngine.Random.Range(5, 11); if (UnityEngine.Random.value < .5f) q.Element = (TestStage1QuestElement)UnityEngine.Random.Range(1, 5); }
        return q;
    }
    private void ApplySelection(int index)
    {
        if (index == ActiveQuestIndex || index < 0 || index >= Count) return; ActiveQuestIndex = index;
        var q = quests[index]; q.StartedAt = Time.time; quests[index] = q; Broadcast(); RefreshBoard(); QuestChanged?.Invoke();
    }
    private void SendSelection(int index)
    {
        var n = NetworkManager.Singleton; if (n == null) return; using var w = new FastBufferWriter(4, Allocator.Temp); w.WriteValueSafe(index); n.CustomMessagingManager.SendNamedMessage(SelectMessage, NetworkManager.ServerClientId, w);
    }
    private static void ReceiveSelection(ulong sender, FastBufferReader r) { r.ReadValueSafe(out int i); FindFirstObjectByType<TestStage1QuestSystem>()?.ApplySelection(i); }
    private void CompleteRun() { IsRunComplete = true; HasActiveQuest = false; Save(); Broadcast(); RefreshBoard(); RunCompleted?.Invoke(); }
    private void RefreshBoard()
    {
        if (texts.Count != Count) return;
        for (int i = 0; i < Count; i++) { texts[i].text = IsRunComplete ? (i == 0 ? "通關！" : "") : $"{(i == ActiveQuestIndex ? "▶ " : "　")}{Describe(quests[i])}"; texts[i].color = i == ActiveQuestIndex ? new Color32(105, 48, 12, 255) : new Color32(48, 34, 24, 255); }
    }
    private static string Describe(Quest q)
    {
        string e = ElementName(q.Element); return q.Kind switch { TestStage1QuestKind.Firefly => $"收集螢火蟲　進度：{q.Progress}/{q.Target}", TestStage1QuestKind.Mining => $"採集{e}元素礦物　進度：{q.Progress}/{q.Target}", _ => $"討伐{(q.Element == TestStage1QuestElement.Any ? "" : e + "屬性")}史萊姆　進度：{q.Progress}/{q.Target}" };
    }
    public static string GetStatisticsText()
    {
        float ca = CollectionQuestCount == 0 ? 0 : CollectionTotalSeconds / CollectionQuestCount, ha = HuntQuestCount == 0 ? 0 : HuntTotalSeconds / HuntQuestCount;
        return $"完成任務：{CompletedQuestCount}\n總花費時間：{Format(ElapsedSeconds)}\n採集任務：{CollectionQuestCount}\n平均採集時間：{Format(ca)}\n討伐任務：{HuntQuestCount}\n平均討伐時間：{Format(ha)}";
    }
    private static void Save()
    {
        PlayerPrefs.SetInt("LazyAlchemist.Last.CompletedQuests", CompletedQuestCount); PlayerPrefs.SetFloat("LazyAlchemist.Last.ElapsedSeconds", ElapsedSeconds);
        PlayerPrefs.SetInt("LazyAlchemist.Last.CollectionQuests", CollectionQuestCount); PlayerPrefs.SetFloat("LazyAlchemist.Last.AverageCollectionSeconds", CollectionQuestCount == 0 ? 0 : CollectionTotalSeconds / CollectionQuestCount);
        PlayerPrefs.SetInt("LazyAlchemist.Last.HuntQuests", HuntQuestCount); PlayerPrefs.SetFloat("LazyAlchemist.Last.AverageHuntSeconds", HuntQuestCount == 0 ? 0 : HuntTotalSeconds / HuntQuestCount); PlayerPrefs.Save();
    }
    private void Broadcast() { var n = NetworkManager.Singleton; if (n == null || !n.IsServer) return; foreach (ulong id in n.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) SendState(id); }
    private void SendState(ulong id)
    {
        var n = NetworkManager.Singleton; using var w = new FastBufferWriter(256, Allocator.Temp);
        w.WriteValueSafe((byte)TestStage1GameSettings.Mode); w.WriteValueSafe(TestStage1GameSettings.CountdownSeconds); w.WriteValueSafe(TestStage1GameSettings.RequiredQuestCount); w.WriteValueSafe(ActiveQuestIndex);
        for (int i = 0; i < Count; i++) { w.WriteValueSafe((byte)quests[i].Kind); w.WriteValueSafe((byte)quests[i].Element); w.WriteValueSafe(quests[i].Progress); w.WriteValueSafe(quests[i].Target); }
        w.WriteValueSafe(CompletedQuestCount); w.WriteValueSafe(CollectionQuestCount); w.WriteValueSafe(HuntQuestCount); w.WriteValueSafe(CollectionTotalSeconds); w.WriteValueSafe(HuntTotalSeconds); w.WriteValueSafe(ElapsedSeconds); w.WriteValueSafe(RemainingSeconds); w.WriteValueSafe(IsRunComplete); w.WriteValueSafe(HasActiveQuest);
        n.CustomMessagingManager.SendNamedMessage(StateMessage, id, w);
    }
    private void OnConnected(ulong id) { if (id != NetworkManager.ServerClientId) SendState(id); }
    private static void ReceiveState(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte mode); r.ReadValueSafe(out float countdown); r.ReadValueSafe(out int required); if ((TestStage1GameMode)mode == TestStage1GameMode.Countdown) TestStage1GameSettings.ConfigureCountdown(countdown); else TestStage1GameSettings.ConfigureQuestCount(required);
        r.ReadValueSafe(out int active); ActiveQuestIndex = Mathf.Clamp(active, 0, 2);
        for (int i = 0; i < Count; i++) { r.ReadValueSafe(out byte k); r.ReadValueSafe(out byte e); r.ReadValueSafe(out int p); r.ReadValueSafe(out int t); quests[i] = new Quest { Kind = (TestStage1QuestKind)k, Element = (TestStage1QuestElement)e, Progress = p, Target = t, StartedAt = Time.time }; }
        r.ReadValueSafe(out int c); r.ReadValueSafe(out int cc); r.ReadValueSafe(out int hc); r.ReadValueSafe(out float ct); r.ReadValueSafe(out float ht); r.ReadValueSafe(out float elapsed); r.ReadValueSafe(out float remaining); r.ReadValueSafe(out bool complete); r.ReadValueSafe(out bool has);
        int previousCompleted = CompletedQuestCount; bool becameComplete = !IsRunComplete && complete; CompletedQuestCount = c; CollectionQuestCount = cc; HuntQuestCount = hc; CollectionTotalSeconds = ct; HuntTotalSeconds = ht; ElapsedSeconds = elapsed; RemainingSeconds = remaining; IsRunComplete = complete; HasActiveQuest = has;
        if (c > previousCompleted) FindFirstObjectByType<TestStage1QuestSystem>()?.PlayQuestCompleteSound();
        QuestChanged?.Invoke(); FindFirstObjectByType<TestStage1QuestSystem>()?.RefreshBoard(); if (becameComplete) RunCompleted?.Invoke();
    }
    private bool IsAuthority() { if (TestStage1DebugMode.IsEnabled) return TestStage1DebugMode.IsServer; var n = NetworkManager.Singleton; return n == null || !n.IsListening || n.IsServer; }
    private void PlayQuestCompleteSound()
    {
        completionAudioSource ??= gameObject.AddComponent<AudioSource>();
        completionAudioSource.playOnAwake = false; completionAudioSource.spatialBlend = 0f;
        if (completionClip == null)
        {
            const int rate = 22050, count = 5000; var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float frequency = t < .1f ? 660f : 880f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 5f) * .22f;
            }
            completionClip = AudioClip.Create("QuestComplete_Placeholder", count, 1, rate, false); completionClip.SetData(samples, 0);
        }
        completionAudioSource.PlayOneShot(completionClip);
    }
    private static GameObject FindObject(string name) { foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) { var f = FindChild(root.transform, name); if (f != null) return f.gameObject; } return null; }
    private static Transform FindChild(Transform root, string name) { if (root.name == name) return root; for (int i = 0; i < root.childCount; i++) { var f = FindChild(root.GetChild(i), name); if (f != null) return f; } return null; }
    private static TestStage1QuestElement GuessElement(string s) { s = s.ToLowerInvariant(); if (s.Contains("flame") || s.Contains("fire")) return TestStage1QuestElement.Fire; if (s.Contains("land") || s.Contains("earth")) return TestStage1QuestElement.Land; if (s.Contains("wind")) return TestStage1QuestElement.Wind; if (s.Contains("water")) return TestStage1QuestElement.Water; return TestStage1QuestElement.Any; }
    public static string ElementName(TestStage1QuestElement e) => e switch { TestStage1QuestElement.Fire => "火", TestStage1QuestElement.Land => "土", TestStage1QuestElement.Wind => "風", TestStage1QuestElement.Water => "水", _ => "任意" };
    private static string Format(float s) => $"{Mathf.FloorToInt(s / 60):00}:{Mathf.FloorToInt(s) % 60:00}";
}
