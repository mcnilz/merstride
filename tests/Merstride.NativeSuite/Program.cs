using System.Text.Json;
using Merstride.Acceptance;
using Merstride.NativeTesting;

try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var exclude = false;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--exclude-release-environment-orchestrator" && !exclude) { exclude = true; continue; }
        if (args[i] is not ("--binary" or "--fixture" or "--results") || i + 1 >= args.Length || !options.TryAdd(args[i], args[++i]))
            throw new ArgumentException("Expected --binary FILE --fixture FILE --results FILE --exclude-release-environment-orchestrator.");
    }
    if (!exclude || options.Count != 3) throw new ArgumentException("All native suite options are required.");
    foreach (var key in new[] { "--binary", "--fixture" })
    {
        var path = Path.GetFullPath(options[key]);
        if (!File.Exists(path)) throw new FileNotFoundException("Native suite input does not exist.", path);
        Environment.SetEnvironmentVariable(key == "--binary" ? "MERSTRIDE_BINARY" : "MERSTRIDE_FIXTURE", path);
    }
    // Fail closed if an assertion adapter regression stops detecting false results.
    foreach (var check in new Action[] {
        () => Assert.That(1, Is.EqualTo(2)),
        () => Assert.That(new[] { "a", "a" }, Is.EquivalentTo(new[] { "a", "b" })),
        () => Assert.That(new[] { 1, 2 }, Is.EqualTo(new[] { 2, 1 })),
        () => Assert.That("expected", Does.Contain("expected").And.Not.Contain("expected")),
        () => Assert.That(new[] { 1 }, Has.Length.EqualTo(2)),
        () => Assert.That(false, Is.True),
        () => Assert.That("x", Is.EqualTo("different").And.EqualTo("x").IgnoreCase),
        () => Assert.That(1, Is.InRange(2, 3)) })
    {
        var failed = false;
        try { check(); } catch (AssertionException) { failed = true; }
        if (!failed) throw new InvalidOperationException("Native assertion negative control failed.");
    }
    var cases = FeatureTests.Cases().Where(c => !c.Tags.Contains("@build") &&
        !c.Steps.Any(s => s.StartsWith("the published native binary on a clean", StringComparison.Ordinal))).ToArray();
    if (cases.Length == 0 || cases.Any(c => !c.Implemented)) throw new InvalidOperationException("Unimplemented or missing required runtime scenarios.");
    var output = Path.GetFullPath(options["--results"]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    using var stream = File.Create(output);
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
    writer.WriteStartArray();
    var failures = 0;
    foreach (var scenario in cases)
    {
        string? error = null;
        try { FeatureTests.Execute(scenario); }
        catch (Exception exception) { error = exception.ToString(); failures++; }
        writer.WriteStartObject();
        writer.WriteString("feature", Path.GetRelativePath(FeatureTests.Root, scenario.File).Replace('\\', '/'));
        writer.WriteNumber("line", scenario.Line);
        writer.WriteString("scenario", scenario.Name);
        writer.WriteStartArray("tags"); foreach (var tag in scenario.Tags) writer.WriteStringValue(tag); writer.WriteEndArray();
        writer.WriteString("platform", System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
        writer.WriteNumber("complementaryCalls", FeatureTests.LastComplementaryCalls);
        writer.WriteStartArray("operations");
        foreach (var operation in FeatureTests.Operations)
        {
            writer.WriteStartObject(); writer.WriteString("pass", operation.Pass); writer.WriteString("purpose", operation.Purpose);
            writer.WriteString("command", operation.Command); writer.WriteString("mode", operation.Mode); writer.WriteNumber("exit", operation.Exit);
            writer.WriteString("variant", operation.Variant);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartObject("result"); writer.WriteString("status", error is null ? "passed" : "failed");
        if (error is not null) writer.WriteString("error", error);
        writer.WriteEndObject(); writer.WriteEndObject(); writer.Flush();
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {scenario}");
        if (error is not null) Console.Error.WriteLine(error);
    }
    writer.WriteEndArray();
    return failures == 0 ? 0 : 1;
}
catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
