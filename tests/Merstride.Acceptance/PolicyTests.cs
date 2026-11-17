using NUnit.Framework;

namespace Merstride.Acceptance;

public class PolicyTests
{
    [TestCase("success", "success", 0)]
    [TestCase("skipped", "success", 1)]
    [TestCase("success", "skipped", 1)]
    [TestCase("failure", "success", 1)]
    [TestCase("success", "failure", 1)]
    [TestCase("cancelled", "success", 1)]
    [TestCase("success", "cancelled", 1)]
    public void BuildGateRequiresBothWindowsAndLinux(string windows, string linux, int expectedExit)
    {
        using var build = new BuildWorld();
        build.Run("pwsh", "-NoProfile", "-File", "tests/fixtures/build-gate.ps1",
            "-Windows", windows, "-Linux", linux, "-ExpectedExit", expectedExit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.That(build.ExitCode, Is.Zero);
    }
    [TestCase("skip-verify")]
    [TestCase("wrong-run")]
    [TestCase("wrong-publish-needs")]
    [TestCase("conditional-platform")]
    [TestCase("token-in-build")]
    [TestCase("echo-assembly")]
    [TestCase("moving-checkout")]
    [TestCase("missing-checkout-ref")]
    [TestCase("missing-macos-result")]
    [TestCase("conditional-macos-clean")]
    [TestCase("intel-macos")]
    [TestCase("missing-macos-download")]
    [TestCase("runtime-path-hiding")]
    [TestCase("missing-reference-budget")]
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
                "moving-checkout" => original.Replace("ref: ${{ github.sha }}", "ref: main"),
                "missing-checkout-ref" => original.Replace("          ref: ${{ github.sha }}\n", ""),
                "missing-macos-result" => original.Replace("          MACOS_RESULT: ${{ needs.macos.result }}\n", ""),
                "conditional-macos-clean" => original.Replace("  macos-clean:\n", "  macos-clean:\n    if: false\n"),
                "intel-macos" => original.Replace("runs-on: macos-15", "runs-on: macos-15-intel"),
                "missing-macos-download" => original.Replace("          name: release-macos\n", "          name: wrong-platform\n"),
                "runtime-path-hiding" => original.Replace(" --remove-hosted-dotnet", " --hide-dotnet"),
                "missing-reference-budget" => original.Replace("          MACOS_REFERENCE_BUDGET_JSON: ${{ vars.MERSTRIDE_MACOS_REFERENCE_BUDGET }}\n", ""),
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
    [TestCase("macos-in-ordinary-build")]
    [TestCase("conditional-windows")]
    [TestCase("conditional-linux")]
    [TestCase("unapproved-push-branch")]
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
                "macos-in-ordinary-build" => workflow.Replace("  gate:\n", "  macos:\n    runs-on: macos-15\n    steps: []\n  gate:\n"),
                "conditional-windows" => workflow.Replace("  windows:\n", "  windows:\n    if: false\n"),
                "conditional-linux" => workflow.Replace("  linux:\n", "  linux:\n    if: false\n"),
                "unapproved-push-branch" => workflow.Replace("branches: [main, codex/p15-macos-native-release]", "branches: [main, '**']"),
                _ => workflow.Replace("if ($env:BUILD_RESULT -ne 'success') { throw 'Required build did not succeed.' }", "Write-Output 'always green'")
            };
            Assert.That(workflow, Is.Not.EqualTo(File.ReadAllText(Path.Combine(FeatureTests.Root, ".github/workflows/build.yml"))), "Mutation must change the workflow.");
            File.WriteAllText(Path.Combine(temp, "build.yml"), workflow);
            using var build = new BuildWorld();
            build.Run("pwsh", "-NoProfile", "-File", "scripts/workflow-policy.ps1", "-WorkflowDirectory", temp);
            Assert.That(build.ExitCode, Is.Not.Zero);
        }
        finally { Directory.Delete(temp, true); }
    }
}
