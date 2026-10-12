// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks inline images: their entries, rendering intent and colour space.</summary>
internal static class PdfAInlineImageChecks
{
    /// <summary>The bytes added around inline image entries to parse them as a dictionary.</summary>
    private const int DelimiterWidth = 2;

    /// <summary>Checks an inline image.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="entries">The image's entries between <c>BI</c> and <c>ID</c>.</param>
    /// <param name="frame">Where it is painted.</param>
    internal static void Visit(PdfAColourState state, ReadOnlySpan<byte> entries, in PdfAContentFrame frame)
    {
        if (Parse(state, entries) is not { } image)
        {
            return;
        }

        var isMask = image.Get(KnownName.IM).AsBoolean(false);
        if (!frame.Paint.ProcessColour && !isMask)
        {
            return;
        }

        PdfAImageChecks.CheckEntries(state, image, Entry(image, KnownName.I, KnownName.Interpolate), Entry(image, KnownName.BPC, KnownName.BitsPerComponent), false);
        PdfARenderingIntents.Check(state, image.GetRaw(KnownName.Intent));
        if (Entry(image, KnownName.IM, KnownName.ImageMask).AsBoolean(false))
        {
            PdfAColourChecks.Visit(state, frame.Paint.Fill);
            return;
        }

        PdfAColourChecks.Visit(state, ColourSpace(state, Entry(image, KnownName.CS, KnownName.ColorSpace), frame.Scope));
    }

    /// <summary>Reads an inline image entry by its abbreviation, or by its full name when the abbreviation is absent.</summary>
    /// <param name="image">The entries.</param>
    /// <param name="abbreviation">The abbreviated key.</param>
    /// <param name="full">The full key.</param>
    /// <returns>The value.</returns>
    private static PdfValue Entry(PdfDictionary image, KnownName abbreviation, KnownName full) =>
        image.ContainsKey(abbreviation) ? image.Get(abbreviation) : image.Get(full);

    /// <summary>Reads an inline image's colour space: abbreviated device names are expanded and Default spaces apply.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The colour space entry.</param>
    /// <param name="scope">The resources of the content.</param>
    /// <returns>The space, or null.</returns>
    private static PdfAColourSpace? ColourSpace(PdfAColourState state, PdfValue value, in PdfAColourResourceScope scope)
    {
        if (value.TryGetName(out var name))
        {
            name = Expand(name);
            value = PdfValue.FromName(name);
            var defaults = scope.Own ?? scope.Page;
            var defaultName = PdfAColourSpaceParsing.DefaultFor(state, defaults, name);
            if (!defaultName.IsNone && PdfAColourSpaceParsing.FromResources(state, defaults!, defaultName, false) is { } replaced)
            {
                return replaced;
            }
        }

        var own = scope.Own is { } ownResources ? PdfAColourSpaceParsing.Parse(state, value, ownResources, false) : null;
        return own ?? PdfAColourSpaceParsing.Parse(state, value, scope.Page, false);
    }

    /// <summary>Expands an abbreviated colour space name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The full name.</returns>
    private static PdfName Expand(PdfName name) => name.ToKnownName() switch
    {
        KnownName.G => KnownName.DeviceGray,
        KnownName.RGB => KnownName.DeviceRGB,
        KnownName.CMYK => KnownName.DeviceCMYK,
        KnownName.I => KnownName.Indexed,
        _ => name,
    };

    /// <summary>Parses inline image entries as a dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The dictionary, or null.</returns>
    private static PdfDictionary? Parse(PdfAColourState state, ReadOnlySpan<byte> entries)
    {
        var text = new byte[entries.Length + DelimiterWidth + DelimiterWidth];
        text[0] = (byte)'<';
        text[1] = (byte)'<';
        entries.CopyTo(text.AsSpan(DelimiterWidth));
        text[^DelimiterWidth] = (byte)'>';
        text[^1] = (byte)'>';
        var parser = new PdfParser(text, 0, state.Store, state.Store.Names);
        return parser.ParseValue().AsDictionary();
    }
}
