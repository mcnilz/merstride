using NUnit.Framework;

namespace Merstride.Acceptance;

public class AgentTests
{
    [TestCase("{\"outcome\":\"blocked\",\"extra\":true}")]
    [TestCase("{\"outcome\":\"blocked\",\"outcome\":\"blocked\"}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":{\"x\":1,\"x\":2}}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":[]}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":null}")]
    [TestCase("{\"outcome\":7}")]
    [TestCase("{}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":{\"n\":9007199254740992}}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":{\"n\":1e400}}")]
    [TestCase("{\"outcome\":\"blocked\",\"data\":{\"n\":NaN}}")]
    public void InvalidCompletionsLeaveStateUntouched(string payload)
    {
        using var world = new AgentWorld(); world.RejectPayload(payload);
    }

    [Test]
    public void InvalidUtf8CompletionLeavesStateUntouched()
    {
        using var world = new AgentWorld(); world.RejectInvalidUtf8();
    }

    [Test]
    public void EquivalentNumbersAndArraysRespectReceiptSemantics()
    {
        using var world = new AgentWorld(); world.ReceiptSemantics();
    }

    [Test]
    public void InputIsFrozenAndSelectedWithoutPromptInterpolation()
    {
        using var world = new AgentWorld(); world.FrozenInput();
    }

    [Test]
    public void SelectedProcessTailsAreBoundedAndIdentifyTheirSource()
    {
        using var world = new AgentWorld(); world.ProcessContext();
    }

    [Test]
    public void SpawnFailureContextIsAvailableWithANullExitCode()
    {
        using var world = new AgentWorld(); world.SpawnFailureContext();
    }

    [TestCase("context: [nodes.missing.data]")]
    [TestCase("context: [nodes.done.data]")]
    [TestCase("context: [nodes.repair.exitCode]")]
    [TestCase("context: [input, input]")]
    [TestCase("context: input")]
    public void InvalidAgentSelectorsFailBeforeRunCreation(string context)
    {
        using var world = new AgentWorld(); world.InvalidSelector(context);
    }

    [Test]
    public void CompletionDefaultsDataAndStillRequiresAdvanceAtAnEnd()
    {
        using var world = new AgentWorld(); world.DefaultData();
    }

    [Test]
    public void InputAndContextLimitsFailBeforeAgentWorkIsRequested()
    {
        using var world = new AgentWorld(); world.OversizedContext();
    }

    [Test]
    public void ExcessivelyNestedCompletionIsRejectedWithoutMutation()
    {
        using var world = new AgentWorld();
        world.RejectPayload("{\"outcome\":\"blocked\",\"data\":" + string.Concat(Enumerable.Repeat("{\"x\":", 17)) + "0" + new string('}', 17) + "}");
    }

    [TestCase("{\"x\":1,\"x\":2}", "INVALID_PAYLOAD")]
    [TestCase("[]", "INVALID_PAYLOAD")]
    [TestCase("{\"x\":1e400}", "INVALID_PAYLOAD")]
    [TestCase("{\"x\":9007199254740992}", "INVALID_PAYLOAD")]
    [TestCase("oversize", "INPUT_LIMIT")]
    public void InvalidInputCreatesNoRun(string input, string error)
    {
        using var world = new AgentWorld(); world.InvalidInput(input, error);
    }
}
