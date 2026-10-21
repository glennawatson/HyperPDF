// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.ViewModels;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the custom page colours and the always-on keyboard cursor in Preferences.</summary>
public sealed class DocumentColourPreferencesTests
{
    /// <summary>The settings file name.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>A custom paper colour.</summary>
    private const uint NavyPaper = 0x102040U;

    /// <summary>Choosing custom colours shows their boxes, applies valid colours and keeps the last valid one while a box holds a typo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomColoursApplyOnlyWhenValid()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        await Assert.That(preferences.UsesCustomPageTone).IsFalse();
        preferences.PageTone = (int)PageToneChoice.Custom;
        preferences.CustomPaper = "#102040";
        preferences.CustomInk = "FFF";

        await Assert.That(preferences.UsesCustomPageTone).IsTrue();
        await Assert.That(test.Services.Settings.CustomPaperColor).IsEqualTo(NavyPaper);
        await Assert.That(test.Services.Settings.CustomInkColor).IsEqualTo(0xFFFF00U);
        await Assert.That(preferences.CustomToneContrast).Contains("six hex digits");

        preferences.CustomInk = "FFF0C0";

        await Assert.That(test.Services.Settings.CustomInkColor).IsEqualTo(0xFFF0C0U);
        await Assert.That(preferences.CustomToneContrast).StartsWith("Contrast ");
        var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load();
        await Assert.That(saved.PageTone).IsEqualTo(PageToneChoice.Custom);
        await Assert.That(saved.CustomPaperColor).IsEqualTo(NavyPaper);
    }

    /// <summary>A pair that is hard to read says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LowContrastIsReported()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services) { CustomPaper = "808080", CustomInk = "909090" };

        await Assert.That(preferences.CustomToneContrast).Contains("hard to read");
    }

    /// <summary>With the keyboard cursor kept, a document opens in caret browsing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptKeyboardCursorOpensDocumentsInCaretMode()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("before.pdf", 1)]);
        await Assert.That(main.SelectedTab!.IsCaretMode).IsFalse();

        using var preferences = new PreferencesViewModel(test.Services) { KeepKeyboardCursor = true };
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("after.pdf", 1)]);

        await Assert.That(test.Services.Settings.KeepKeyboardCursor).IsTrue();
        await Assert.That(main.SelectedTab!.IsCaretMode).IsTrue();
    }
}
