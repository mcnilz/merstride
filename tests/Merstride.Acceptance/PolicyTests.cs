using NUnit.Framework;

namespace Merstride.Acceptance;

public class PolicyTests
{
    [TestCase("skip-verify")]
    [TestCase("wrong-run")]
    [TestCase("wrong-publish-needs")]
    [TestCase("conditional-platform")]
    [TestCase("token-in-build")]
    [TestCase("echo-assembly")]
    public void ReleaseWorkflowRejectsPublicationBypasses(string defect)
    {
        var temp = Path.Combine(FeatureTests.Root, "artifacts", "policy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var original = File.ReadAllText(Path.Combine(FeatureTests.Root, ".github/workflows/release.yml"));
            var changed = defect switch
            {
                "skip-verify" => original.Replace("  verify:\n", "  verify:\n    if: false\n"),
                "wrong-run" => original.Replace("          name: verified-release", "          run-id: 123\n          name: verified-release"),
                "wrong-publish-needs" => original.Replace("    needs: [gate, verify]", "    needs: [gate]"),
                "conditional-platform" => original.Replace("  linux:\n", "  linux:\n    if: false\n"),
                "token-in-build" => original.Replace("      RELEASE_VERSION:", "      GH_TOKEN: ${{ github.token }}\n      RELEASE_VERSION:"),
                _ => original.Replace("          ./scripts/release.ps1 -Mode Assemble", "          Write-Output './scripts/release.ps1 -Mode Assemble'")
            };
            Assert.That(changed, Is.Not.EqualTo(original), "Mutation must actually change the workflow.");
            File.WriteAllText(Path.Combine(temp, "release.yml"), changed);
            using var build = new BuildWorld();
            build.Run("pwsh", "-NoProfile", "-File", "scripts/workflow-policy.ps1", "-WorkflowDirectory", temp);
            Assert.That(build.ExitCode, Is.Not.Zero);
        }
        finally { Directory.Delete(temp, true); }
    }
    [TestCase("spaced-uses")]
    [TestCase("quoted-write")]
    [TestCase("ignored-build-failure")]
    [TestCase("ineffective-aggregate")]
    public void WorkflowPolicyRejectsEquivalentYamlBypasses(string defect)
    {
        var temp = Path.Combine(FeatureTests.Root, "artifacts", "policy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var workflow = File.ReadAllText(Path.Combine(FeatureTests.Root, ".github/workflows/build.yml"));
            workflow = defect switch
            {
                "spaced-uses" => workflow.Replace("uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9", "uses : unknown/publish@main"),
                "quoted-write" => workflow.Replace("    timeout-minutes: 30", "    timeout-minutes: 30\n    permissions: { contents: 'write' }"),
                "ignored-build-failure" => workflow.Replace("        run: ./scripts/build.ps1", "        continue-on-error: true\n        run: ./scripts/build.ps1"),
                _ => workflow.Replace("if ($env:BUILD_RESULT -ne 'success') { throw 'Required build did not succeed.' }", "Write-Output 'always green'")
            };
            File.WriteAllText(Path.Combine(temp, "build.yml"), workflow);
            using var build = new BuildWorld();
            build.Run("pwsh", "-NoProfile", "-File", "scripts/workflow-policy.ps1", "-WorkflowDirectory", temp);
            Assert.That(build.ExitCode, Is.Not.Zero);
        }
        finally { Directory.Delete(temp, true); }
    }
}
