// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Maps the named sharpness sets to the individual choices they stand for, and back.</summary>
public static class SharpnessPresets
{
    /// <summary>The choices of the Sharp set: hard text edges.</summary>
    private static readonly RenderSharpness SharpChoices = RenderSharpness.Default with { TextEdges = TextEdgeChoice.Sharp };

    /// <summary>The choices of the Crisp set: hard edges everywhere and lines fitted to whole pixels.</summary>
    private static readonly RenderSharpness CrispChoices = SharpChoices with
    {
        SmoothGraphics = false,
        SmoothImages = false,
        StrokeAdjustment = StrokeAdjustmentChoice.Always,
    };

    /// <summary>Gets the individual choices of a named set.</summary>
    /// <param name="preset">The set.</param>
    /// <returns>The choices, or <see langword="null"/> for <see cref="SharpnessPreset.Custom"/> and values that are not a set.</returns>
    public static RenderSharpness? Get(SharpnessPreset preset) => preset switch
    {
        SharpnessPreset.Smooth => RenderSharpness.Default,
        SharpnessPreset.Sharp => SharpChoices,
        SharpnessPreset.Crisp => CrispChoices,
        _ => null,
    };

    /// <summary>Finds the named set that holds exactly the given choices.</summary>
    /// <param name="sharpness">The choices.</param>
    /// <returns>The set, or <see cref="SharpnessPreset.Custom"/> when none matches.</returns>
    public static SharpnessPreset Match(RenderSharpness sharpness)
    {
        ArgumentNullException.ThrowIfNull(sharpness);
        var normal = sharpness.Normalize();
        if (normal == RenderSharpness.Default)
        {
            return SharpnessPreset.Smooth;
        }

        if (normal == SharpChoices)
        {
            return SharpnessPreset.Sharp;
        }

        return normal == CrispChoices ? SharpnessPreset.Crisp : SharpnessPreset.Custom;
    }
}
