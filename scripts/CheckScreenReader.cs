#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Scripts;

const string Enabled = "1";

const string ArgumentSeparator = "--";

const string Dotnet = "dotnet";

const string RunCommand = "run";

const string FileOption = "--file";

const int UsageExitCode = 2;

const int ProcessTimeoutMinutes = 2;

const int ProcessNameColumn = 2;

const int MinimumBusFields = 3;

const int ConditionTimeoutSeconds = 30;

const int PollingMilliseconds = 100;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("dotnet run --file scripts/CheckScreenReader.cs -- [--wayland] <app path> [minimum named controls]");
    return args.Length == 0 ? UsageExitCode : 0;
}

if (!OperatingSystem.IsLinux())
{
    throw new PlatformNotSupportedException("AT-SPI requires Linux.");
}

var root = ScriptProcess.Root();

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(ProcessTimeoutMinutes));

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var token = cancellation.Token;

if (Environment.GetEnvironmentVariable("CHECK_SCREEN_READER_INNER") != Enabled)
{
    return await ScriptProcess.RunAsync(
        "dbus-run-session",
        [
            ArgumentSeparator,
            Dotnet,
            RunCommand,
            FileOption,
            Path.Combine(root, "scripts", "CheckScreenReader.cs"),
            ArgumentSeparator,
            ..args],
        root,
        token,
        new Dictionary<string, string?> { ["CHECK_SCREEN_READER_INNER"] = Enabled });
}

var wayland = args[0] == "--wayland";

var appIndex = wayland ? 1 : 0;

if (args.Length <= appIndex)
{
    throw new ArgumentException("An application path is required.");
}

var app = Path.GetFullPath(args[appIndex]);

var minimumIndex = appIndex + 1;

var minimum = args.Length > minimumIndex ? args[minimumIndex] : "25";

var work = Path.Combine(Path.GetTempPath(), $"hyperpdf-accessibility-{Guid.NewGuid():N}");

var runtime = Path.Combine(work, "runtime");

_ = Directory.CreateDirectory(runtime);

File.SetUnixFileMode(runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

var environment = new Dictionary<string, string?> { ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null, ["WAYLAND_SOCKET"] = null, ["XDG_RUNTIME_DIR"] = runtime, ["LIBGL_ALWAYS_SOFTWARE"] = Enabled, };

foreach (var name in new[]
{
    "CONFIG",
    "DATA",
    "STATE",
}

)
{
    var directory = Path.Combine(work, name.ToLowerInvariant());
    _ = Directory.CreateDirectory(directory);
    environment[$"XDG_{name}_HOME"] = directory;
}

var processes = new List<Process>();

var logs = new List<Task<string>>();

try
{
    if (wayland)
    {
        environment["WAYLAND_DISPLAY"] = "wayland-test";
        Start("weston", ["--backend=headless", "--renderer=pixman", "--socket=wayland-test", "--idle-time=0"]);
        await PollAsync(() => Task.FromResult(File.Exists(Path.Combine(runtime, "wayland-test"))), token);
    }
    else
    {
        var display = ScriptProcess.Start("Xvfb", ["-displayfd", Enabled, "-screen", "0", "1400x900x24"], root, environment, true);
        processes.Add(display);
        logs.Add(display.StandardError.ReadToEndAsync(token));
        environment["DISPLAY"] = $":{await display.StandardOutput.ReadLineAsync(token)}";
        logs.Add(display.StandardOutput.ReadToEndAsync(token));
    }

    var check = Path.Combine(root, "scripts", "AccessibilityCheck.cs");
    var pdf = Path.Combine(work, "check.pdf");
    if (await ScriptProcess.RunAsync(Dotnet, [RunCommand, FileOption, check, ArgumentSeparator, "create-check-pdf", pdf], root, token) != 0)
    {
        return 1;
    }

    var launcher = File.Exists("/usr/libexec/at-spi-bus-launcher") ? "/usr/libexec/at-spi-bus-launcher" : "at-spi-bus-launcher";
    Start(launcher, ["--launch-immediately"]);
    string? address = null;
    await PollAsync(
        async () =>
        {
            try
            {
                var value = await ScriptProcess.CaptureAsync(
                    "gdbus",
                    [
                        "call",
                        "--session",
                        "--dest",
                        "org.a11y.Bus",
                        "--object-path",
                        "/org/a11y/bus",
                        "--method",
                        "org.a11y.Bus.GetAddress"],
                    root,
                    token,
                    environment);
                var first = value.IndexOf('\'');
                var last = value.LastIndexOf('\'');
                if (first >= 0 && last > first)
                {
                    address = value[(first + 1)..last];
                }

                return address is not null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        },
        token);
    await ScriptProcess.CaptureAsync(
        "gdbus",
        [
            "call",
            "--session",
            "--dest",
            "org.a11y.Bus",
            "--object-path",
            "/org/a11y/bus",
            "--method",
            "org.freedesktop.DBus.Properties.Set",
            "org.a11y.Status",
            "IsEnabled",
            "<true>"],
        root,
        token,
        environment);
    Start(app, ["--new-instance", pdf]);
    string? failure = null;
    await PollAsync(
        async () =>
        {
            var listing = await ScriptProcess.CaptureAsync("busctl", [$"--address={address}", "list", "--no-pager"], root, token, environment);
            foreach (var line in listing.Split('\n'))
            {
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < MinimumBusFields || fields[ProcessNameColumn] != "hyperpdf")
                {
                    continue;
                }

                try
                {
                    Console.WriteLine(
                        await ScriptProcess.CaptureAsync(
                            Dotnet,
                            [RunCommand, FileOption, check, ArgumentSeparator, "atspi-walk", address!, fields[0], minimum],
                            root,
                            token,
                            environment));
                    return true;
                }
                catch (InvalidOperationException error)
                {
                    failure = error.Message;
                }
            }

            if (failure is not null)
            {
                await Console.Error.WriteLineAsync(failure);
            }

            return false;
        },
        token);
    return 0;
}
finally
{
    foreach (var process in processes)
    {
        if (!process.HasExited)
        {
            process.Kill(true);
        }

        await process.WaitForExitAsync(CancellationToken.None);
        process.Dispose();
    }

    foreach (var log in logs)
    {
        try
        {
            Console.WriteLine(await log);
        }
        catch (OperationCanceledException)
        {
        }
    }

    Directory.Delete(work, true);
}

void Start(string executable, string[] arguments)
{
    var process = ScriptProcess.Start(executable, arguments, root, environment, true);
    processes.Add(process);
    logs.Add(process.StandardOutput.ReadToEndAsync(token));
    logs.Add(process.StandardError.ReadToEndAsync(token));
}

static async Task PollAsync(Func<Task<bool>> condition, CancellationToken token)
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(TimeSpan.FromSeconds(ConditionTimeoutSeconds));
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollingMilliseconds));
    while (!await condition())
    {
        _ = await timer.WaitForNextTickAsync(timeout.Token);
    }
}
