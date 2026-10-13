// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Decides which OpenType features a script needs and in what order (OpenType specification: "Developing OpenType Fonts for
/// Arabic Script", "Developing OpenType Fonts for Devanagari Script" and "Default features"), and compiles them into the
/// lookups of a font.
/// </summary>
internal static class PlanBuilder
{
    /// <summary>The index of the Indic stage that applies the rphf feature.</summary>
    internal const int IndicReph = 3;

    /// <summary>The index of the Indic stage after which the pre-base consonants have their half forms.</summary>
    internal const int IndicLastBasic = 11;

    /// <summary>Builds the plan for a script in a font.</summary>
    /// <param name="font">The font's layout tables.</param>
    /// <param name="script">The script.</param>
    /// <param name="language">The OpenType language tag, or zero.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <param name="vertical">Whether the run is written vertically.</param>
    /// <param name="overrides">The caller's feature settings, or <see langword="null"/>.</param>
    /// <returns>The plan.</returns>
    internal static ShapePlan Build(OtFontLayout font, ShapingScript script, uint language, bool rightToLeft, bool vertical, IReadOnlyList<PdfFontFeature>? overrides)
    {
        var substitution = script.Kind switch
        {
            ShapingKind.Arabic => ArabicSubstitution(rightToLeft),
            ShapingKind.Indic => IndicSubstitution(),
            _ => DefaultSubstitution(rightToLeft, vertical),
        };
        var positioning = vertical ? new List<List<FeatureRequest>>() : PositioningFor(script.Kind);
        Override(substitution, positioning, overrides);
        var gsub = PlanCompiler.Compile(font.Substitution, script.Tags, language, Freeze(substitution));
        var gpos = PlanCompiler.Compile(font.Positioning, script.Tags, language, Freeze(positioning));
        var hasKerning = !vertical && Array.Exists(PlanCompiler.Compile(font.Positioning, script.Tags, language, [[FeatureRequest.Everywhere(OtTags.Kern)]]), static stage => stage.Length > 0);
        return new(gsub, gpos, !vertical && !hasKerning && !font.LegacyKerning.IsEmpty && !IsDisabled(overrides, OtTags.Kern));
    }

    /// <summary>Makes the default substitution stages for scripts that need no special handling.</summary>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <param name="vertical">Whether the run is written vertically.</param>
    /// <returns>The stages.</returns>
    private static List<List<FeatureRequest>> DefaultSubstitution(bool rightToLeft, bool vertical)
    {
        List<FeatureRequest> first = [Every(OtTags.Ccmp), Every(OtTags.Locl)];
        if (rightToLeft)
        {
            first.Add(Every(OtTags.Rtlm));
        }

        if (vertical)
        {
            first.Add(Every(OtTags.Vert));
            first.Add(Every(OtTags.Vrt2));
        }

        return [first, Features(OtTags.Rlig, OtTags.Calt, OtTags.Rclt, OtTags.Liga, OtTags.Clig)];
    }

    /// <summary>Makes the substitution stages for Arabic script: joining forms first, then ligatures and contextual forms.</summary>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <returns>The stages.</returns>
    private static List<List<FeatureRequest>> ArabicSubstitution(bool rightToLeft)
    {
        List<FeatureRequest> first = [Every(OtTags.Ccmp), Every(OtTags.Locl)];
        if (rightToLeft)
        {
            first.Add(Every(OtTags.Rtlm));
        }

        List<FeatureRequest> forms =
        [
            new(OtTags.Isol, ShapeMasks.Isolated, 1),
            new(OtTags.Fina, ShapeMasks.Final, 1),
            new(OtTags.Medi, ShapeMasks.Medial, 1),
            new(OtTags.Init, ShapeMasks.Initial, 1),
        ];
        return [first, forms, Features(OtTags.Rlig, OtTags.Calt, OtTags.Rclt, OtTags.Liga, OtTags.Clig)];
    }

    /// <summary>Makes the substitution stages for Indic scripts: each basic feature in its own stage, so the shaper can reorder between them.</summary>
    /// <returns>The stages.</returns>
    private static List<List<FeatureRequest>> IndicSubstitution() =>
    [
        Features(OtTags.Ccmp, OtTags.Locl),
        Features(OtTags.Nukt),
        Features(OtTags.Akhn),
        [new(OtTags.Rphf, ShapeMasks.Reph, 1)],
        Features(OtTags.Rkrf),
        Features(OtTags.Pref),
        Features(OtTags.Blwf),
        Features(OtTags.Abvf),
        [new(OtTags.Half, ShapeMasks.PreBase, 1)],
        Features(OtTags.Pstf),
        Features(OtTags.Vatu),
        Features(OtTags.Cjct),
        Features(OtTags.Pres, OtTags.Abvs, OtTags.Blws, OtTags.Psts, OtTags.Haln, OtTags.Rlig, OtTags.Calt, OtTags.Rclt, OtTags.Liga, OtTags.Clig),
    ];

    /// <summary>Makes the positioning stage for a script.</summary>
    /// <param name="kind">The script's shaping model.</param>
    /// <returns>The stages.</returns>
    private static List<List<FeatureRequest>> PositioningFor(ShapingKind kind) => kind switch
    {
        ShapingKind.Indic => [Features(OtTags.Dist, OtTags.Abvm, OtTags.Blwm, OtTags.Kern, OtTags.Mark, OtTags.Mkmk, OtTags.Curs)],
        ShapingKind.Arabic => [Features(OtTags.Curs, OtTags.Kern, OtTags.Mark, OtTags.Mkmk)],
        _ => [Features(OtTags.Kern, OtTags.Mark, OtTags.Mkmk, OtTags.Curs, OtTags.Dist)],
    };

    /// <summary>Makes a request for a feature that every glyph takes.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <returns>The request.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FeatureRequest Every(uint tag) => FeatureRequest.Everywhere(tag);

    /// <summary>Makes a stage of features that every glyph takes.</summary>
    /// <param name="tags">The feature tags.</param>
    /// <returns>The stage.</returns>
    private static List<FeatureRequest> Features(params uint[] tags)
    {
        var stage = new List<FeatureRequest>(tags.Length);
        foreach (var tag in tags)
        {
            stage.Add(Every(tag));
        }

        return stage;
    }

    /// <summary>Turns the stages into arrays.</summary>
    /// <param name="stages">The stages.</param>
    /// <returns>The arrays.</returns>
    private static FeatureRequest[][] Freeze(List<List<FeatureRequest>> stages)
    {
        var frozen = new FeatureRequest[stages.Count][];
        for (var i = 0; i < frozen.Length; i++)
        {
            frozen[i] = [.. stages[i]];
        }

        return frozen;
    }

    /// <summary>Determines whether the caller turned a feature off.</summary>
    /// <param name="overrides">The caller's feature settings.</param>
    /// <param name="tag">The feature tag.</param>
    /// <returns><see langword="true"/> when the feature is set to zero.</returns>
    private static bool IsDisabled(IReadOnlyList<PdfFontFeature>? overrides, uint tag)
    {
        if (overrides is null)
        {
            return false;
        }

        foreach (var feature in overrides)
        {
            if (feature.Value == 0 && OtTag.Of(feature.Tag) == tag)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Applies the caller's feature settings: removes the features turned off and adds, or sets the value of, the ones turned on.</summary>
    /// <param name="substitution">The substitution stages, changed in place.</param>
    /// <param name="positioning">The positioning stages, changed in place.</param>
    /// <param name="overrides">The caller's feature settings.</param>
    private static void Override(List<List<FeatureRequest>> substitution, List<List<FeatureRequest>> positioning, IReadOnlyList<PdfFontFeature>? overrides)
    {
        if (overrides is null)
        {
            return;
        }

        foreach (var feature in overrides)
        {
            var tag = OtTag.Of(feature.Tag);
            var inSubstitution = Set(substitution, tag, feature.Value);
            var inPositioning = Set(positioning, tag, feature.Value);
            AddUnlisted(substitution, positioning, new(tag, ShapeMasks.Global, feature.Value), inSubstitution || inPositioning);
        }
    }

    /// <summary>Adds a feature the caller turned on that the script does not use by default, to the last stage of each table.</summary>
    /// <param name="substitution">The substitution stages.</param>
    /// <param name="positioning">The positioning stages.</param>
    /// <param name="request">The feature.</param>
    /// <param name="listed">Whether a stage already had the feature.</param>
    private static void AddUnlisted(List<List<FeatureRequest>> substitution, List<List<FeatureRequest>> positioning, FeatureRequest request, bool listed)
    {
        if (listed || request.Value <= 0)
        {
            return;
        }

        substitution[^1].Add(request);
        if (positioning.Count > 0)
        {
            positioning[^1].Add(request);
        }
    }

    /// <summary>Sets the value of a feature in every stage that has it, removing it when the value is zero.</summary>
    /// <param name="stages">The stages.</param>
    /// <param name="tag">The feature tag.</param>
    /// <param name="value">The new value.</param>
    /// <returns><see langword="true"/> when a stage had the feature.</returns>
    private static bool Set(List<List<FeatureRequest>> stages, uint tag, int value)
    {
        var found = false;
        foreach (var stage in stages)
        {
            for (var i = stage.Count - 1; i >= 0; i--)
            {
                if (stage[i].Tag != tag)
                {
                    continue;
                }

                found = true;
                if (value == 0)
                {
                    stage.RemoveAt(i);
                }
                else
                {
                    stage[i] = stage[i] with { Value = value };
                }
            }
        }

        return found;
    }
}
