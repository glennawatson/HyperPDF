// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Records the device colour spaces an object names: an image's or shading's /ColorSpace, a group's /CS, the entries
/// of a resource /ColorSpace dictionary, and the alternate or base space of Separation, DeviceN, Indexed and Pattern
/// arrays.
/// </summary>
internal static class PdfAConversionSpaceColours
{
    /// <summary>The position of a Separation or DeviceN array's alternate space.</summary>
    private const int AlternateIndex = 2;

    /// <summary>Records the device spaces a value and its direct contents name.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="facts">The facts.</param>
    /// <param name="depth">The nesting depth.</param>
    internal static void Mark(PdfObjectStore store, PdfValue value, PdfAConversionFacts facts, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            MarkDictionary(store, dictionary, facts, depth);
        }
        else if (value.AsArray() is { } array)
        {
            MarkArray(array, facts);
            for (var i = 0; i < array.Count; i++)
            {
                Mark(store, array.GetRaw(i), facts, depth + 1);
            }
        }
    }

    /// <summary>Records a device space name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="defaults">The default spaces in force.</param>
    /// <param name="facts">The facts.</param>
    internal static void MarkName(KnownName name, PdfAConversionDefaultSpaces defaults, PdfAConversionFacts facts)
    {
        switch (name)
        {
            case KnownName.DeviceRGB or KnownName.RGB:
                {
                    facts.UsesRgb |= !defaults.Rgb;
                    break;
                }

            case KnownName.DeviceCMYK or KnownName.CMYK:
                {
                    facts.UsesCmyk |= !defaults.Cmyk;
                    break;
                }

            case KnownName.DeviceGray or KnownName.G:
                {
                    facts.UsesGray |= !defaults.Gray;
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Determines whether a rendering intent is one the PDF specification defines.</summary>
    /// <param name="intent">The intent's name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsKnownIntent(string intent) => intent is "Perceptual" or "RelativeColorimetric" or "Saturation" or "AbsoluteColorimetric";

    /// <summary>Records the device spaces of a dictionary's colour space entries, then walks its values.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="facts">The facts.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void MarkDictionary(PdfObjectStore store, PdfDictionary dictionary, PdfAConversionFacts facts, int depth)
    {
        var space = dictionary.GetRaw(KnownName.ColorSpace);
        if (space.Kind == PdfKind.Name)
        {
            MarkName(space.AsName().ToKnownName(), default, facts);
        }
        else if (StoreReading.Resolve(store, space).AsDictionary() is { } named)
        {
            MarkResourceSpaces(store, named, facts);
        }

        MarkName(dictionary.GetRaw(KnownName.CS).AsName().ToKnownName(), default, facts);
        for (var i = 0; i < dictionary.Count; i++)
        {
            Mark(store, dictionary.GetValueAt(i), facts, depth + 1);
        }
    }

    /// <summary>Records the device spaces a resource /ColorSpace dictionary names, other than its default spaces.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="spaces">The dictionary.</param>
    /// <param name="facts">The facts.</param>
    private static void MarkResourceSpaces(PdfObjectStore store, PdfDictionary spaces, PdfAConversionFacts facts)
    {
        for (var i = 0; i < spaces.Count; i++)
        {
            if (store.Names.GetString(spaces.GetKeyAt(i)).StartsWith("Default", StringComparison.Ordinal))
            {
                continue;
            }

            var value = StoreReading.Resolve(store, spaces.GetValueAt(i));
            if (value.Kind == PdfKind.Name)
            {
                MarkName(value.AsName().ToKnownName(), default, facts);
            }
        }
    }

    /// <summary>Records the alternate or base device space of a colour space array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="facts">The facts.</param>
    private static void MarkArray(PdfArray array, PdfAConversionFacts facts)
    {
        var index = array.GetRaw(0).AsName().ToKnownName() switch
        {
            KnownName.Separation or KnownName.DeviceN => AlternateIndex,
            KnownName.Indexed or KnownName.Pattern => 1,
            _ => -1,
        };
        if (index > 0)
        {
            MarkName(array.GetRaw(index).AsName().ToKnownName(), default, facts);
        }
    }
}
