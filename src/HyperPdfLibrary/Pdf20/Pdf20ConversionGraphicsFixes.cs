// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Fixes the deprecated entries of graphics state parameter dictionaries (ISO 32000-2 8.4.5 Table 57): a blend mode array
/// becomes the first mode readers know, and /Compatible becomes /Normal (11.3.5); identity transfer functions are removed,
/// and ones that change colour are counted so the copy can be refused (10.5).
/// </summary>
internal static class Pdf20ConversionGraphicsFixes
{
    /// <summary>Fixes one dictionary.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    internal static void Fix(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        if (!IsGraphicsState(dictionary))
        {
            return;
        }

        FixBlendMode(tally, dictionary, ref copy);
        FixTransfer(tally, dictionary, ref copy);
    }

    /// <summary>Determines whether a dictionary is a graphics state parameter dictionary, which often carries no /Type.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsGraphicsState(PdfDictionary dictionary) =>
        dictionary.IsName(KnownName.Type, KnownName.ExtGState)
        || dictionary.ContainsKey(KnownName.BM)
        || ((dictionary.ContainsKey(KnownName.TR) || dictionary.ContainsKey(KnownName.TR2)) && !dictionary.ContainsKey(KnownName.S) && !dictionary.ContainsKey(KnownName.G));

    /// <summary>Replaces a blend mode array, or /Compatible, with a single known mode.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixBlendMode(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        var mode = dictionary.Get(KnownName.BM);
        if (mode.AsArray() is { } modes)
        {
            Pdf20ConversionEdit.Set(dictionary, ref copy, KnownName.BM, PdfValue.FromName(FirstKnown(modes)));
            tally.BlendModes++;
        }
        else if (mode.IsName(KnownName.Compatible))
        {
            Pdf20ConversionEdit.Set(dictionary, ref copy, KnownName.BM, PdfValue.FromName(KnownName.Normal));
            tally.BlendModes++;
        }
    }

    /// <summary>Finds the first blend mode in an array that readers know, as ISO 32000-2 11.3.5 reads the array.</summary>
    /// <param name="modes">The array.</param>
    /// <returns>The mode, or Normal when none is known.</returns>
    private static PdfName FirstKnown(PdfArray modes)
    {
        for (var i = 0; i < modes.Count; i++)
        {
            var name = modes.GetName(i);
            if (PdfBlendModeNames.IsStandard(name.ToKnownName()))
            {
                return name;
            }
        }

        return KnownName.Normal;
    }

    /// <summary>Removes identity transfer functions, and counts the ones that change colours.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixTransfer(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        var second = dictionary.Get(KnownName.TR2);
        var first = dictionary.Get(KnownName.TR);
        if (first.IsNull && second.IsNull)
        {
            return;
        }

        // TR2 takes precedence over TR when both are present (Table 57).
        if (!IsIdentity(second.IsNull ? first : second))
        {
            tally.ColourTransfers++;
            return;
        }

        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.TR);
        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.TR2);
        tally.TransferFunctions++;
    }

    /// <summary>Determines whether a transfer function value changes nothing: a name (Identity or Default), or an array of names.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it changes no colour.</returns>
    private static bool IsIdentity(PdfValue value)
    {
        if (value.Kind == PdfKind.Name)
        {
            return true;
        }

        if (value.AsArray() is not { } functions)
        {
            return false;
        }

        for (var i = 0; i < functions.Count; i++)
        {
            if (functions.Get(i).Kind != PdfKind.Name)
            {
                return false;
            }
        }

        return true;
    }
}
