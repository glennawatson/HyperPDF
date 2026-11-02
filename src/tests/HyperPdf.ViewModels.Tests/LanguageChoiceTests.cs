// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.ViewModels.Preferences;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the dialog shown on the first launch for choosing the interface language.</summary>
public sealed class LanguageChoiceTests
{
    /// <summary>The settings file name.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The index of French in the language list.</summary>
    private const int French = 2;

    /// <summary>The dialog is needed until a language has been chosen, in either place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsNeededOnlyBeforeALanguageIsChosen()
    {
        using var fresh = new TestServices();
        await Assert.That(LanguageChoiceViewModel.IsNeeded(fresh.Services)).IsTrue();

        using var chosen = new TestServices();
        chosen.Services.Settings.InterfaceLanguage = "de";
        await Assert.That(LanguageChoiceViewModel.IsNeeded(chosen.Services)).IsFalse();

        using var followed = new TestServices();
        followed.Services.Settings.InterfaceLanguageAsked = true;
        await Assert.That(LanguageChoiceViewModel.IsNeeded(followed.Services)).IsFalse();
    }

    /// <summary>Continuing keeps the chosen language and the fact that the user was asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContinueSavesTheChoice()
    {
        using var test = new TestServices();
        var choice = new LanguageChoiceViewModel(test.Services) { SelectedIndex = French };

        _ = await choice.ContinueCommand.Execute().ToTask();

        var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load();
        await Assert.That(saved.InterfaceLanguage).IsEqualTo("fr");
        await Assert.That(saved.InterfaceLanguageAsked).IsTrue();
        await Assert.That(LanguageChoiceViewModel.Options.Count).IsEqualTo(PreferencesViewModel.InterfaceLanguageOptions.Count);
    }

    /// <summary>Following the desktop is remembered as asked, so the dialog does not return.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowingTheDesktopIsRemembered()
    {
        using var test = new TestServices();
        var choice = new LanguageChoiceViewModel(test.Services);

        _ = await choice.ContinueCommand.Execute().ToTask();

        await Assert.That(test.Services.Settings.InterfaceLanguage).IsEqualTo(string.Empty);
        await Assert.That(LanguageChoiceViewModel.IsNeeded(test.Services)).IsFalse();
    }

    /// <summary>Changing the language in Preferences also counts as having been asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosingInPreferencesCountsAsAsked()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.InterfaceLanguage = French;

        await Assert.That(test.Services.Settings.InterfaceLanguage).IsEqualTo("fr");
        await Assert.That(LanguageChoiceViewModel.IsNeeded(test.Services)).IsFalse();
        await Assert.That(PreferencesViewModel.GetInterfaceLanguageIndex("fr")).IsEqualTo(French);
        await Assert.That(PreferencesViewModel.GetInterfaceLanguageIndex("xx")).IsEqualTo(0);
    }
}
