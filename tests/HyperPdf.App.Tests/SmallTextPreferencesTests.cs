// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.App.Tests;

/// <summary>Checks the small text choices: remembered glyph shapes, fitting to pixels, contrast and LCD edges.</summary>
public sealed class SmallTextPreferencesTests
{
    /// <summary>The settings file used by the tests.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The contrast level the tests choose.</summary>
    private const int ChosenContrast = 2;

    /// <summary>A contrast index past the end of the list.</summary>
    private const int OutOfRangeContrast = 99;

    /// <summary>A hinting index past the end of the list.</summary>
    private const int OutOfRangeHinting = 99;

    /// <summary>Verifies that each small text choice is saved in the settings file and read back.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Choices_RoundTripThroughTheSettingsFile()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, SettingsFile);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            var sharpness = preferences.Sharpness;
            sharpness.SmallTextMasks = true;
            sharpness.TextHinting = (int)TextHintingChoice.Slight;
            sharpness.TextContrast = ChosenContrast;
            sharpness.TextEdges = (int)TextEdgeChoice.Lcd;
        }

        var saved = new SettingsStore(path).Load().Sharpness;
        using var nextRun = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform());
        using var reloaded = new PreferencesViewModel(nextRun);

        await Assert.That(saved.SmallTextMasks).IsTrue();
        await Assert.That(saved.TextHinting).IsEqualTo(TextHintingChoice.Slight);
        await Assert.That(saved.TextContrast).IsEqualTo(ChosenContrast);
        await Assert.That(saved.TextEdges).IsEqualTo(TextEdgeChoice.Lcd);
        await Assert.That(reloaded.Sharpness.SmallTextMasks).IsTrue();
        await Assert.That(reloaded.Sharpness.TextHinting).IsEqualTo((int)TextHintingChoice.Slight);
        await Assert.That(reloaded.Sharpness.TextContrast).IsEqualTo(ChosenContrast);
    }

    /// <summary>Verifies that everything starts as it was before: no remembered shapes, no fitting, normal contrast.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StartsWithThePlainChoices()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        await Assert.That(preferences.Sharpness.SmallTextMasks).IsFalse();
        await Assert.That(SharpnessViewModel.TextHintingOptions[preferences.Sharpness.TextHinting]).IsEqualTo("None");
        await Assert.That(SharpnessViewModel.TextContrastOptions[preferences.Sharpness.TextContrast]).IsEqualTo("Normal");
        await Assert.That(preferences.Sharpness.Preset).IsEqualTo((int)SharpnessPreset.Smooth);
    }

    /// <summary>Verifies that a choice other than the plain ones moves the named set to Custom.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ASmallTextChoice_MovesTheSetToCustom()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.TextContrast = 1;

        await Assert.That(preferences.Sharpness.Preset).IsEqualTo((int)SharpnessPreset.Custom);
    }

    /// <summary>Verifies that an index out of range still gives a working choice.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OutOfRangeIndexes_GiveWorkingChoices()
    {
        using var test = new TestServices();
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.TextContrast = OutOfRangeContrast;
        preferences.Sharpness.TextHinting = OutOfRangeHinting;

        await Assert.That(test.Services.Settings.Sharpness.TextContrast).IsEqualTo(RenderSharpness.MaxTextContrast);
        await Assert.That(test.Services.Settings.Sharpness.TextHinting).IsEqualTo(TextHintingChoice.None);
    }

    /// <summary>Verifies that the choices reach HyperPDF's rendering of an open document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Choices_ReachTheOpenDocumentsRendering()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("small-text.pdf", 1)]);
        using var preferences = new PreferencesViewModel(test.Services);

        preferences.Sharpness.TextEdges = (int)TextEdgeChoice.Lcd;
        preferences.Sharpness.TextHinting = (int)TextHintingChoice.Full;
        preferences.Sharpness.TextContrast = ChosenContrast;
        preferences.Sharpness.SmallTextMasks = true;
        var quality = HyperPdfFormRuntime.CreateRenderOptions((HyperPdfDocument)main.SelectedTab!.TryGetDocument()!).Quality;

        await Assert.That(quality.TextEdges).IsEqualTo(PdfTextEdges.Lcd);
        await Assert.That(quality.TextHinting).IsEqualTo(PdfTextHinting.Full);
        await Assert.That(quality.TextContrast).IsEqualTo(ChosenContrast);
        await Assert.That(quality.GlyphMasks).IsTrue();
        await Assert.That(quality.UsesGlyphMasks).IsTrue();
    }
}
