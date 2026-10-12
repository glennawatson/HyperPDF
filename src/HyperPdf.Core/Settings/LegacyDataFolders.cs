// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Settings;

/// <summary>
/// Moves each user's data from the folders the app used before it was renamed from PdfViewerLite to HyperPDF: the
/// settings, the downloaded voices, text recognition languages and dictionaries, and the recent documents list.
/// </summary>
public static class LegacyDataFolders
{
    /// <summary>The settings and downloads folder name before the rename.</summary>
    internal const string LegacyName = "pdfviewerlite";

    /// <summary>The settings and downloads folder name.</summary>
    internal const string Name = "hyperpdf";

    /// <summary>The Windows and macOS data folder name before the rename.</summary>
    internal const string LegacyPlatformName = "PdfViewerLite";

    /// <summary>The Windows and macOS data folder name.</summary>
    internal const string PlatformName = "HyperPDF";

    /// <summary>Moves every legacy folder of the current user that has no new folder yet.</summary>
    /// <returns>The number of folders moved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveAll() => MoveAll(
        SettingsStore.GetConfigHome(),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support"));

    /// <summary>Moves every legacy folder under the given roots that has no new folder yet.</summary>
    /// <param name="configHome">The configuration root that holds the settings folder.</param>
    /// <param name="localData">The local application data root that holds the downloads and the Windows data folder.</param>
    /// <param name="applicationSupport">The macOS Application Support root.</param>
    /// <returns>The number of folders moved.</returns>
    public static int MoveAll(string configHome, string localData, string applicationSupport)
    {
        // On case-insensitive file systems the lower-case and platform names are one folder, so the second move of a
        // root finds its target already present and does nothing.
        var moved = 0;
        moved += Move(Path.Combine(configHome, LegacyName), Path.Combine(configHome, Name)) ? 1 : 0;
        moved += Move(Path.Combine(localData, LegacyName), Path.Combine(localData, Name)) ? 1 : 0;
        moved += Move(Path.Combine(localData, LegacyPlatformName), Path.Combine(localData, PlatformName)) ? 1 : 0;
        moved += Move(Path.Combine(applicationSupport, LegacyPlatformName), Path.Combine(applicationSupport, PlatformName)) ? 1 : 0;
        return moved;
    }

    /// <summary>Moves a legacy folder to its new name when the legacy folder exists and the new one does not.</summary>
    /// <param name="legacy">The folder before the rename.</param>
    /// <param name="current">The folder after the rename.</param>
    /// <returns><see langword="true"/> when the folder moved.</returns>
    public static bool Move(string legacy, string current)
    {
        ArgumentException.ThrowIfNullOrEmpty(legacy);
        ArgumentException.ThrowIfNullOrEmpty(current);
        if (Directory.Exists(current) || !Directory.Exists(legacy))
        {
            return false;
        }

        try
        {
            Directory.Move(legacy, current);
            return true;
        }
        catch (IOException)
        {
            // Another process holds a file in the old folder; the app starts with fresh data and the old folder stays.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
