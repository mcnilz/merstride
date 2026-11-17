using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
#if NATIVE_SUITE
using Merstride.NativeTesting;
#else
using NUnit.Framework;
#endif

namespace Merstride.Acceptance;

internal sealed class CreationGuideWorld : IDisposable
{
    private readonly CliWorld world = new();
    private byte[] binaryHash = [];
    private bool json;

    internal void Execute(string step)
    {
        const string invocation = "I invoke the creation guide with arguments ";
        if (step.StartsWith(invocation, StringComparison.Ordinal))
        {
            var args = step[invocation.Length..].Replace("{binary}", world.Binary, StringComparison.Ordinal).Split(';');
            json = args.TakeWhile(a => a != "--").Any(a => a is "--json" or "--json=false");
            world.Raw(args);
            return;
        }
        switch (step)
        {
            case "only the published native executable in an isolated directory":
                var source = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY") ??
                    Path.Combine(FeatureTests.Root, "artifacts", "native", OperatingSystem.IsWindows() ? "merstride.exe" : "merstride");
                world.Binary = Path.Combine(world.DirectoryPath, Path.GetFileName(source));
                File.Copy(source, world.Binary);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(world.Binary, File.GetUnixFileMode(source));
                binaryHash = SHA256.HashData(File.ReadAllBytes(world.Binary));
                break;
            case "I extract and validate the complete example from that response":
                var instructions = Protocol().GetProperty("guide").GetString()!;
                var yaml = Regex.Matches(instructions, "(?m)^```yaml merstride\\r?\\n[\\s\\S]*?^```\\r?$");
                var mermaid = Regex.Matches(instructions, "(?m)^```mermaid\\r?\\n[\\s\\S]*?^```\\r?$");
                Assert.That(yaml, Has.Count.EqualTo(1), "One complete metadata block, not fragments.");
                Assert.That(mermaid, Has.Count.EqualTo(1), "One complete matching graph.");
                File.WriteAllText(world.FilePath, yaml[0].Value + "\n" + mermaid[0].Value + "\n");
                world.Raw("validate", world.FilePath, "--state-dir", world.Binary!, "--json");
                break;
            case "the example validates without running its process or creating state":
                Assert.That(world.ExitCode, Is.Zero);
                var validation = Protocol();
                Assert.That(validation.GetProperty("workflowId").GetString(), Is.EqualTo("review"));
                Assert.That(validation.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(validation.GetProperty("diagnostics").GetArrayLength(), Is.Zero);
                Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.EquivalentTo(new[] { world.Binary, world.FilePath }));
                break;
            case "English help describes the workflow authoring command":
                Assert.That(world.ExitCode, Is.Zero);
                var help = world.Stdout.StartsWith('{') ? Protocol().GetProperty("guide").GetString()! : world.Stdout;
                foreach (var term in new[] { "Usage:", "creation-guide", "workflow authoring", "complete example", "--json", "--state-dir", "Exit codes:" })
                    Assert.That(help, Does.Contain(term));
                Assert.That(world.Stderr, Is.Empty);
                break;
            case "a usage error includes a help hint":
                Assert.That(world.ExitCode, Is.EqualTo(2));
                if (json && !(UsabilityAuditWorld.Complementary && !world.Stdout.StartsWith('{')))
                {
                    var error = Protocol().GetProperty("error");
                    Assert.That(error.GetProperty("code").GetString(), Is.EqualTo("USAGE"));
                    Assert.That(error.GetProperty("message").GetString(), Does.Contain("--help"));
                }
                else
                {
                    Assert.That(world.Stdout, Is.Empty);
                    Assert.That(world.Stderr, Does.Contain("Error USAGE").And.Contain("--help"));
                }
                break;
            case "one successful response contains self-contained authoring instructions":
                Assert.That(world.ExitCode, Is.Zero);
                var guide = world.Stdout;
                if (json)
                {
                    var response = Protocol();
                    Assert.That(response.GetProperty("status").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    Assert.That(response.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null));
                    guide = response.GetProperty("guide").GetString()!;
                }
                else
                {
                    Assert.That(guide, Does.StartWith("Merstride workflow creation guide"));
                    Assert.That(world.Stderr, Is.Empty);
                }
                Assert.That(guide, Is.Not.Empty);
                foreach (var term in new[] { "yaml merstride", "flowchart TD", "--workflow", "schemaVersion", "maxTransitions", "timeoutSeconds", "spawn_error", "output_limit", "signal", "default", "nodes.", "validate", "guide" })
                    Assert.That(guide, Does.Contain(term));
                break;
            case "the isolated directory contains only the unchanged executable":
                Assert.That(Directory.GetFileSystemEntries(world.DirectoryPath), Is.EqualTo(new[] { world.Binary }));
                Assert.That(SHA256.HashData(File.ReadAllBytes(world.Binary!)), Is.EqualTo(binaryHash));
                break;
            default: throw new AssertionException($"Undefined required creation-guide step: {step}");
        }
    }

    private JsonElement Protocol()
    {
        using var document = JsonDocument.Parse(world.Stdout);
        var response = document.RootElement.Clone();
        Assert.That(response.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(world.Stderr, Is.Empty);
        return response;
    }

    public void Dispose() => world.Dispose();
}
