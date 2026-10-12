// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Settings;

/// <summary>Tests for <see cref="LegacyDataFolders"/>.</summary>
public sealed class LegacyDataFoldersTests
{
    /// <summary>The settings and downloads folder name before the rename.</summary>
    private const string LegacyName = "pdfviewerlite";

    /// <summary>The settings and downloads folder name.</summary>
    private const string Name = "hyperpdf";

    /// <summary>The settings file name.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The settings file content.</summary>
    private const string SettingsText = "{}";

    /// <summary>The downloaded voices folder.</summary>
    private const string Voices = "voices";

    /// <summary>The folders the first test moves: the settings and the downloads.</summary>
    private const int LegacyFolders = 2;

    /// <summary>The legacy settings and downloads move to the new folders.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesLegacyFolders()
    {
        var root = CreateRoot();
        try
        {
            var config = Path.Combine(root, "config");
            var local = Path.Combine(root, "local");
            var support = Path.Combine(root, "support");
            _ = Directory.CreateDirectory(Path.Combine(config, LegacyName));
            await File.WriteAllTextAsync(Path.Combine(config, LegacyName, SettingsFile), SettingsText);
            _ = Directory.CreateDirectory(Path.Combine(local, LegacyName, Voices));
            _ = Directory.CreateDirectory(support);

            var moved = LegacyDataFolders.MoveAll(config, local, support);

            await Assert.That(moved).IsGreaterThanOrEqualTo(LegacyFolders);
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(config, Name, SettingsFile))).IsEqualTo(SettingsText);
            await Assert.That(Directory.Exists(Path.Combine(config, LegacyName))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(local, Name, Voices))).IsTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>An existing new folder is kept and the legacy folder is left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsExistingFolder()
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, LegacyName);
            var current = Path.Combine(root, Name);
            _ = Directory.CreateDirectory(legacy);
            _ = Directory.CreateDirectory(current);

            await Assert.That(LegacyDataFolders.Move(legacy, current)).IsFalse();
            await Assert.That(Directory.Exists(legacy)).IsTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Nothing moves when there is no legacy folder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IgnoresMissingLegacyFolder()
    {
        var root = CreateRoot();
        try
        {
            var current = Path.Combine(root, Name);
            await Assert.That(LegacyDataFolders.Move(Path.Combine(root, LegacyName), current)).IsFalse();
            await Assert.That(Directory.Exists(current)).IsFalse();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Creates an empty folder for one test.</summary>
    /// <returns>The folder.</returns>
    private static string CreateRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"hyperpdf-legacy-{Guid.NewGuid():N}")).FullName;
}
