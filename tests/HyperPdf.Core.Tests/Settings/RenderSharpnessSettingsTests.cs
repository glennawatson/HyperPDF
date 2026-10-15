// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Settings;

/// <summary>Tests that the page sharpness choices save, load and stay inside their ranges.</summary>
public sealed class RenderSharpnessSettingsTests
{
    /// <summary>A line width within range.</summary>
    private const int LineWidth = 3;

    /// <summary>A value that is none of the named sets.</summary>
    private const SharpnessPreset NotAPreset = (SharpnessPreset)99;

    /// <summary>A line width above the range.</summary>
    private const int TooWideLine = 13;

    /// <summary>A line width below the range.</summary>
    private const int NegativeWidth = -2;

    /// <summary>Fresh settings hold the default choices: smooth edges and area reduction on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultsAreSmoothAndPlain()
    {
        var sharpness = new AppSettings().Sharpness;

        await Assert.That(sharpness).IsEqualTo(RenderSharpness.Default);
        await Assert.That(sharpness.TextEdges).IsEqualTo(TextEdgeChoice.Smooth);
        await Assert.That(sharpness.SmoothGraphics).IsTrue();
        await Assert.That(sharpness.SmoothImages).IsTrue();
        await Assert.That(sharpness.AreaImageReduction).IsTrue();
        await Assert.That(sharpness.StrokeAdjustment).IsEqualTo(StrokeAdjustmentChoice.Off);
        await Assert.That(sharpness.MinimumLineWidth).IsEqualTo(0);
    }

    /// <summary>Every choice survives a save and a load.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryChoiceRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}", "settings.json");
        var chosen = new RenderSharpness
        {
            TextEdges = TextEdgeChoice.Sharp,
            SmoothGraphics = false,
            SmoothImages = false,
            StrokeAdjustment = StrokeAdjustmentChoice.Always,
            MinimumLineWidth = LineWidth,
            AreaImageReduction = false,
        };
        var store = new SettingsStore(path);

        store.Save(new AppSettings { Sharpness = chosen });
        var loaded = store.Load();

        await Assert.That(loaded.Sharpness).IsEqualTo(chosen);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    /// <summary>A file written by an earlier version has no sharpness and gets the defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFileWithoutSharpnessGivesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"showSidebar\": false }");

        var loaded = new SettingsStore(path).Load();

        await Assert.That(loaded.ShowSidebar).IsFalse();
        await Assert.That(loaded.Sharpness).IsEqualTo(RenderSharpness.Default);
        File.Delete(path);
    }

    /// <summary>A null value gives the defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ANullSharpnessGivesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"sharpness\": null }");

        var loaded = new SettingsStore(path).Load();

        await Assert.That(loaded.Sharpness).IsEqualTo(RenderSharpness.Default);
        File.Delete(path);
    }

    /// <summary>Numbers and names out of range load as the closest working choice instead of failing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutOfRangeValuesAreBroughtIntoRange()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        var json = string.Concat(
            "{ \"sharpness\": { \"textEdges\": 99, \"minimumLineWidth\": ",
            NegativeWidth,
            ", \"strokeAdjustment\": -1 } }");
        await File.WriteAllTextAsync(path, json);

        var sharpness = new SettingsStore(path).Load().Sharpness;

        await Assert.That(sharpness.TextEdges).IsEqualTo(TextEdgeChoice.Smooth);
        await Assert.That(sharpness.MinimumLineWidth).IsEqualTo(0);
        await Assert.That(sharpness.StrokeAdjustment).IsEqualTo(StrokeAdjustmentChoice.Off);
        File.Delete(path);
    }

    /// <summary>A file saved with the removed small text choices still loads, and those values fall back to the defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedChoicesInASavedFileAreIgnored()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        const string Json = "{ \"sharpness\": { \"textEdges\": 1, \"textContrast\": 75, \"glyphPlacement\": 1, \"fastSmallText\": true, "
            + "\"smoothGraphics\": true, \"smoothImages\": true, \"strokeAdjustment\": 0, \"minimumLineWidth\": 0, \"areaImageReduction\": true } }";
        await File.WriteAllTextAsync(path, Json);

        var sharpness = new SettingsStore(path).Load().Sharpness;

        await Assert.That(sharpness).IsEqualTo(RenderSharpness.Default with { TextEdges = TextEdgeChoice.Sharp });
        File.Delete(path);
    }

    /// <summary>A saved file that names only some sharpness choices keeps the defaults for the ones it leaves out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoicesLeftOutOfASavedFileKeepTheirDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"sharpness\": { \"textEdges\": 1 } }");

        var sharpness = new SettingsStore(path).Load().Sharpness;

        await Assert.That(sharpness).IsEqualTo(RenderSharpness.Default with { TextEdges = TextEdgeChoice.Sharp });
        File.Delete(path);
    }

    /// <summary>The named sets hold the choices they describe.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedSetsHoldTheirChoices()
    {
        var smooth = SharpnessPresets.Get(SharpnessPreset.Smooth)!;
        var sharp = SharpnessPresets.Get(SharpnessPreset.Sharp)!;
        var crisp = SharpnessPresets.Get(SharpnessPreset.Crisp)!;

        await Assert.That(smooth).IsEqualTo(RenderSharpness.Default);
        await Assert.That(sharp.TextEdges).IsEqualTo(TextEdgeChoice.Sharp);
        await Assert.That(sharp.SmoothGraphics && sharp.SmoothImages).IsTrue();
        await Assert.That(crisp.TextEdges).IsEqualTo(TextEdgeChoice.Sharp);
        await Assert.That(crisp.SmoothGraphics || crisp.SmoothImages).IsFalse();
        await Assert.That(crisp.StrokeAdjustment).IsEqualTo(StrokeAdjustmentChoice.Always);
        await Assert.That(SharpnessPresets.Get(SharpnessPreset.Custom)).IsNull();
        await Assert.That(SharpnessPresets.Get(NotAPreset)).IsNull();
    }

    /// <summary>Each named set is recognised from its choices, and any other mix is Custom.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchFindsTheNamedSetOrCustom()
    {
        await Assert.That(SharpnessPresets.Match(RenderSharpness.Default)).IsEqualTo(SharpnessPreset.Smooth);
        await Assert.That(SharpnessPresets.Match(SharpnessPresets.Get(SharpnessPreset.Sharp)!)).IsEqualTo(SharpnessPreset.Sharp);
        await Assert.That(SharpnessPresets.Match(SharpnessPresets.Get(SharpnessPreset.Crisp)!)).IsEqualTo(SharpnessPreset.Crisp);
        await Assert.That(SharpnessPresets.Match(new() { TextEdges = TextEdgeChoice.Sharp, MinimumLineWidth = 1 })).IsEqualTo(SharpnessPreset.Custom);
    }

    /// <summary>The line width limit is the largest offered thickness.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TooWideMinimumLineIsClamped()
    {
        var settings = new AppSettings { Sharpness = new() { MinimumLineWidth = TooWideLine } };

        await Assert.That(settings.Sharpness.MinimumLineWidth).IsEqualTo(RenderSharpness.MaxMinimumLineWidth);
    }
}
