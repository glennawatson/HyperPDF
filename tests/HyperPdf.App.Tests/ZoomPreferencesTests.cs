// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Tests the zoom used when opening documents.</summary>
public sealed class ZoomPreferencesTests
{
    /// <summary>Verifies fresh settings show a whole page rather than expanding it to a wide screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsWithFitPage() => await Assert.That(new AppSettings().DefaultZoomMode).IsEqualTo(ZoomMode.FitPage);

    /// <summary>Verifies the Preferences control saves the zoom used by new tabs.</summary>
    /// <param name="mode">The opening zoom mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ZoomMode.Free)]
    [Arguments(ZoomMode.FitWidth)]
    [Arguments(ZoomMode.FitPage)]
    public async Task SavesOpeningZoom(ZoomMode mode)
    {
        using var test = new TestServices();
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        try
        {
            window.Show();
            var choice = window.FindControl<ComboBox>("OpeningZoomBox");
            await Assert.That(choice).IsNotNull();
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            choice!.SelectedIndex = (int)mode;
            await Assert.That(await UiWait.UntilAsync(() => test.Services.Settings.DefaultZoomMode == mode)).IsTrue();
            var saved = new SettingsStore(Path.Combine(test.Directory, "settings.json")).Load();
            await Assert.That(saved.DefaultZoomMode).IsEqualTo(mode);
            using var main = new MainViewModel(test.Services);
            main.Open([test.CreateDocument("opening-zoom.pdf", 1)]);
            await Assert.That(main.SelectedTab!.ZoomMode).IsEqualTo(mode);
        }
        finally
        {
            window.Close();
        }
    }
}
