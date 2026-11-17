using System.Diagnostics;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class BuildWorld : IDisposable
{
    private readonly string directory = Path.Combine(FeatureTests.Root, "artifacts", "fixtures", Guid.NewGuid().ToString("N"));
    private string output = "";
    private int exitCode;
    internal int ExitCode => exitCode;
    private string workflowDirectory = "";

    internal void Fixture(bool forbidden)
    {
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(FeatureTests.Root, "src/Merstride/config.nsdepcop"), Path.Combine(directory, "config.nsdepcop"), true);
        File.WriteAllText(Path.Combine(directory, "ArchitectureFixture.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><WarningsAsErrors>NSDEPCOP01;NSDEPCOP07</WarningsAsErrors></PropertyGroup>
              <ItemGroup><PackageReference Include="NsDepCop" Version="3.3.0" PrivateAssets="all" ExcludeAssets="compile;runtime" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), forbidden ? """
            namespace Merstride.Infrastructure { public sealed class Adapter { } }
            namespace Merstride.Domain { public sealed class Model { public Infrastructure.Adapter Value { get; } = new(); } }
            """ : """
            namespace Merstride.Domain { public sealed class Model { } }
            namespace Merstride.Application { public sealed class Service { public Domain.Model Value { get; } = new(); } }
            """);
    }

    internal void ParserFixture(string area)
    {
        Fixture(false);
        var project = Path.Combine(directory, "ArchitectureFixture.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("<ItemGroup>", "<ItemGroup><PackageReference Include=\"System.CommandLine\" Version=\"3.0.0\" />"));
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), $"namespace Merstride.{area}; public sealed class Model {{ public System.CommandLine.Option Value {{ get; }} = new System.CommandLine.Option<bool>(\"--flag\"); }}");
    }

    internal void AssertParserBoundary(bool allowed)
    {
        Run("dotnet", "build", directory, "--nologo", "--no-incremental");
        Assert.That(exitCode == 0, Is.EqualTo(allowed), output);
        if (!allowed) Assert.That(output, Does.Contain("error NSDEPCOP01"));
    }

    internal void Run(string executable, params string[] args)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = FeatureTests.Root, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); Assert.Fail("Build/policy gate exceeded 60-second watchdog."); }
        output = stdout.Result + stderr.Result; exitCode = process.ExitCode;
        TestContext.Out.WriteLine(output);
    }

    internal void Execute(string step)
    {
        switch (step)
        {
            case "a workflow using an action outside the official allowlist or without a verified full commit SHA":
                workflowDirectory = Path.Combine(directory, "workflows");
                Directory.CreateDirectory(workflowDirectory);
                File.WriteAllText(Path.Combine(workflowDirectory, "build.yml"), "jobs:\n  bad:\n    steps:\n      - uses: unknown/publish@main\n      - uses: actions/checkout@v7\n");
                break;
            case "I run the workflow policy checks":
                Run("pwsh", "-NoProfile", "-File", "scripts/workflow-policy.ps1", "-WorkflowDirectory", workflowDirectory); break;
            case "the build gate fails and identifies the action reference":
                Assert.That(exitCode, Is.Not.Zero); Assert.That(output, Does.Contain("unknown/publish@main")); break;
            case "a pull request from a fork":
                workflowDirectory = Path.Combine(FeatureTests.Root, ".github/workflows"); break;
            case "the build workflow runs":
                Run("pwsh", "-NoProfile", "-File", "scripts/workflow-policy.ps1", "-WorkflowDirectory", workflowDirectory); break;
            case "the build and implemented acceptance checks run with read-only repository permissions":
                Assert.That(exitCode, Is.Zero);
                Assert.That(output, Does.Contain("read-only fork build policy passed"));
                TestContext.Out.WriteLine("Local YAML policy validation only; hosted fork execution is unavailable. The complete build script is exercised independently.");
                break;
            case "no release is published and no repository secret is exposed to the contribution":
                Assert.That(exitCode, Is.Zero);
                var workflow = File.ReadAllText(Path.Combine(workflowDirectory, "build.yml"));
                Assert.That(workflow, Does.Not.Contain("secrets.").And.Not.Contain("pull_request_target").And.Not.Contain("gh release")); break;
            case "a proposed direct or transitive dependency missing its required provenance or license review":
                var src = Path.Combine(directory, "src", "proposed");
                Directory.CreateDirectory(src);
                File.WriteAllText(Path.Combine(src, "packages.lock.json"), """
                    {"version":1,"dependencies":{"net11.0":{"Unreviewed.Package":{"type":"Transitive","resolved":"1.0.0","contentHash":"unreviewed"}}}}
                    """);
                Directory.CreateDirectory(Path.Combine(directory, "tests")); break;
            case "I run the dependency review gate":
                Run("pwsh", "-NoProfile", "-File", "scripts/dependencies.ps1", "-Repository", directory); break;
            case "adoption is blocked and the missing evidence is reported":
                Assert.That(exitCode, Is.Not.Zero); Assert.That(output, Does.Contain("Missing dependency review evidence: Unreviewed.Package/1.0.0")); break;
            case "a build fixture with the production NsDepCop configuration": Fixture(false); break;
            case "its Domain code references an Infrastructure type": Fixture(true); break;
            case "its Application code references a Domain type without any forbidden dependency": break;
            case "I build the fixture": Run("dotnet", "build", directory, "--nologo", "--no-incremental"); break;
            case "the build fails with diagnostic NSDEPCOP01 as an error":
                Assert.That(exitCode, Is.Not.Zero); Assert.That(output, Does.Contain("error NSDEPCOP01")); break;
            case "the build succeeds with architecture analysis enabled":
                Assert.That(exitCode, Is.Zero); break;
            case "a production project with a missing NsDepCop configuration":
            case "a production project with architecture analysis disabled":
            case "a production project with illegal dependencies suppressed globally":
                Fixture(false);
                if (step.EndsWith("configuration", StringComparison.Ordinal)) File.Delete(Path.Combine(directory, "config.nsdepcop"));
                else if (step.EndsWith("disabled", StringComparison.Ordinal))
                    File.WriteAllText(Path.Combine(directory, "Directory.Build.props"), "<Project><PropertyGroup><TargetFramework>net11.0</TargetFramework><RunAnalyzers>false</RunAnalyzers></PropertyGroup></Project>");
                else File.WriteAllText(Path.Combine(directory, ".editorconfig"), "root = true\n[*.cs]\ndotnet_diagnostic.NSDEPCOP01.severity = none\n");
                break;
            case "I execute the architecture build gate":
                Run("pwsh", "-NoProfile", "-File", "scripts/architecture.ps1", "-Project", directory); break;
            case "the gate fails and identifies the ineffective enforcement":
                Assert.That(exitCode, Is.Not.Zero); Assert.That(output, Does.Contain("ARCHITECTURE")); break;
            default: throw new AssertionException($"Undefined required build step: {step}");
        }
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
