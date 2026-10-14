// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Tests applying and saving PDF page colour choices.</summary>
public sealed class PreferencesViewModelTests
{
    /// <summary>A recognizable persisted fillable-field tint.</summary>
    private const uint LegacyTintColor = 0x123456U;

    /// <summary>The settings file used by the tests.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The index of the soft yellow tint.</summary>
    private const int YellowChoice = 1;

    /// <summary>The soft yellow tint as 0xRRGGBB.</summary>
    private const uint YellowTint = 0xEBDC96U;

    /// <summary>The index of the 60% strength.</summary>
    private const int StrongChoice = 5;

    /// <summary>The 60% strength as an opacity from 0 to 255.</summary>
    private const int StrongAlpha = 153;

    /// <summary>A strength saved by an earlier version that is not offered; the closest offered one is 40%.</summary>
    private const int LegacyAlpha = 100;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The x of a point inside the empty, fillable field "Limited".</summary>
    private const int FillableX = 172;

    /// <summary>The y, from the top, of a point inside "Limited".</summary>
    private const int FillableY = 460;

    /// <summary>The position of the red channel in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The position of the blue channel in a BGRA pixel.</summary>
    private const int BlueSlot = 0;

    /// <summary>The bits to shift the red of a colour down by.</summary>
    private const int RedShift = 16;

    /// <summary>How far a blended channel may differ from the expected one, in levels of 255.</summary>
    private const int ChannelTolerance = 3;

    /// <summary>The largest value of a channel.</summary>
    private const double ChannelMax = 255;

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

    /// <summary>The form field colour and strength are listed with plain names, saved when chosen and read back by a new run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormHighlightChoicesRoundTrip()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            await Assert.That(PreferencesViewModel.FormHighlightColorOptions[preferences.FormHighlightColor]).IsEqualTo("Soft blue");
            await Assert.That(PreferencesViewModel.FormHighlightOpacityOptions[preferences.FormHighlightOpacity]).IsEqualTo("25% (standard)");
            preferences.FormHighlightColor = YellowChoice;
            preferences.FormHighlightOpacity = StrongChoice;
        }

        var saved = new SettingsStore(path).Load();
        using var nextRun = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform());
        using var reloaded = new PreferencesViewModel(nextRun);

        await Assert.That(saved.FormHighlightColor).IsEqualTo(YellowTint);
        await Assert.That(saved.FormHighlightAlpha).IsEqualTo(StrongAlpha);
        await Assert.That(reloaded.FormHighlightColor).IsEqualTo(YellowChoice);
        await Assert.That(reloaded.FormHighlightOpacity).IsEqualTo(StrongChoice);
    }

    /// <summary>A tint saved by an earlier version that is not one of the offered colours shows the closest choice and is kept until the user picks another.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnlistedFormHighlightIsKeptUntilChanged()
    {
        using var test = new TestServices();
        test.Services.Settings.FormHighlightColor = LegacyTintColor;
        test.Services.Settings.FormHighlightAlpha = LegacyAlpha;
        using var preferences = new PreferencesViewModel(test.Services);

        await Assert.That(test.Services.Settings.FormHighlightColor).IsEqualTo(LegacyTintColor);
        await Assert.That(test.Services.Settings.FormHighlightAlpha).IsEqualTo(LegacyAlpha);
        await Assert.That(PreferencesViewModel.FormHighlightOpacityOptions[preferences.FormHighlightOpacity]).IsEqualTo("40%");
    }

    /// <summary>Choosing a colour and strength tints the fillable fields of a document that is already open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChosenFormHighlightAppearsInTheOpenDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("rich-form.pdf", FormSamples.CreateRichForm())]);
        using var preferences = new PreferencesViewModel(test.Services) { FormHighlightColor = YellowChoice, FormHighlightOpacity = StrongChoice };
        var document = main.SelectedTab!.TryGetDocument()!;
        var tint = DocumentFeature<IFormHighlight>.Get(document)!.Highlight;
        var size = document.GetPageSizes()[0];
        var width = (int)MathF.Ceiling(size.Width);
        var pixels = new byte[width * (int)MathF.Ceiling(size.Height) * BytesPerPixel];

        var rendered = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, (int)MathF.Ceiling(size.Height), width * BytesPerPixel));
        var offset = ((FillableY * width) + FillableX) * BytesPerPixel;

        await Assert.That(tint.Color).IsEqualTo(YellowTint);
        await Assert.That(tint.Alpha).IsEqualTo((byte)StrongAlpha);
        await Assert.That(rendered).IsTrue();
        await Assert.That(Math.Abs(pixels[offset + RedSlot] - Blend(YellowTint >> RedShift))).IsLessThanOrEqualTo(ChannelTolerance);
        await Assert.That(Math.Abs(pixels[offset + BlueSlot] - Blend(YellowTint))).IsLessThanOrEqualTo(ChannelTolerance);
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
            var reloadBar = view.FindNamed<Border>("ReloadBar")!;
            await File.AppendAllTextAsync(path, "\n% external change\n");
            tab.OnFileChanged();
            await Assert.That(reloadBar.IsVisible).IsTrue();
            await Assert.That(view.FindNamed<TextBlock>("ReloadText")!.Text).Contains("Reload it?");
            var dismiss = view.FindNamed<Button>("DismissReloadButton")!;
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

    /// <summary>Works out the channel a colour channel gives over white at the strong strength.</summary>
    /// <param name="channel">The colour's channel, in the low byte.</param>
    /// <returns>The blended value.</returns>
    private static int Blend(uint channel) => (int)Math.Round(ChannelMax - ((ChannelMax - (channel & byte.MaxValue)) * (StrongAlpha / ChannelMax)));
}
