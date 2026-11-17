using NUnit.Framework;
using System.Text.Json;

namespace Merstride.Acceptance;

public class UsabilityAuditTests
{
    [TestCase("valid", "managed", 0)]
    [TestCase("missing-text", "managed", 1)]
    [TestCase("missing-json", "managed", 1)]
    [TestCase("only-observations", "managed", 1)]
    [TestCase("missing-command", "managed", 1)]
    [TestCase("wrong-variant", "managed", 1)]
    [TestCase("missing-text", "native", 1)]
    [TestCase("missing-json", "native", 1)]
    [TestCase("only-observations", "native", 1)]
    [TestCase("missing-command", "native", 1)]
    [TestCase("wrong-variant", "native", 1)]
    public void PublicAuditRequiresPrimaryActionEvidenceInEachMode(string defect, string host, int exit)
    {
        using var cli = new CliWorld();
        using var reader = new Microsoft.VisualBasic.FileIO.TextFieldParser(Path.Combine(FeatureTests.Root, "docs/usability-scenario-review.tsv"));
        reader.SetDelimiters("\t"); reader.HasFieldsEnclosedInQuotes = true;
        _ = reader.ReadFields();
        var matrix = new Dictionary<string, string[]>(StringComparer.Ordinal);
        while (!reader.EndOfData)
        {
            var cells = reader.ReadFields()!; matrix.Add(cells[0], cells);
        }
        var managed = new List<object>(); var native = new List<object>();
        foreach (var scenario in FeatureTests.Cases())
        {
            var feature = Path.GetRelativePath(FeatureTests.Root, scenario.File).Replace('\\', '/');
            var identity = $"{feature}:{scenario.Line} {scenario.Name}";
            var row = matrix[identity];
            List<OperationEvidence> Evidence(bool mutate)
            {
                var actions = row[6].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .SelectMany(declaration => row[7].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(mode => new OperationEvidence("controlled-policy-fixture", "action", declaration.Split('@')[0], mode, 0,
                            declaration.Contains('@') ? declaration.Split('@')[1] : "direct"))).ToList();
                if (!mutate || !identity.StartsWith("features/00-bootstrap.feature:", StringComparison.Ordinal)) return actions;
                actions.RemoveAll(a => a.Command == "advance" && (defect is "only-observations" or "missing-command" or "wrong-variant" || a.Mode == (defect == "missing-text" ? "text" : "json")));
                // Setup/status and positive call counts cannot replace the action.
                actions.Add(new("controlled-policy-fixture", "action", "status", "text", 0));
                actions.Add(new("controlled-policy-fixture", "observation", "advance", "text", 0));
                actions.Add(new("controlled-policy-fixture", "observation", "advance", "json", 0));
                if (defect == "wrong-variant")
                {
                    actions.Add(new("controlled-policy-fixture", "action", "advance", "text", 0, "different-work"));
                    actions.Add(new("controlled-policy-fixture", "action", "advance", "json", 0, "different-work"));
                }
                return actions;
            }
            managed.Add(new { feature, line = scenario.Line, scenario = scenario.Name, tags = scenario.Tags,
                result = new { status = row[3] == "release-environment" ? "required; not run" : "passed", complementaryCalls = 1,
                    operations = Evidence(defect != "valid" && host == "managed") } });
            if (row[3] is "native-cli" or "native-observer" or "native-budget")
                native.Add(new { feature, line = scenario.Line, scenario = scenario.Name, tags = scenario.Tags, complementaryCalls = 1,
                    operations = Evidence(defect != "valid" && host == "native"), result = new { status = "passed" } });
        }
        var managedPath = Path.Combine(cli.DirectoryPath, "managed.json");
        var nativePath = Path.Combine(cli.DirectoryPath, "native.json");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(managedPath, JsonSerializer.Serialize(managed, options));
        File.WriteAllText(nativePath, JsonSerializer.Serialize(native, options));
        using var build = new BuildWorld();
        build.Run("pwsh", "-NoProfile", "-File", "scripts/usability-audit.ps1", "-ManagedResults", managedPath,
            "-NativeResults", nativePath, "-Output", Path.Combine(cli.DirectoryPath, "policy-fixture-result.json"));
        Assert.That(build.ExitCode, Is.EqualTo(exit));
    }

    [TestCase("valid", 0)]
    [TestCase("missing-row", 1)]
    [TestCase("duplicate-row", 1)]
    [TestCase("missing-assessment", 1)]
    [TestCase("wrong-boundary", 1)]
    public void PublicAuditRejectsIncompleteOrMisclassifiedReview(string defect, int exit)
    {
        using var cli = new CliWorld();
        var rows = File.ReadAllLines(Path.Combine(FeatureTests.Root, "docs/usability-scenario-review.tsv")).ToList();
        switch (defect)
        {
            case "missing-row": rows.RemoveAt(1); break;
            case "duplicate-row": rows.Add(rows[1]); break;
            case "missing-assessment":
                var cells = rows[1].Split('\t'); cells[4] = ""; rows[1] = string.Join('\t', cells); break;
            case "wrong-boundary": rows[1] = rows[1].Replace("native-cli", "public-build"); break;
        }
        var matrix = Path.Combine(cli.DirectoryPath, "review.tsv"); File.WriteAllLines(matrix, rows);
        using var build = new BuildWorld();
        build.Run("pwsh", "-NoProfile", "-File", "scripts/usability-audit.ps1", "-InventoryOnly", "-Matrix", matrix);
        Assert.That(build.ExitCode, Is.EqualTo(exit));
    }
}
