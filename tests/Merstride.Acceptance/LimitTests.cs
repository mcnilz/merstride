using NUnit.Framework;

namespace Merstride.Acceptance;

public class LimitTests
{
    [TestCase("end")]
    [TestCase("process")]
    [TestCase("agent")]
    public void AgentCompletionConsumesTheLastTransitionWithoutActivatingItsTarget(string target)
    {
        using var world = new LimitWorld(); world.AgentTransition(target);
    }

    [Test]
    public void ReplacementCharactersCannotExpandContextPastItsByteLimit()
    {
        using var world = new LimitWorld(); world.InvalidByteTail();
    }

    [TestCase("safe")]
    [TestCase("negativeUnsafe")]
    [TestCase("objectDepth16")]
    [TestCase("objectDepth17")]
    public void CompletionPreservesObjectDepthAndSafeIntegerBoundaries(string property)
    {
        using var world = new LimitWorld(); world.CompletionBoundary(property);
    }
    [TestCase("exact")]
    [TestCase("oversize")]
    [TestCase("depth16")]
    [TestCase("depth17")]
    [TestCase("duplicate")]
    [TestCase("unsafe")]
    [TestCase("safe")]
    [TestCase("negativeUnsafe")]
    [TestCase("objectDepth16")]
    [TestCase("objectDepth17")]
    public void StartInputUsesTheSameStructuredDataBoundaries(string property)
    {
        using var world = new LimitWorld(); world.InputBoundary(property);
    }
    [Test]
    public void PromptAndSerializedContextAllowExactlyThirtyTwoKiB()
    {
        using var world = new LimitWorld(); world.ExactContext();
    }
    [TestCase(0)]
    [TestCase(1)]
    public void CombinedOutputAllowsExactlyTenMiB(int extra)
    {
        using var world = new LimitWorld(); world.ExactOutput(extra);
    }
}
