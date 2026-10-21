// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>
/// The text and image sharpness choices. A named set (smooth, sharp or crisp) fills in the individual choices, and changing
/// an individual choice moves the named set to the one that matches, or to Custom. Each choice applies at once and is saved;
/// the list indexes match the explicit values of the render enums.
/// </summary>
[DebuggerDisplay("SharpnessViewModel")]
public sealed partial class SharpnessViewModel : ReactiveObject, IDisposable
{
    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Subscriptions writing each choice to the settings.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether the view model is setting its own values, so the change handlers leave them alone.</summary>
    private bool _syncing;

    /// <summary>Initializes a new instance of the <see cref="SharpnessViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public SharpnessViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        Load();
        _presetDescriptionHelper = this.WhenChanged(static x => x.Preset).Select(DescribePreset).ToProperty(this, static x => x.PresetDescription);
        _subscriptions.Add(_presetDescriptionHelper);
        Follow();
    }

    /// <summary>Gets the named sets in enum order.</summary>
    public static IReadOnlyList<string> PresetOptions { get; } = ["Smooth", "Sharp", "Crisp", "Custom"];

    /// <summary>Gets the text edge choices in enum order.</summary>
    public static IReadOnlyList<string> TextEdgeOptions { get; } = ["Smooth", "Sharp", "LCD"];

    /// <summary>Gets the small text hinting choices in enum order.</summary>
    public static IReadOnlyList<string> TextHintingOptions { get; } = ["None", "Slight", "Full"];

    /// <summary>Gets the small text contrast choices; the index is the contrast level.</summary>
    public static IReadOnlyList<string> TextContrastOptions { get; } = ["Normal", "Darker", "Darkest", "Boldest"];

    /// <summary>Gets the stroke adjustment choices in enum order.</summary>
    public static IReadOnlyList<string> StrokeAdjustmentOptions { get; } = ["Off", "When the page asks", "Always"];

    /// <summary>Gets the minimum line thickness choices; the index is the number of pixels.</summary>
    public static IReadOnlyList<string> MinimumLineOptions { get; } = ["Off", "1 pixel", "2 pixels", "3 pixels"];

    /// <summary>Gets or sets the named set index; Custom when the individual choices match no named set.</summary>
    [Reactive]
    public partial int Preset { get; set; }

    /// <summary>Gets what the chosen named set does.</summary>
    [ObservableAsProperty]
    public partial string PresetDescription { get; }

    /// <summary>Gets or sets the text edge index.</summary>
    [Reactive]
    public partial int TextEdges { get; set; }

    /// <summary>Gets or sets a value indicating whether the edges of shapes and lines are smoothed.</summary>
    [Reactive]
    public partial bool SmoothGraphics { get; set; }

    /// <summary>Gets or sets a value indicating whether scaled images are smoothed.</summary>
    [Reactive]
    public partial bool SmoothImages { get; set; }

    /// <summary>Gets or sets the stroke adjustment index.</summary>
    [Reactive]
    public partial int StrokeAdjustment { get; set; }

    /// <summary>Gets or sets the minimum line thickness index, which is also the number of pixels.</summary>
    [Reactive]
    public partial int MinimumLineWidth { get; set; }

    /// <summary>Gets or sets a value indicating whether big image reductions average the pixels they cover.</summary>
    [Reactive]
    public partial bool AreaImageReduction { get; set; }

    /// <summary>Gets or sets a value indicating whether small text is drawn from remembered glyph shapes.</summary>
    [Reactive]
    public partial bool SmallTextMasks { get; set; }

    /// <summary>Gets or sets the small text hinting index.</summary>
    [Reactive]
    public partial int TextHinting { get; set; }

    /// <summary>Gets or sets the small text contrast index, which is also the contrast level.</summary>
    [Reactive]
    public partial int TextContrast { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Describes a named set.</summary>
    /// <param name="index">The set index.</param>
    /// <returns>What the set does, or an empty text for an index that is no set.</returns>
    internal static string DescribePreset(int index) => (SharpnessPreset)index switch
    {
        SharpnessPreset.Smooth => "Text, lines and pictures are drawn with soft edges that blend into the page.",
        SharpnessPreset.Sharp => "Text is drawn with hard edges, where each pixel is fully dark or fully clear. Lines and pictures keep soft edges.",
        SharpnessPreset.Crisp => "Text, lines and pictures are drawn with hard edges, and lines are fitted to whole screen pixels.",
        SharpnessPreset.Custom => "The choices under Advanced match none of the other three.",
        _ => string.Empty,
    };

    /// <summary>Loads each choice from the settings.</summary>
    private void Load()
    {
        var sharpness = _services.Settings.Sharpness;
        _syncing = true;
        try
        {
            Show(sharpness);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Shows saved choices in the individual choices and the named set. The caller holds <see cref="_syncing"/>.</summary>
    /// <param name="sharpness">The choices.</param>
    private void Show(RenderSharpness sharpness)
    {
        Preset = (int)SharpnessPresets.Match(sharpness);
        TextEdges = (int)sharpness.TextEdges;
        SmoothGraphics = sharpness.SmoothGraphics;
        SmoothImages = sharpness.SmoothImages;
        StrokeAdjustment = (int)sharpness.StrokeAdjustment;
        MinimumLineWidth = sharpness.MinimumLineWidth;
        AreaImageReduction = sharpness.AreaImageReduction;
        SmallTextMasks = sharpness.SmallTextMasks;
        TextHinting = (int)sharpness.TextHinting;
        TextContrast = sharpness.TextDarkness;
    }

    /// <summary>Writes each later change of a choice to the settings.</summary>
    private void Follow()
    {
        _subscriptions.Add(this.WhenChanged(static x => x.Preset).Skip(1).SubscribeSafe(ApplyPreset, ErrorTrace.Write));
        Follow(this.WhenChanged(static x => x.TextEdges), static (s, index) => s with { TextEdges = (TextEdgeChoice)index });
        Follow(this.WhenChanged(static x => x.SmoothGraphics), static (s, on) => s with { SmoothGraphics = on });
        Follow(this.WhenChanged(static x => x.SmoothImages), static (s, on) => s with { SmoothImages = on });
        Follow(this.WhenChanged(static x => x.StrokeAdjustment), static (s, index) => s with { StrokeAdjustment = (StrokeAdjustmentChoice)index });
        Follow(this.WhenChanged(static x => x.MinimumLineWidth), static (s, pixels) => s with { MinimumLineWidth = pixels });
        Follow(this.WhenChanged(static x => x.AreaImageReduction), static (s, on) => s with { AreaImageReduction = on });
        Follow(this.WhenChanged(static x => x.SmallTextMasks), static (s, on) => s with { SmallTextMasks = on });
        Follow(this.WhenChanged(static x => x.TextHinting), static (s, index) => s with { TextHinting = (TextHintingChoice)index });
        Follow(this.WhenChanged(static x => x.TextContrast), static (s, level) => s with { TextDarkness = level });
    }

    /// <summary>Applies each later value of a choice to the settings, then saves.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The choice's changes; the first value is the loaded one and is skipped.</param>
    /// <param name="change">Gives the choices with the value applied.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Follow<T>(IObservable<T> source, Func<RenderSharpness, T, RenderSharpness> change) =>
        _subscriptions.Add(source.Skip(1).SubscribeSafe(
            value =>
            {
                if (_syncing)
                {
                    return;
                }

                // The setter brings the value into range, so an out of range index still gives a working choice.
                _services.Settings.Sharpness = change(_services.Settings.Sharpness, value);
                _services.ApplySettings();
                SelectMatchingPreset();
            },
            ErrorTrace.Write));

    /// <summary>Moves the named set to the one that matches the individual choices, or to Custom.</summary>
    private void SelectMatchingPreset()
    {
        _syncing = true;
        try
        {
            Preset = (int)SharpnessPresets.Match(_services.Settings.Sharpness);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Applies a named set when the person picks one: every individual choice takes the set's value.</summary>
    /// <param name="index">The picked set index.</param>
    private void ApplyPreset(int index)
    {
        if (_syncing || SharpnessPresets.Get((SharpnessPreset)index) is not { } choices)
        {
            return;
        }

        _services.Settings.Sharpness = choices;
        _syncing = true;
        try
        {
            Show(_services.Settings.Sharpness);
        }
        finally
        {
            _syncing = false;
        }

        _services.ApplySettings();
    }
}
