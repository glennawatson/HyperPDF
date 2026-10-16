// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Finds Firefox without using a user profile or platform-specific APIs.</summary>
internal static class FirefoxBrowserDiscovery
{
    /// <summary>The Windows Firefox executable name.</summary>
    private const string WindowsExecutable = "firefox.exe";

    /// <summary>Returns an installed executable or fails with an explicit prerequisite error.</summary>
    /// <returns>The Firefox executable path.</returns>
    /// <exception cref="FileNotFoundException">Firefox is missing or the configured path does not exist.</exception>
    internal static string FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("PVL_FIREFOX_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? Path.GetFullPath(configured) : throw new FileNotFoundException("PVL_FIREFOX_PATH does not identify an existing Firefox executable.", configured);
        }

        if (Environment.GetEnvironmentVariable("PVL_REQUIRE_PDFJS") != "1")
        {
            throw new FileNotFoundException("Firefox is required for pdf.js rendering comparisons. Set PVL_FIREFOX_PATH to its executable.");
        }

        var executable = OperatingSystem.IsWindows() ? WindowsExecutable : "firefox";
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            var path = Path.Combine(folder, executable);
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        foreach (var path in KnownLocations())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Firefox is required for pdf.js rendering comparisons. Set PVL_FIREFOX_PATH to its executable.");
    }

    /// <summary>Returns conventional Firefox installation paths.</summary>
    /// <returns>The operation result.</returns>
    private static IEnumerable<string> KnownLocations()
    {
        if (OperatingSystem.IsMacOS())
        {
            yield return "/Applications/Firefox.app/Contents/MacOS/firefox";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Firefox.app", "Contents", "MacOS", "firefox");
        }
        else if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", WindowsExecutable);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", WindowsExecutable);
        }
    }
}
