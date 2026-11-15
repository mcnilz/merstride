using NUnit.Framework;

namespace Merstride.Acceptance;

public class PolicyTests
{
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
                "quoted-write" => workflow.Replace("    timeout-minutes: 20", "    timeout-minutes: 20\n    permissions: { contents: 'write' }"),
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
