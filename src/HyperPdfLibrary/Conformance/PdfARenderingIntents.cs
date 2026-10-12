// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks rendering intents: a named intent must be one of the four PDF defines (ISO 32000-1 8.6.5.8).</summary>
internal static class PdfARenderingIntents
{
    /// <summary>Checks a rendering intent value; values that are not names are not rendering intents and are skipped, as in veraPDF.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The value.</param>
    internal static void Check(PdfAColourState state, PdfValue value)
    {
        if (StoreReading.Resolve(state.Store, value).TryGetName(out var name))
        {
            Check(state, name);
        }
    }

    /// <summary>Checks a named rendering intent.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="name">The intent.</param>
    internal static void Check(PdfAColourState state, PdfName name)
    {
        var spelling = state.Store.Names.GetSpelling(name);
        var holds = spelling.SequenceEqual("RelativeColorimetric"u8) || spelling.SequenceEqual("AbsoluteColorimetric"u8)
            || spelling.SequenceEqual("Perceptual"u8) || spelling.SequenceEqual("Saturation"u8);
        state.Context.Require(holds, PdfARule.RenderingIntentName, null);
    }
}
