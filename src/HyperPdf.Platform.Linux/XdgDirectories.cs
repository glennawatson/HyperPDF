// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Linux;

/// <summary>Resolves XDG base directories.</summary>
public static class XdgDirectories
{
    /// <summary>Gets the user configuration directory (<c>$XDG_CONFIG_HOME</c> or <c>~/.config</c>).</summary>
    public static string ConfigHome => Resolve("XDG_CONFIG_HOME", ".config");

    /// <summary>Gets the user data directory (<c>$XDG_DATA_HOME</c> or <c>~/.local/share</c>).</summary>
    public static string DataHome => Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));

    /// <summary>Resolves a directory from an environment variable with a home relative fallback.</summary>
    /// <param name="variable">The environment variable.</param>
    /// <param name="fallback">The fallback relative to the home directory.</param>
    /// <returns>The directory.</returns>
    private static string Resolve(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrEmpty(value) && Path.IsPathRooted(value)
            ? value
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    }
}
