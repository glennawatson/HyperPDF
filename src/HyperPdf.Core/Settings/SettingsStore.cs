// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the user's configuration directory.</summary>
[DebuggerDisplay("SettingsStore: {FilePath}")]
public sealed class SettingsStore
{
    /// <summary>Initializes a new instance of the <see cref="SettingsStore"/> class using the default location.</summary>
    public SettingsStore()
        : this(Path.Combine(GetConfigHome(), "hyperpdf", "settings.json"))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SettingsStore"/> class.</summary>
    /// <param name="filePath">The settings file.</param>
    public SettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
    }

    /// <summary>Gets the settings file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets the XDG configuration directory.</summary>
    /// <returns>The directory.</returns>
    public static string GetConfigHome()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return !string.IsNullOrEmpty(xdg) ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    }

    /// <summary>Loads settings, returning defaults when the file is missing or unreadable.</summary>
    /// <returns>The settings.</returns>
    public AppSettings Load() => PrivateJsonFile.Load(FilePath, SettingsJsonContext.Default.AppSettings) ?? new();

    /// <summary>Saves settings atomically.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PrivateJsonFile.Save(FilePath, settings, SettingsJsonContext.Default.AppSettings);
    }
}
