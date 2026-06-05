using System;
using UnityEngine;

public enum TestStage1RightHandTool
{
    None,
    Pickaxe,
    Staff
}

public enum TestStage1ElementPower
{
    None,
    Fire,
    Land,
    Wind,
    Water
}

public readonly struct TestStage1PlayerState
{
    public readonly bool HasSlimeBaitInLeftHand;
    public readonly TestStage1RightHandTool RightHandTool;
    public readonly TestStage1ElementPower ElementPower;

    public TestStage1PlayerState(
        bool hasSlimeBaitInLeftHand,
        TestStage1RightHandTool rightHandTool,
        TestStage1ElementPower elementPower)
    {
        HasSlimeBaitInLeftHand = hasSlimeBaitInLeftHand;
        RightHandTool = rightHandTool;
        ElementPower = elementPower;
    }
}

public static class TestStage1HandState
{
    public static TestStage1PlayerState Current { get; private set; }

    public static bool HasSlimeBaitInLeftHand => Current.HasSlimeBaitInLeftHand;
    public static TestStage1RightHandTool RightHandTool => Current.RightHandTool;
    public static TestStage1ElementPower ElementPower => Current.ElementPower;

    public static event Action<TestStage1PlayerState> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Current = new TestStage1PlayerState(false, TestStage1RightHandTool.None, TestStage1ElementPower.None);
        Changed = null;
    }

    public static void SetLeftHandSlimeBait(bool hasSlimeBait)
    {
        SetState(hasSlimeBait, Current.RightHandTool, Current.ElementPower);
    }

    public static void SetRightHandTool(TestStage1RightHandTool tool)
    {
        SetState(Current.HasSlimeBaitInLeftHand, tool, Current.ElementPower);
    }

    public static void SetElementPower(TestStage1ElementPower elementPower)
    {
        SetState(Current.HasSlimeBaitInLeftHand, Current.RightHandTool, elementPower);
    }

    public static void SetState(
        bool hasSlimeBaitInLeftHand,
        TestStage1RightHandTool rightHandTool,
        TestStage1ElementPower elementPower)
    {
        if (Current.HasSlimeBaitInLeftHand == hasSlimeBaitInLeftHand
            && Current.RightHandTool == rightHandTool
            && Current.ElementPower == elementPower)
        {
            return;
        }

        Current = new TestStage1PlayerState(hasSlimeBaitInLeftHand, rightHandTool, elementPower);
        Changed?.Invoke(Current);
    }

    public static TestStage1RightHandTool ToolFromItemName(string itemName)
    {
        if (string.Equals(itemName, "pickaxe", StringComparison.Ordinal))
        {
            return TestStage1RightHandTool.Pickaxe;
        }

        if (string.Equals(itemName, "staff", StringComparison.Ordinal))
        {
            return TestStage1RightHandTool.Staff;
        }

        return TestStage1RightHandTool.None;
    }

    public static TestStage1ElementPower ElementFromColorIndex(byte colorIndex)
    {
        return colorIndex switch
        {
            0 => TestStage1ElementPower.Fire,
            1 => TestStage1ElementPower.Land,
            2 => TestStage1ElementPower.Wind,
            3 => TestStage1ElementPower.Water,
            _ => TestStage1ElementPower.None
        };
    }
}
