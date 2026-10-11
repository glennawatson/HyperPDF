// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests applying and saving PDF page colour choices.</summary>
public sealed class PreferencesViewModelTests
{
    /// <summary>A recognizable persisted fillable-field tint.</summary>
    private const uint LegacyTintColor = 0x123456U;

    /// <summary>The settings file used by the tests.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>Legacy engine settings are ignored and omitted when settings are saved again.</summary>
    /// <param name="legacyChoice">The value saved by an earlier version.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task LegacyPdfEngineSettingIsIgnored(int legacyChoice)
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        var contents = string.Concat("{\"pdfEngine\":", legacyChoice, ",\"formHighlightColor\":", LegacyTintColor, "}");
        await File.WriteAllTextAsync(path, contents);
        var store = new SettingsStore(path);
        var settings = store.Load();
        store.Save(settings);
        var saved = await File.ReadAllTextAsync(path);

        await Assert.That(settings.FormHighlightColor).IsEqualTo(LegacyTintColor);
        await Assert.That(saved).DoesNotContain("pdfEngine");
    }

    /// <summary>The default app composition opens documents with HyperPDF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultAppServicesUseHyperPdf()
    {
        using var services = AppServices.CreateDefault(new FallbackPlatform());

        await Assert.That(services.Engine.Name).IsEqualTo("HyperPDF");
    }

    /// <summary>Verifies that choosing a page colour applies even after comfort page colour was turned off.</summary>
    /// <param name="choice">The page colour chosen in Preferences.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageToneChoice.White)]
    [Arguments(PageToneChoice.Dark)]
    [Arguments(PageToneChoice.FollowDesktop)]
    public async Task ChoosingPageColourAppliesAndSaves(PageToneChoice choice)
    {
        using var test = new TestServices();
        test.Services.Settings.PageToneEnabled = false;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("page-colour.pdf", 1)]);
        var enabled = new List<bool>();
        using var changes = main.WhenAnyValue(static vm => vm.PageToneEnabled).SubscribeSafe(enabled.Add, static error => Trace.TraceError(error.ToString()));
        var preferences = new PreferencesViewModel(test.Services) { PageTone = (int)choice };

        await Assert.That(enabled[^1]).IsTrue();
        await Assert.That(main.SelectedTab!.PageTone).IsEqualTo(ThemeResolver.GetTone(choice, ColorSchemes.Calm));
        await Assert.That(preferences.PageTone).IsEqualTo((int)choice);
        await Assert.That(PreferencesViewModel.PageToneOptions[preferences.PageTone]).IsNotNull();
        var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load();
        await Assert.That(saved.PageTone).IsEqualTo(choice);
        await Assert.That(saved.PageToneEnabled).IsTrue();
    }

    /// <summary>The ask-before-reload choice is visible and survives another settings load.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AskingBeforeReloadPersists()
    {
        using var test = new TestServices();
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            preferences.FileChange = (int)FileChangeAction.AskToReload;
            await Assert.That(PreferencesViewModel.FileChangeOptions[preferences.FileChange]).IsEqualTo("Ask before reloading");
        }

        var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load();
        await Assert.That(saved.FileChangeAction).IsEqualTo(FileChangeAction.AskToReload);
    }

    /// <summary>A changed PDF offers Reload and Dismiss after the ask choice is selected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AskingBeforeReloadShowsDismissiblePrompt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("changed.pdf", 1);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        using var preferences = new PreferencesViewModel(test.Services) { FileChange = (int)FileChangeAction.AskToReload };
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var reloadBar = view.FindControl<Border>("ReloadBar")!;
            await File.AppendAllTextAsync(path, "\n% external change\n");
            tab.OnFileChanged();
            await Assert.That(reloadBar.IsVisible).IsTrue();
            await Assert.That(view.FindControl<TextBlock>("ReloadText")!.Text).Contains("Reload it?");
            var dismiss = view.FindControl<Button>("DismissReloadButton")!;
            await Assert.That(await UiWait.UntilAsync(() => dismiss.Command is not null)).IsTrue();
            dismiss.Command!.Execute(null);
            await Assert.That(reloadBar.IsVisible).IsFalse();
            await Assert.That(tab.IsLoaded).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
