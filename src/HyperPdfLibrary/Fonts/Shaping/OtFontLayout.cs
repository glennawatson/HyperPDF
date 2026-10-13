// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>A font's layout tables (GSUB, GPOS, GDEF and the legacy kern table), with the shaping plans made from them kept for reuse.</summary>
[DebuggerDisplay("OtFontLayout: {Substitution.LookupCount} substitutions, {Positioning.LookupCount} positionings")]
internal sealed class OtFontLayout
{
    /// <summary>The plans made so far.</summary>
    private readonly ConcurrentDictionary<PlanKey, ShapePlan> _plans = new();

    /// <summary>Initializes a new instance of the <see cref="OtFontLayout"/> class.</summary>
    /// <param name="font">The font face.</param>
    internal OtFontLayout(SfntFont font)
    {
        Substitution = new(font.TableMemory(SfntTags.Gsub));
        Positioning = new(font.TableMemory(SfntTags.Gpos));
        Definitions = new(font.TableMemory(SfntTags.Gdef));
        LegacyKerning = font.TableMemory(SfntTags.Kern);
    }

    /// <summary>Gets the glyph substitution table.</summary>
    internal OtLayoutTable Substitution { get; }

    /// <summary>Gets the glyph positioning table.</summary>
    internal OtLayoutTable Positioning { get; }

    /// <summary>Gets the glyph definitions.</summary>
    internal OtGdef Definitions { get; }

    /// <summary>Gets the legacy 'kern' table, or empty.</summary>
    internal ReadOnlyMemory<byte> LegacyKerning { get; }

    /// <summary>Gets the plan for a script, language and direction, making it on first use.</summary>
    /// <param name="script">The script.</param>
    /// <param name="language">The OpenType language tag, or zero.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <param name="vertical">Whether the run is written vertically.</param>
    /// <param name="features">The caller's feature settings, or <see langword="null"/>.</param>
    /// <returns>The plan.</returns>
    internal ShapePlan PlanFor(ShapingScript script, uint language, bool rightToLeft, bool vertical, IReadOnlyList<PdfFontFeature>? features)
    {
        var key = new PlanKey(script.Name, language, rightToLeft, vertical, Signature(features));
        return _plans.TryGetValue(key, out var plan)
            ? plan
            : _plans.GetOrAdd(key, PlanBuilder.Build(this, script, language, rightToLeft, vertical, features));
    }

    /// <summary>Writes the caller's feature settings as text, so plans made with different settings are kept apart.</summary>
    /// <param name="features">The settings, or <see langword="null"/>.</param>
    /// <returns>The text; empty when there are no settings.</returns>
    private static string Signature(IReadOnlyList<PdfFontFeature>? features)
    {
        if (features is not { Count: > 0 })
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var feature in features)
        {
            _ = text.Append(feature.Tag).Append('=').Append(feature.Value).Append(',');
        }

        return text.ToString();
    }
}
