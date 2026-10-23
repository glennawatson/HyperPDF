// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Settings;

/// <summary>Tests for <see cref="SettingsStore"/>.</summary>
public sealed class SettingsStoreTests
{
    /// <summary>The remembered document path.</summary>
    private const string DocumentPath = "documents/a.pdf";

    /// <summary>Remembered tabs compare by their saved values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SessionTabsHaveValueEquality()
    {
        var first = new SessionTab { FilePath = DocumentPath, PageIndex = 1, IsSelected = true };
        var second = new SessionTab { FilePath = first.FilePath, PageIndex = first.PageIndex, IsSelected = true };
        await Assert.That(first.Equals(second)).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
        await Assert.That(first.Equals(new SessionTab { FilePath = first.FilePath })).IsFalse();
    }

    /// <summary>Verifies settings survive a save and load.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoundTrips()
    {
        const int cacheSize = 512;
        const int page = 3;
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}", "settings.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings { ColorScheme = ColorSchemeChoice.Calm, PageTone = PageToneChoice.SoftPaper, DefaultZoomMode = ZoomMode.FitPage, TileCacheMegabytes = cacheSize };
        settings.Session.Add(new() { FilePath = DocumentPath, PageIndex = page, IsSelected = true });

        store.Save(settings);
        var loaded = store.Load();

        await Assert.That(loaded.ColorScheme).IsEqualTo(ColorSchemeChoice.Calm);
        await Assert.That(loaded.PageTone).IsEqualTo(PageToneChoice.SoftPaper);
        await Assert.That(loaded.DefaultZoomMode).IsEqualTo(ZoomMode.FitPage);
        await Assert.That(loaded.TileCacheMegabytes).IsEqualTo(cacheSize);
        await Assert.That(loaded.Session.Count).IsEqualTo(1);
        await Assert.That(loaded.Session[0].FilePath).IsEqualTo(DocumentPath);
        await Assert.That(loaded.Session[0].PageIndex).IsEqualTo(page);
        await Assert.That(loaded.Session[0].IsSelected).IsTrue();
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    /// <summary>Verifies a corrupt file falls back to defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorruptFileGivesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ not json");

        var loaded = new SettingsStore(path).Load();

        await Assert.That(loaded.ShowSidebar).IsTrue();
        File.Delete(path);
    }
}
