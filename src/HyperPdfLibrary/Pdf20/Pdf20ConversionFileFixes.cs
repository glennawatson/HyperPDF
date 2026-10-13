// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Fixes the deprecated operating system entries: the DOS, Mac and Unix file names of a file specification (ISO 32000-2
/// 7.11.3), the Mac parameters of an embedded file (7.11.4.1), and the Win, Mac and Unix parameters of a launch action
/// (12.6.4.6). A name moves to /F when /F is missing, so the specification still names its file.
/// </summary>
internal static class Pdf20ConversionFileFixes
{
    /// <summary>Fixes one dictionary.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    internal static void Fix(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        if (dictionary.IsName(KnownName.S, KnownName.Launch))
        {
            FixLaunch(tally, dictionary, ref copy);
            return;
        }

        var hasSystemNames = dictionary.ContainsKey(tally.Names.Dos) || dictionary.ContainsKey(KnownName.Unix) || dictionary.Get(KnownName.Mac).Kind == PdfKind.String;
        if (hasSystemNames)
        {
            FixSpecification(tally, dictionary, ref copy);
        }
        else if (dictionary.Get(KnownName.Mac).Kind == PdfKind.Dictionary)
        {
            tally.FileNames += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Mac) ? 1 : 0;
        }
    }

    /// <summary>Moves a system file name to /F when /F is missing, then removes the system names.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The file specification.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixSpecification(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        if (!dictionary.ContainsKey(KnownName.F))
        {
            var moved = FirstString(dictionary, tally.Names.Dos);
            moved = moved.IsNull ? FirstString(dictionary, KnownName.Mac) : moved;
            moved = moved.IsNull ? FirstString(dictionary, KnownName.Unix) : moved;
            if (!moved.IsNull)
            {
                Pdf20ConversionEdit.Set(dictionary, ref copy, KnownName.F, moved);
            }
        }

        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, tally.Names.Dos);
        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Unix);
        if (dictionary.Get(KnownName.Mac).Kind == PdfKind.String)
        {
            _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Mac);
        }

        tally.FileNames++;
    }

    /// <summary>Reads an entry that is a string.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The string, or a null value when the entry is missing or not a string.</returns>
    private static PdfValue FirstString(PdfDictionary dictionary, PdfName key)
    {
        var value = dictionary.Get(key);
        return value.Kind == PdfKind.String ? value : default;
    }

    /// <summary>Removes a launch action's platform parameters, keeping the file it launches.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The launch action.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixLaunch(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        if (!dictionary.ContainsKey(KnownName.Win) && !dictionary.ContainsKey(KnownName.Mac) && !dictionary.ContainsKey(KnownName.Unix))
        {
            return;
        }

        if (!dictionary.ContainsKey(KnownName.F))
        {
            var file = dictionary.GetDictionary(KnownName.Win) is { } windows ? FirstString(windows, KnownName.F) : default;
            if (file.IsNull)
            {
                tally.LaunchWithoutFile++;
            }
            else
            {
                Pdf20ConversionEdit.Set(dictionary, ref copy, KnownName.F, file);
            }
        }

        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Win);
        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Mac);
        _ = Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Unix);
        tally.LaunchParameters++;
    }
}
