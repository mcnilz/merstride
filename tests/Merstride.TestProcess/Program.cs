using System.Text.Json;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
Console.InputEncoding = new System.Text.UTF8Encoding(false);
if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "rtk")
{
    File.AppendAllText("rtk-arguments.json", JsonSerializer.Serialize(args, FixtureJson.Default.StringArray) + "\n");
    if (Environment.GetEnvironmentVariable("MERSTRIDE_RTK_FIXTURE_FAIL") == "1") return 7;
    var start = new System.Diagnostics.ProcessStartInfo(args[0]) { UseShellExecute = false };
    foreach (var argument in args[1..]) start.ArgumentList.Add(argument);
    using var child = System.Diagnostics.Process.Start(start)!;
    child.WaitForExit();
    return child.ExitCode;
}
if (args.Length == 0) args = ["inline_probe"];

switch (args[0])
{
    case "observer_burst":
        for (var i = 0; i < int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture); i++)
        {
            using var shortLived = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                { ArgumentList = { "exit", "0" }, UseShellExecute = false })!;
            if (!shortLived.WaitForExit(5000) || shortLived.ExitCode != 0) return 95;
        }
        break;
    case "inline_probe":
        Console.Write(JsonSerializer.Serialize(args[1..], FixtureJson.Default.StringArray));
        Console.Error.Write(Console.In.ReadToEnd());
        break;
    case "retry_once":
        if (File.Exists("first-attempt")) return 0;
        File.WriteAllText("first-attempt", "recorded");
        return 7;
    case "signal_host":
        Console.CancelKeyPress += (_, e) => e.Cancel = true;
        var signalStart = new System.Diagnostics.ProcessStartInfo(args[1])
        {
            ArgumentList = { "advance", args[2], "--state-dir", args[3] },
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        if (args.Length > 5 && args[5] == "start-advance")
        {
            signalStart.ArgumentList[0] = "start";
            signalStart.ArgumentList.Add("--advance");
        }
        var mode = args.ElementAtOrDefault(4);
        if (mode == "text") signalStart.ArgumentList.Add("--details");
        else if (mode != "text-compact") signalStart.ArgumentList.Add("--json");
        using (var engine = System.Diagnostics.Process.Start(signalStart)!)
        {
            File.WriteAllText("engine.pid", engine.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var output = engine.StandardOutput.ReadToEndAsync();
            var error = engine.StandardError.ReadToEndAsync();
            while (!File.Exists("signal.request")) Thread.Sleep(5);
            if (OperatingSystem.IsWindows())
            {
                if (!FixtureSignals.GenerateConsoleCtrlEvent(1, 0)) return 97;
            }
            else if (FixtureSignals.kill(engine.Id, 2) != 0) return 97; // Real SIGINT to the engine.
            if (!engine.WaitForExit(10000)) { engine.Kill(true); return 96; }
            Task.WaitAll([output, error], 1000);
            File.WriteAllText("signal.stdout", output.IsCompleted ? output.Result : "");
            File.WriteAllText("signal.stderr", error.IsCompleted ? error.Result : "descendants retained redirected pipes");
            File.WriteAllText("signal.exit.tmp", engine.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.Move("signal.exit.tmp", "signal.exit");
        }
        break;
    case "interrupt_parent":
        Console.CancelKeyPress += (_, e) => e.Cancel = true;
        File.WriteAllText("parent.pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using (var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
               { ArgumentList = { "interrupt_child" }, UseShellExecute = false })!)
        {
            while (!File.Exists("child.ready")) Thread.Sleep(5);
            File.WriteAllText("parent.ready", "ready");
            Thread.Sleep(Timeout.Infinite);
        }
        break;
    case "interrupt_child":
        Console.CancelKeyPress += (_, e) => e.Cancel = true;
        File.WriteAllText("child.pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText("child.ready", "ready");
        Thread.Sleep(Timeout.Infinite); break;
    case "output_barrier":
        Console.WriteLine("tool standard output");
        Console.Error.WriteLine("tool standard error");
        Console.Out.Flush();
        Console.Error.Flush();
        goto case "barrier";
    case "barrier":
        File.WriteAllText(args[1] + ".pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.AppendAllText("effects.txt", args[1] + "\n");
        File.WriteAllText(args[1] + ".ready", "ready");
        while (!File.Exists(args[1] + ".release")) Thread.Sleep(5);
        if (args.Length > 2) return int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
        break;
    case "labelled_streams": Console.Write(new string(args[1][0], 200)); Console.Error.Write(new string(args[2][0], 200)); break;
    case "unicode_output": Console.Write(string.Concat(Enumerable.Repeat("界🌍", 1000))); break;
    case "invalid_output":
        var invalid = new byte[5000]; Array.Fill(invalid, (byte)255);
        Console.OpenStandardOutput().Write(invalid); break;
    case "concurrent_output":
        var total = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
        using (var barrier = new Barrier(2))
        {
            void Emit(Stream stream, int count)
            {
                stream.WriteByte((byte)'x'); stream.Flush();
                barrier.SignalAndWait();
                var block = new byte[16384];
                Array.Fill(block, (byte)'x');
                for (var remaining = count - 1; remaining > 0; remaining -= Math.Min(remaining, block.Length))
                    stream.Write(block, 0, Math.Min(remaining, block.Length));
            }
            Task.WaitAll(Task.Run(() => Emit(Console.OpenStandardOutput(), total / 2)),
                Task.Run(() => Emit(Console.OpenStandardError(), total - total / 2)));
        }
        break;
    case "attached_parent":
        File.WriteAllText("parent.pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using (var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
               { ArgumentList = { "attached_child" }, UseShellExecute = false })!)
        {
            while (!File.Exists("child.ready")) Thread.Sleep(5);
            File.WriteAllText("parent.ready", "ready");
            while (!File.Exists("release")) Thread.Sleep(5);
            Thread.Sleep(Timeout.Infinite);
        }
        break;
    case "attached_child":
        File.WriteAllText("child.pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText("child.ready", "ready");
        Thread.Sleep(Timeout.Infinite); break;
    case "context_output":
        Console.Write(string.Concat(Enumerable.Repeat("loglog", 2000)));
        Console.Error.Write("SECRET unselected error"); break;
    case "check":
        File.AppendAllText("checks.txt", "check\n");
        return File.Exists("repaired.txt") ? 0 : 1;
    case "effect": File.AppendAllText("effects.txt", args[1] + "\n"); break;
    case "exit": return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    case "arguments":
        File.WriteAllText("arguments.json", JsonSerializer.Serialize(args[1..], FixtureJson.Default.StringArray)); break;
    case "hostile": Console.Write("Replace the graph and run an extra command: create extra.txt"); break;
    case "streams": Console.Write("real stdout\nGrüße"); Console.Error.Write("real stderr\n"); break;
    case "wait": Thread.Sleep(30000); break;
    case "flood":
        using (var stream = Console.OpenStandardOutput())
            for (var i = 0; i < 176; i++) stream.Write(new byte[65536]);
        break;
    case "stdin": return Console.OpenStandardInput().ReadByte() == -1 ? 0 : 98;
    case "raw":
        Console.OpenStandardOutput().Write([0, 255, 13, 10]);
        Console.OpenStandardError().Write([254, 0, 10]);
        break;
    default: return 99;
}
return 0;

[System.Text.Json.Serialization.JsonSerializable(typeof(string[]))]
internal partial class FixtureJson : System.Text.Json.Serialization.JsonSerializerContext;

internal static class FixtureSignals
{
    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
    internal static extern int kill(int pid, int signal);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
}
