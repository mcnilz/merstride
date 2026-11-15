using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Merstride.Acceptance;

public class DistributionTests
{
    [TestCase("compiler")]
    [TestCase("ilc")]
    [TestCase("link")]
    [TestCase("publish")]
    [TestCase("runtime-assets")]
    [TestCase("bundled-analyzer")]
    public void DistributionGateRejectsBuildToolLeakage(string boundary)
    {
        var temp = Path.Combine(FeatureTests.Root, "artifacts", "audit-fixtures", Guid.NewGuid().ToString("N"));
        var audit = Path.Combine(temp, "audit");
        var native = Path.Combine(temp, "native");
        Directory.CreateDirectory(audit); Directory.CreateDirectory(native);
        try
        {
            foreach (var file in Directory.GetFiles(Path.Combine(FeatureTests.Root, "artifacts/audit"), "*.txt"))
                File.Copy(file, Path.Combine(audit, Path.GetFileName(file)));
            var binary = Environment.GetEnvironmentVariable("MERSTRIDE_BINARY")!;
            File.Copy(binary, Path.Combine(native, Path.GetFileName(binary)));
            var assets = JsonNode.Parse(File.ReadAllText(Path.Combine(FeatureTests.Root, "src/Merstride/obj/project.assets.json")))!;
            if (boundary == "runtime-assets")
            {
                var target = assets["targets"]!.AsObject().First().Value!;
                target["NsDepCop/3.3.0"]!["runtime"] = JsonNode.Parse("{\"lib/NsDepCop.dll\":{}}");
            }
            else if (boundary == "bundled-analyzer") File.WriteAllText(Path.Combine(native, "NsDepCop.dll"), "forbidden tool fixture");
            else File.AppendAllText(Path.Combine(audit, boundary + ".txt"), "\n/build/nsdepcop/3.3.0/NsDepCop.dll\n");
            var assetPath = Path.Combine(temp, "assets.json"); File.WriteAllText(assetPath, assets.ToJsonString());
            using var build = new BuildWorld();
            build.Run("pwsh", "-NoProfile", "-File", "scripts/distribution-audit.ps1", "-NativeDirectory", native, "-AuditDirectory", audit, "-Assets", assetPath);
            Assert.That(build.ExitCode, Is.Not.Zero);
        }
        finally { Directory.Delete(temp, true); }
    }
}
