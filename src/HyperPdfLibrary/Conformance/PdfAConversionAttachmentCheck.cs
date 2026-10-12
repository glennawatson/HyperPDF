// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks that each attached file is itself a PDF/A file, which PDF/A-2 (parts 1 and 2) and plain PDF/A-4 (parts 1, 2 and 4)
/// require (ISO 19005-2 §6.8; ISO 19005-4:2020 §6.9). An attachment passes when it claims an allowed part and passes the
/// checks this library makes for that level.
/// </summary>
internal static class PdfAConversionAttachmentCheck
{
    /// <summary>Refuses the conversion for each attachment that is not an acceptable PDF/A file.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="files">The file specifications.</param>
    internal static void RequirePdfA(PdfAConversionSession session, PdfAConversionAttachmentList files)
    {
        if (files.Direct > 0)
        {
            Refuse(session, "an attached file");
            return;
        }

        foreach (var number in files.References)
        {
            if (StoreReading.GetObject(session.Store, new(number, 0)).AsDictionary() is not { } specification)
            {
                continue;
            }

            var name = specification.GetText(KnownName.UF) ?? specification.GetText(KnownName.F) ?? "an attached file";
            if (specification.GetDictionary(KnownName.EF) is not { } embedded || !AllArePdfA(session, embedded))
            {
                Refuse(session, name);
            }
        }
    }

    /// <summary>Determines whether every stream an /EF dictionary holds is an acceptable PDF/A file.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="embedded">The /EF dictionary.</param>
    /// <returns><see langword="true"/> when they all are.</returns>
    private static bool AllArePdfA(PdfAConversionSession session, PdfDictionary embedded)
    {
        for (var i = 0; i < embedded.Count; i++)
        {
            if (StoreReading.Resolve(session.Store, embedded.GetValueAt(i)).AsStream() is not { } stream || !IsPdfA(session, stream))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether an embedded stream is a PDF that claims an allowed PDF/A part and passes its checks.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="stream">The embedded file stream.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPdfA(PdfAConversionSession session, PdfStream stream)
    {
        try
        {
            using var attachment = PdfDocumentReader.Open(stream.DecodeToArray(session.CancellationToken), null);
            if (PdfAValidator.ClaimedLevel(attachment) is not { } level)
            {
                return false;
            }

            var part = PdfALevels.Part(level);
            var allowed = part is PdfAClaim.Part1 or PdfAClaim.Part2 || (part == PdfAClaim.Part4 && session.Part == PdfAClaim.Part4);
            return allowed && PdfAValidator.Validate(attachment, level, session.CancellationToken).Passed;
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException or NotSupportedException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    /// <summary>Records an attachment that stops the conversion.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="name">The attachment's name.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Refuse(PdfAConversionSession session, string name) =>
        session.Refuse(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The attachment \"{name}\" is not a PDF/A file, and {PdfALevels.Label(session.Level)} allows only PDF/A attachments. Choose PDF/A-3 to keep it."),
            PdfARule.EmbeddedFilePdfA12,
            PdfARule.EmbeddedFilePdfA124);
}
