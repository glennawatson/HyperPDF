// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.Views;
using HyperPdf.Core.Settings;
using HyperPdf.ViewModels.Preferences;

namespace HyperPdf.App.Tests;

/// <summary>Checks the language dialog shown on the first launch.</summary>
public sealed class LanguageChoiceWindowTests
{
    /// <summary>The index of French in the language list.</summary>
    private const int French = 2;

    /// <summary>The index of English in the language list.</summary>
    private const int English = 1;

    /// <summary>The settings file name.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>Picking a language selects it, and Continue keeps it and closes the dialog.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickingALanguageAndContinuingKeepsIt()
    {
        using var test = new TestServices();
        var window = new LanguageChoiceWindow { ViewModel = new(test.Services) };
        window.Show();
        try
        {
            var list = window.FindControl<ListBox>("LanguageList")!;
            var button = window.FindControl<Button>("ContinueButton")!;

            await Assert.That(list.ItemCount).IsEqualTo(LanguageChoiceViewModel.Options.Count);

            _ = await UiWait.UntilAsync(() => window.IsActive || window.IsVisible);
            list.SelectedIndex = French;
            _ = await UiWait.UntilAsync(() => window.ViewModel!.SelectedIndex == French);
            await Assert.That(window.ViewModel!.SelectedIndex).IsEqualTo(French);

            button.Command?.Execute(null);
            _ = await UiWait.UntilAsync(() => !window.IsVisible);

            var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load();
            await Assert.That(saved.InterfaceLanguage).IsEqualTo("fr");
            await Assert.That(saved.InterfaceLanguageAsked).IsTrue();
        }
        finally
        {
            window.Close();
            LanguagePreferences.Change(English);
        }
    }
}
