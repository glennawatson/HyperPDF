// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks JPEG 2000 image data against the PDF/A rules (ISO 19005-2 6.2.8.3, ISO 19005-4 6.2.7.3).</summary>
internal static class PdfAJpxChecks
{
    /// <summary>The enumerated colour space PDF/A forbids: CIEJab.</summary>
    private const long CieJab = 19;

    /// <summary>The highest METH value PDF/A allows.</summary>
    private const long MaxMethod = 3;

    /// <summary>The highest bit depth PDF/A allows.</summary>
    private const long MaxBitDepth = 38;

    /// <summary>The channel count of a grey image.</summary>
    private const long GrayChannels = 1;

    /// <summary>The channel count of a three-channel image.</summary>
    private const long ThreeChannels = 3;

    /// <summary>The channel count of a four-channel image.</summary>
    private const long FourChannels = 4;

    /// <summary>Determines whether a stream's filters include <c>/JPXDecode</c>, reading the filter list as veraPDF does.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image dictionary.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    internal static bool IsJpx(PdfAColourState state, PdfDictionary image)
    {
        var filter = image.Get(KnownName.Filter);
        if (filter.TryGetName(out var name))
        {
            return name.Is(KnownName.JPXDecode);
        }

        var filters = filter.AsArray();
        var found = false;
        for (var i = 0; filters is not null && i < filters.Count; i++)
        {
            // A filter that is not a name makes veraPDF read no filters at all.
            if (!StoreReading.Resolve(state.Store, filters.Get(i)).TryGetName(out var each))
            {
                return false;
            }

            found |= each.Is(KnownName.JPXDecode);
        }

        return found;
    }

    /// <summary>Checks the header of a JPEG 2000 image.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="header">The header.</param>
    /// <param name="hasColourSpace">Whether the image dictionary has a <c>/ColorSpace</c>, which overrides the data's colour specifications.</param>
    internal static void Check(PdfAColourState state, PdfAJpxHeader header, bool hasColourSpace)
    {
        var context = state.Context;
        context.Require(header.Channels is GrayChannels or ThreeChannels or FourChannels, PdfARule.JpxChannels, null);
        context.Require(hasColourSpace || header.ColourSpecifications == 1 || header.ApproximateSpecifications == 1, PdfARule.JpxColorSpecifications, null);
        context.Require(hasColourSpace || header.Method is >= 1 and <= MaxMethod, PdfARule.JpxColorMethod, null);
        context.Require(hasColourSpace || header.EnumeratedSpace != CieJab, PdfARule.JpxEnumeratedColorSpace, null);
        context.Require(!header.HasBitsPerComponentBox && header.BitDepth is >= 1 and <= MaxBitDepth, PdfARule.JpxBitDepth, null);
    }
}
