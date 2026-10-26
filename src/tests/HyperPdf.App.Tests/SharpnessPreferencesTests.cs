// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Rendering;
using HyperPdf.App.Theming;
using HyperPdf.App.Views;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;
using HyperPdf.Engine;
using HyperPdf.ViewModels;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.App.Tests;

/// <summary>Tests the text and image sharpness preferences: the controls, the saved settings and the open documents.</summary>
public sealed class SharpnessPreferencesTests
{
    /// <summary>The settings file used by the tests.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The index of the 2 pixel minimum line thickness.</summary>
    private const int TwoPixels = 2;

    /// <summary>The name of the Smooth choices.</summary>
    private const string SmoothName = "Smooth";

    /// <summary>The name of the named set box.</summary>
    private const string PresetBox = "SharpnessPresetBox";

    /// <summary>The name of the text edges box.</summary>
    private const string EdgesBox = "TextEdgesBox";

    /// <summary>The name of the line fitting box.</summary>
    private const string StrokeBox = "StrokeAdjustmentBox";

    /// <summary>The name of the thinnest line box.</summary>
    private const string LineBox = "MinimumLineBox";

    /// <summary>The name of the smooth lines box.</summary>
    private const string GraphicsBox = "SmoothGraphicsBox";

    /// <summary>The name of the smooth pictures box.</summary>
    private const string ImagesBox = "SmoothImagesBox";

    /// <summary>The name of the pixel averaging box.</summary>
    private const string AreaBox = "AreaImageReductionBox";

    /// <summary>The name of the advanced group.</summary>
    private const string AdvancedGroup = "SharpnessAdvanced";

    /// <summary>The width of the Preferences window.</summary>
    private const double PreferencesWidth = 540;

    /// <summary>A window narrower than the Preferences window.</summary>
    private const double NarrowWidth = 360;

    /// <summary>The largest interface text size offered.</summary>
    private const double LargeTextPoints = 16;

    /// <summary>A text edge index no choice has.</summary>
    private const int OutOfRangeEdge = 40;

    /// <summary>A minimum line width index below the first choice.</summary>
    private const int OutOfRangeWidth = -3;

    /// <summary>Every control of the sharpness choices, the named set first.</summary>
    private static readonly string[] ControlNames = [PresetBox, EdgesBox, StrokeBox, LineBox, GraphicsBox, ImagesBox, AreaBox];

    /// <summary>Every choice is saved when made and read back by a new run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoicesRoundTripThroughTheSettingsFile()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            var sharpness = preferences.Sharpness;
            sharpness.TextEdges = (int)TextEdgeChoice.Sharp;
            sharpness.SmoothGraphics = false;
            sharpness.SmoothImages = false;
            sharpness.StrokeAdjustment = (int)StrokeAdjustmentChoice.FollowPage;
            sharpness.MinimumLineWidth = TwoPixels;
            sharpness.AreaImageReduction = false;
        }

        var saved = new SettingsStore(path).Load().Sharpness;
        using var nextRun = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), new AvaloniaRenderHead());
        using var reloaded = new PreferencesViewModel(nextRun);

        await Assert.That(saved.TextEdges).IsEqualTo(TextEdgeChoice.Sharp);
        await Assert.That(saved.SmoothGraphics).IsFalse();
        await Assert.That(saved.SmoothImages).IsFalse();
        await Assert.That(saved.StrokeAdjustment).IsEqualTo(StrokeAdjustmentChoice.FollowPage);
        await Assert.That(saved.MinimumLineWidth).IsEqualTo(TwoPixels);
        await Assert.That(saved.AreaImageReduction).IsFalse();
        await Assert.That(reloaded.Sharpness.TextEdges).IsEqualTo((int)TextEdgeChoice.Sharp);
        await Assert.That(reloaded.Sharpness.Preset).IsEqualTo((int)SharpnessPreset.Custom);
        await Assert.That(reloaded.Sharpness.MinimumLineWidth).IsEqualTo(TwoPixels);
        await Assert.That(reloaded.Sharpness.AreaImageReduction).IsFalse();
    }

    /// <summary>The standard choices are plain: smooth, no line fitting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsWithThePlainChoices()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);
        var sharpness = preferences.Sharpness;

        await Assert.That(SharpnessViewModel.TextEdgeOptions[sharpness.TextEdges]).IsEqualTo(SmoothName);
        await Assert.That(SharpnessViewModel.StrokeAdjustmentOptions[sharpness.StrokeAdjustment]).IsEqualTo("Off");
        await Assert.That(SharpnessViewModel.MinimumLineOptions[sharpness.MinimumLineWidth]).IsEqualTo("Off");
        await Assert.That(sharpness.SmoothGraphics).IsTrue();
        await Assert.That(sharpness.SmoothImages).IsTrue();
        await Assert.That(sharpness.AreaImageReduction).IsTrue();
    }

    /// <summary>An index out of range still gives a working choice in the settings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutOfRangeIndexesGiveWorkingChoices()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.TextEdges = OutOfRangeEdge;
        preferences.Sharpness.MinimumLineWidth = OutOfRangeWidth;

        await Assert.That(test.Services.Settings.Sharpness.TextEdges).IsEqualTo(TextEdgeChoice.Smooth);
        await Assert.That(test.Services.Settings.Sharpness.MinimumLineWidth).IsEqualTo(0);
    }

    /// <summary>Choosing a setting reaches a document that is already open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChosenSharpnessReachesTheOpenDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sharpness.pdf", 1)]);
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.TextEdges = (int)TextEdgeChoice.Sharp;
        var document = main.SelectedTab!.TryGetDocument()!;
        var applied = DocumentFeature<IRenderSharpness>.Get(document)!.Sharpness;

        await Assert.That(applied.TextEdges).IsEqualTo(TextEdgeChoice.Sharp);
        await Assert.That(HyperPdfFormRuntime.CreateRenderOptions((HyperPdfDocument)document).Quality.TextEdges).IsEqualTo(PdfTextEdges.Sharp);
    }

    /// <summary>A document opened after the choice was made starts with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADocumentOpenedLaterStartsWithTheChoice()
    {
        using var test = new TestServices();
        var chosen = new RenderSharpness { SmoothImages = false, MinimumLineWidth = 1 };
        var engine = new HighlightedHyperPdfEngine(static () => FormHighlight.Default, () => chosen);
        using var document = (HyperPdfDocument)engine.Open(test.CreateDocument("later.pdf", 1), null);
        var quality = HyperPdfFormRuntime.CreateRenderOptions(document).Quality;

        await Assert.That(quality.SmoothImages).IsFalse();
        await Assert.That(quality.MinimumLineWidth).IsEqualTo(1F);
    }

    /// <summary>The controls show the saved choices and write the ones the person makes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsBindToTheChoices()
    {
        using var test = new TestServices();
        test.Services.Settings.Sharpness = new() { TextEdges = TextEdgeChoice.Sharp, SmoothImages = false };
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        try
        {
            window.Show();
            var edges = window.FindControl<ComboBox>(EdgesBox)!;
            var images = window.FindControl<CheckBox>(ImagesBox)!;
            var area = window.FindControl<CheckBox>(AreaBox)!;
            var thin = window.FindControl<ComboBox>(LineBox)!;
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();

            await Assert.That(edges.SelectedIndex).IsEqualTo((int)TextEdgeChoice.Sharp);
            await Assert.That(images.IsChecked).IsFalse();
            await Assert.That(area.IsChecked).IsTrue();

            area.IsChecked = false;
            thin.SelectedIndex = TwoPixels;
            edges.SelectedIndex = (int)TextEdgeChoice.Smooth;

            await Assert.That(await UiWait.UntilAsync(() => test.Services.Settings.Sharpness.TextEdges == TextEdgeChoice.Smooth)).IsTrue();
            var saved = new SettingsStore(Path.Combine(test.Directory, SettingsFile)).Load().Sharpness;
            await Assert.That(saved.AreaImageReduction).IsFalse();
            await Assert.That(saved.MinimumLineWidth).IsEqualTo(TwoPixels);
            await Assert.That(saved.TextEdges).IsEqualTo(TextEdgeChoice.Smooth);
            await Assert.That(saved.SmoothImages).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The window starts on Smooth, the standard choices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsOnTheSmoothSet()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        await Assert.That(SharpnessViewModel.PresetOptions[preferences.Sharpness.Preset]).IsEqualTo(SmoothName);
        await Assert.That(SharpnessViewModel.PresetOptions).IsEquivalentTo([SmoothName, "Sharp", "Crisp", "Custom"]);
    }

    /// <summary>Picking a named set fills in the individual choices, saves them and reaches the settings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickingASetFillsInTheChoices()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using var preferences = new PreferencesViewModel(test.Services);
        var sharpness = preferences.Sharpness;

        sharpness.Preset = (int)SharpnessPreset.Crisp;

        var saved = new SettingsStore(path).Load().Sharpness;
        await Assert.That(saved).IsEqualTo(SharpnessPresets.Get(SharpnessPreset.Crisp)!);
        await Assert.That(sharpness.TextEdges).IsEqualTo((int)TextEdgeChoice.Sharp);
        await Assert.That(sharpness.SmoothGraphics).IsFalse();
        await Assert.That(sharpness.SmoothImages).IsFalse();
        await Assert.That(sharpness.StrokeAdjustment).IsEqualTo((int)StrokeAdjustmentChoice.Always);

        sharpness.Preset = (int)SharpnessPreset.Smooth;

        await Assert.That(test.Services.Settings.Sharpness).IsEqualTo(RenderSharpness.Default);
        await Assert.That(sharpness.SmoothImages).IsTrue();
        await Assert.That(sharpness.TextEdges).IsEqualTo((int)TextEdgeChoice.Smooth);
    }

    /// <summary>Picking Custom changes nothing; changing an individual choice moves the set to Custom or to the set that matches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingAChoiceMovesTheSet()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);
        var sharpness = preferences.Sharpness;
        sharpness.Preset = (int)SharpnessPreset.Sharp;

        sharpness.Preset = (int)SharpnessPreset.Custom;
        await Assert.That(test.Services.Settings.Sharpness).IsEqualTo(SharpnessPresets.Get(SharpnessPreset.Sharp)!);

        sharpness.SmoothImages = false;
        await Assert.That(sharpness.Preset).IsEqualTo((int)SharpnessPreset.Custom);
        await Assert.That(sharpness.PresetDescription).IsEqualTo(SharpnessViewModel.DescribePreset((int)SharpnessPreset.Custom));

        sharpness.SmoothImages = true;
        await Assert.That(sharpness.Preset).IsEqualTo((int)SharpnessPreset.Sharp);

        sharpness.TextEdges = (int)TextEdgeChoice.Smooth;
        await Assert.That(sharpness.Preset).IsEqualTo((int)SharpnessPreset.Smooth);
    }

    /// <summary>A set picked in the window reaches a document that is already open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APickedSetReachesTheOpenDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("set.pdf", 1)]);
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.Preset = (int)SharpnessPreset.Crisp;
        var document = main.SelectedTab!.TryGetDocument()!;
        var quality = HyperPdfFormRuntime.CreateRenderOptions((HyperPdfDocument)document).Quality;

        await Assert.That(DocumentFeature<IRenderSharpness>.Get(document)!.Sharpness).IsEqualTo(SharpnessPresets.Get(SharpnessPreset.Crisp)!);
        await Assert.That(quality.TextEdges).IsEqualTo(PdfTextEdges.Sharp);
        await Assert.That(quality.SmoothImages).IsFalse();
        await Assert.That(quality.StrokeAdjustment).IsEqualTo(PdfStrokeAdjustment.Always);
    }

    /// <summary>The set box shows the saved set, writes the one picked and shows what it does; the advanced choices follow it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetBoxFillsInTheAdvancedChoices()
    {
        using var test = new TestServices();
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        try
        {
            window.Show();
            var set = window.FindControl<ComboBox>(PresetBox)!;
            var text = window.FindControl<TextBlock>("SharpnessPresetText")!;
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            await Assert.That(set.SelectedIndex).IsEqualTo((int)SharpnessPreset.Smooth);
            await Assert.That(text.Text).IsEqualTo(SharpnessViewModel.DescribePreset((int)SharpnessPreset.Smooth));

            set.SelectedIndex = (int)SharpnessPreset.Crisp;

            await Assert.That(await UiWait.UntilAsync(() => window.FindControl<CheckBox>(ImagesBox)!.IsChecked == false)).IsTrue();
            await Assert.That(text.Text).IsEqualTo(SharpnessViewModel.DescribePreset((int)SharpnessPreset.Crisp));
            await Assert.That(window.FindControl<ComboBox>(EdgesBox)!.SelectedIndex).IsEqualTo((int)TextEdgeChoice.Sharp);
            await Assert.That(window.FindControl<ComboBox>(StrokeBox)!.SelectedIndex).IsEqualTo((int)StrokeAdjustmentChoice.Always);

            window.FindControl<CheckBox>(GraphicsBox)!.IsChecked = true;

            await Assert.That(await UiWait.UntilAsync(() => set.SelectedIndex == (int)SharpnessPreset.Custom)).IsTrue();
            await Assert.That(text.Text).IsEqualTo(SharpnessViewModel.DescribePreset((int)SharpnessPreset.Custom));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The advanced group starts closed, opens from the keyboard and keeps its choices on screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdvancedGroupOpensFromTheKeyboard()
    {
        using var test = new TestServices();
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        try
        {
            window.Show();
            var advanced = window.FindControl<Expander>(AdvancedGroup)!;
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            await Assert.That(advanced.IsExpanded).IsFalse();

            var header = advanced.GetVisualDescendants().OfType<ToggleButton>().First();
            await Assert.That(header.Focus()).IsTrue();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);

            await Assert.That(await UiWait.UntilAsync(() => advanced.IsExpanded)).IsTrue();
            await Assert.That(window.FindControl<ComboBox>(EdgesBox)!.IsEffectivelyVisible).IsTrue();
            await Assert.That(ControlAutomationPeer.CreatePeerForElement(advanced).GetName()).IsEqualTo("Advanced sharpness choices");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every sharpness control has a name a screen reader can say, and is linked to its label.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharpnessControlsHaveNames()
    {
        using var test = new TestServices();
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        try
        {
            window.Show();
            window.FindControl<Expander>(AdvancedGroup)!.IsExpanded = true;
            await Assert.That(await UiWait.UntilAsync(() => window.FindControl<ComboBox>(EdgesBox)!.IsEffectivelyVisible)).IsTrue();

            foreach (var name in ControlNames)
            {
                var control = window.FindControl<Control>(name)!;
                var accessibleName = ControlAutomationPeer.CreatePeerForElement(control).GetName();
                await Assert.That(string.IsNullOrWhiteSpace(accessibleName)).IsFalse();
                await Assert.That(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(control))).IsFalse();
            }

            await Assert.That(AutomationProperties.GetLabeledBy(window.FindControl<ComboBox>(PresetBox)!)).IsSameReferenceAs(window.FindControl<TextBlock>("SharpnessPresetLabel"));
            await Assert.That(AutomationProperties.GetLabeledBy(window.FindControl<ComboBox>(EdgesBox)!)).IsSameReferenceAs(window.FindControl<TextBlock>("TextEdgesLabel"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>With large interface text or a narrow window, the sharpness controls stay inside the window and do not overlap.</summary>
    /// <param name="points">The interface text size in points.</param>
    /// <param name="width">The window width.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0D, PreferencesWidth)]
    [Arguments(LargeTextPoints, PreferencesWidth)]
    [Arguments(LargeTextPoints, NarrowWidth)]
    public async Task SharpnessControlsFitLargeTextAndNarrowWindows(double points, double width)
    {
        using var test = new TestServices();
        test.Services.Settings.InterfaceFontSizePoints = points > 0 ? points : null;
        test.Services.ApplySettings();
        var theme = test.Services.CurrentTheme;
        var window = new PreferencesWindow { ViewModel = new(test.Services), Width = width };

        // The theme and font go on this window alone. Writing them to the application notifies every window earlier
        // tests left behind, which takes minutes each time on a macOS runner late in the run.
        DesktopThemeApplier.ApplyResources(window.Resources, theme.Scheme, theme);
        DesktopThemeApplier.ApplyFont(window, theme);
        try
        {
            window.Show();
            window.FindControl<Expander>(AdvancedGroup)!.IsExpanded = true;
            await Assert.That(await UiWait.UntilAsync(() => window.FindControl<CheckBox>(AreaBox)!.IsEffectivelyVisible)).IsTrue();

            var controls = ControlNames
                .Select(name => window.FindControl<Control>(name)!)
                .ToArray();
            foreach (var control in controls)
            {
                var origin = control.TranslatePoint(default, window)!.Value;
                await Assert.That(origin.X).IsGreaterThanOrEqualTo(0);
                await Assert.That(origin.X + control.Bounds.Width).IsLessThanOrEqualTo(width);
                await Assert.That(control.Bounds.Height).IsGreaterThan(0);
            }

            var tops = controls.Select(control => control.TranslatePoint(default, window)!.Value.Y).ToArray();
            await Assert.That(tops.Distinct().Count()).IsEqualTo(tops.Length);
        }
        finally
        {
            window.Close();
            test.Services.Settings.InterfaceFontSizePoints = null;
        }
    }
}
