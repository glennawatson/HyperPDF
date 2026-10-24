#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

const string ExecutableName = "hyperpdf";

const string ShareDirectory = "share";

const string PackagingDirectory = "packaging";

const string LinuxDirectory = "linux";

const int OptionWidth = 2;

if (args.Contains("--help"))
{
    Console.WriteLine("dotnet run --file src/tools/InstallLinux.cs -- [--prefix directory] [--from published directory]");
    return 0;
}

if (!OperatingSystem.IsLinux())
{
    throw new PlatformNotSupportedException("Desktop integration requires Linux.");
}

var root = ScriptProcess.Root();

var prefix = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local");

var source = Path.Combine(root, "artifacts", "linux-x64");

for (var index = 0; index < args.Length; index += OptionWidth)
{
    if (index + 1 == args.Length)
    {
        throw new ArgumentException($"Missing value for {args[index]}.");
    }

    switch (args[index])
    {
        case "--prefix":
        {
            prefix = Path.GetFullPath(args[index + 1]);
            break;
        }

        case "--from":
        {
            source = Path.GetFullPath(args[index + 1]);
            break;
        }

        default:
        throw new ArgumentException($"Unknown option {args[index]}.");
    }
}

const string appId = "net.glennwatson.HyperPDF";

var library = Path.Combine(prefix, "lib", ExecutableName);

var bin = Path.Combine(prefix, "bin");

var applications = Path.Combine(prefix, ShareDirectory, "applications");

var metadata = Path.Combine(prefix, ShareDirectory, "metainfo");

foreach (var directory in new[]
{
    library,
    bin,
    applications,
    metadata,
}

)
{
    _ = Directory.CreateDirectory(directory);
}

var executable = Path.Combine(library, ExecutableName);

foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var destination = Path.Combine(library, Path.GetRelativePath(source, file));
    _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(file, destination, true);
}

File.SetUnixFileMode(
    executable,
    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

var link = Path.Combine(bin, ExecutableName);

File.Delete(link);

_ = File.CreateSymbolicLink(link, executable);

var desktop = await File.ReadAllTextAsync(Path.Combine(root, PackagingDirectory, LinuxDirectory, $"{appId}.desktop"), CancellationToken.None);

var lines = desktop.Split('\n');

for (var index = 0; index < lines.Length; index++)
{
    if (lines[index].StartsWith("Exec=hyperpdf", StringComparison.Ordinal))
    {
        lines[index] = lines[index].Replace("Exec=hyperpdf", $"Exec=\"{link}\"", StringComparison.Ordinal);
    }
    else if (lines[index].StartsWith("TryExec=hyperpdf", StringComparison.Ordinal))
    {
        lines[index] = $"TryExec={link}";
    }
}

desktop = string.Join('\n', lines);

await File.WriteAllTextAsync(Path.Combine(applications, $"{appId}.desktop"), desktop, CancellationToken.None);

File.Copy(Path.Combine(root, PackagingDirectory, LinuxDirectory, $"{appId}.metainfo.xml"), Path.Combine(metadata, $"{appId}.metainfo.xml"), true);

var icons = Path.Combine(root, PackagingDirectory, LinuxDirectory, "icons", "hicolor");

foreach (var file in Directory.EnumerateFiles(icons, "*", SearchOption.AllDirectories))
{
    var destination = Path.Combine(prefix, ShareDirectory, "icons", "hicolor", Path.GetRelativePath(icons, file));
    _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(file, destination, true);
}

foreach (var command in new[]
{
    "update-desktop-database",
    "kbuildsycoca6",
}

)
{
    try
    {
        await ScriptProcess.RunAsync(command, command == "update-desktop-database" ? [applications] : ["--noincremental"], root, CancellationToken.None);
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.WriteLine($"Optional desktop cache tool {command} is unavailable.");
    }
}

Console.WriteLine($"Installed to {prefix}. Set the default with: xdg-mime default {appId}.desktop application/pdf");

return 0;
