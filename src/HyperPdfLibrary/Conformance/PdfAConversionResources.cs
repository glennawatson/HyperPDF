// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Removes the device-specific and external entries PDF/A forbids from graphics resources: transfer functions, halftone
/// origin and phase, halftone names, image interpolation, alternate images, OPI and PostScript fragments, reference
/// XObject links and unknown rendering intents (ISO 19005-1 §6.2.4 to §6.2.8; ISO 19005-2 and -3 §6.2.5 to §6.2.9;
/// ISO 19005-4:2020 §6.2.5 to §6.2.9). None changes what the page shows on screen. PostScript XObjects, unknown blend modes
/// and unsupported halftones stop the conversion.
/// </summary>
internal static class PdfAConversionResources
{
    /// <summary>The two-bit image component depth.</summary>
    private const int PairBits = 2;

    /// <summary>The four-bit image component depth.</summary>
    private const int QuarterBits = 4;

    /// <summary>The byte-sized image component depth.</summary>
    private const int ByteBits = 8;

    /// <summary>The image component depth available after PDF/A-1.</summary>
    private const int WideBits = 16;

    /// <summary>The array entries inspected between cancellation checks.</summary>
    private const int CancelInterval = 1024;

    /// <summary>Checks an image component depth for the active PDF/A part.</summary>
    /// <param name="depth">The bits per component.</param>
    /// <param name="wideAllowed">Whether the sixteen-bit form is permitted.</param>
    /// <returns>Whether the part allows this component depth.</returns>
    internal static bool IsAllowedBitDepth(int depth, bool wideAllowed) =>
        depth is 1 or PairBits or QuarterBits or ByteBits || (depth == WideBits && wideAllowed);

    /// <summary>Recognises a blend-mode name or array, including legacy Compatible.</summary>
    /// <param name="value">The resolved blend-mode entry.</param>
    /// <param name="cancellationToken">Cancels an array scan.</param>
    /// <returns>Whether every mode has a standard spelling.</returns>
    internal static bool IsKnownBlend(PdfValue value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.AsArray() is { } modes)
        {
            for (var i = 0; i < modes.Count; i++)
            {
                if (i % CancelInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var name = modes.GetName(i).ToKnownName();
                if (name != KnownName.Compatible && !PdfBlendModeNames.IsStandard(name))
                {
                    return false;
                }
            }

            return true;
        }

        var single = value.AsName().ToKnownName();
        return single == KnownName.Compatible || PdfBlendModeNames.IsStandard(single);
    }

    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var tally = new PdfAConversionResourceTally();
        var changed = PdfAConversionRewriter.Rewrite(session, tally, static (s, t, dictionary) => Fix(s, dictionary, t));
        tally.Report(session, changed);
    }

    /// <summary>Fixes one dictionary.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="tally">The counts.</param>
    /// <returns>A changed copy, or <see langword="null"/>.</returns>
    private static PdfDictionary? Fix(PdfAConversionSession session, PdfDictionary dictionary, PdfAConversionResourceTally tally)
    {
        PdfDictionary? copy = null;
        DropDeviceEntries(session, dictionary, ref copy);
        if (dictionary.ContainsKey(session.Store.Names.Intern("HalftoneType"u8)))
        {
            FixHalftone(session, dictionary, tally, ref copy);
        }

        switch (dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Image:
                {
                    FixImage(session, dictionary, tally, ref copy);
                    break;
                }

            case KnownName.Form or KnownName.PS:
                {
                    FixForm(session, dictionary, tally, ref copy);
                    break;
                }

            default:
                {
                    break;
                }
        }

        if (dictionary.ContainsKey(KnownName.BM) && !IsKnownBlend(dictionary.Get(KnownName.BM), session.CancellationToken))
        {
            tally.UnknownBlendModes++;
        }

        return copy;
    }

    /// <summary>Removes transfer functions, halftone origin and phase, OPI links and unknown rendering intents.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy being made.</param>
    private static void DropDeviceEntries(PdfAConversionSession session, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        var names = session.Store.Names;
        Drop(dictionary, KnownName.TR, ref copy);
        if (dictionary.ContainsKey(KnownName.TR2) && dictionary.GetName(KnownName.TR2) != names.Intern("Default"u8))
        {
            Drop(dictionary, KnownName.TR2, ref copy);
        }

        Drop(dictionary, names.Intern("HTO"u8), ref copy);
        Drop(dictionary, names.Intern("HTP"u8), ref copy);
        Drop(dictionary, KnownName.OPI, ref copy);
        if (dictionary.ContainsKey(KnownName.RI) && !PdfAConversionSpaceColours.IsKnownIntent(names.GetString(dictionary.GetName(KnownName.RI))))
        {
            Drop(dictionary, KnownName.RI, ref copy);
        }
    }

    /// <summary>Removes an entry when it is there.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="copy">The copy being made.</param>
    private static void Drop(PdfDictionary dictionary, PdfName key, ref PdfDictionary? copy)
    {
        if (!dictionary.ContainsKey(key))
        {
            return;
        }

        copy ??= dictionary.Clone();
        _ = copy.Remove(key);
    }

    /// <summary>Fixes an image: no interpolation, alternates or unknown intent; refuses an unsupported bit depth.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="image">The image dictionary.</param>
    /// <param name="tally">The counts.</param>
    /// <param name="copy">The copy being made.</param>
    private static void FixImage(PdfAConversionSession session, PdfDictionary image, PdfAConversionResourceTally tally, ref PdfDictionary? copy)
    {
        if (image.Get(KnownName.Interpolate).AsBoolean())
        {
            Drop(image, KnownName.Interpolate, ref copy);
        }

        Drop(image, KnownName.Alternates, ref copy);
        if (image.ContainsKey(KnownName.Intent) && !PdfAConversionSpaceColours.IsKnownIntent(session.Store.Names.GetString(image.GetName(KnownName.Intent))))
        {
            Drop(image, KnownName.Intent, ref copy);
        }

        if (!image.ContainsKey(KnownName.BitsPerComponent))
        {
            return;
        }

        // One rule allows 1, 2, 4 and 8 bits; the other adds 16. The level applies one of them.
        var depth = image.GetInt32(KnownName.BitsPerComponent);
        var allowed = IsAllowedBitDepth(depth, !session.Applies(PdfARule.ImageBitsPerComponent));
        tally.BadBitDepths += allowed ? 0 : 1;
    }

    /// <summary>Fixes a form XObject: no PostScript fragment or reference link; refuses a PostScript XObject.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="form">The form dictionary.</param>
    /// <param name="tally">The counts.</param>
    /// <param name="copy">The copy being made.</param>
    private static void FixForm(PdfAConversionSession session, PdfDictionary form, PdfAConversionResourceTally tally, ref PdfDictionary? copy)
    {
        var names = session.Store.Names;
        if (form.IsName(KnownName.Subtype, KnownName.PS) || form.GetName(names.Intern("Subtype2"u8)) == names.Intern("PS"u8))
        {
            tally.PostScript++;
            return;
        }

        Drop(form, KnownName.PS, ref copy);
        Drop(form, KnownName.Ref, ref copy);
    }

    /// <summary>Fixes a halftone: no name; refuses types other than 1 and 5.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="halftone">The halftone dictionary.</param>
    /// <param name="tally">The counts.</param>
    /// <param name="copy">The copy being made.</param>
    private static void FixHalftone(PdfAConversionSession session, PdfDictionary halftone, PdfAConversionResourceTally tally, ref PdfDictionary? copy)
    {
        var names = session.Store.Names;
        Drop(halftone, names.Intern("HalftoneName"u8), ref copy);
        var type = halftone.GetInt32(names.Intern("HalftoneType"u8));
        tally.BadHalftones += type is 1 or PdfAConversionResourceTally.ThresholdHalftone ? 0 : 1;
    }
}
