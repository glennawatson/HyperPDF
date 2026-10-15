// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Gives a font a <c>/ToUnicode</c> map for the characters it shows when it has none (ISO 14289-1:2014, 7.21.7; ISO 32000-1:2008,
/// 9.10.3), so a screen reader gets text for every character. The text comes from what the library knows of the font:
/// its encoding and glyph names. A text a font already maps is kept. A font whose characters the library cannot name
/// is left alone, and the preflight reports it, as the repair never guesses a character.
/// </summary>
internal static class PdfUaToUnicodeRepair
{
    /// <summary>The PDF/A level whose glyph rules ask for Unicode text, so the same lookup decides which fonts need a map.</summary>
    private const PdfALevel FontLevel = PdfALevel.PdfA2A;

    /// <summary>Writes a map for every shown font that lacks text for a character it shows.</summary>
    /// <param name="document">The document, read after any earlier repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        var context = new PdfAValidationContext(document, FontLevel, cancellationToken);
        var walk = PdfATextWalk.Run(context);
        Dictionary<PdfDictionary, PdfObjectId>? objects = null;
        foreach (var use in walk.Used)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LacksText(context, use))
            {
                continue;
            }

            objects ??= FindFontObjects(document.Objects, walk.GetDistinctResources());
            Write(document.Objects, transaction, use, objects, cancellationToken);
        }
    }

    /// <summary>Determines whether a font shows a code that has no Unicode text.</summary>
    /// <param name="context">The validation that reads the glyph facts.</param>
    /// <param name="use">The font and the codes it shows.</param>
    /// <returns><see langword="true"/> when some shown code has no text.</returns>
    private static bool LacksText(PdfAValidationContext context, PdfAFontUse use)
    {
        if (use.Glyphs.Count == 0)
        {
            return false;
        }

        var font = PdfAGlyphFonts.Create(context, use);
        foreach (var code in use.Glyphs.Keys)
        {
            if (PdfAGlyphUnicode.Text(font, code, context.Part) is null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes a font's new map.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="use">The font.</param>
    /// <param name="objects">The object of each font dictionary the resources name.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    private static void Write(PdfObjectStore store, PdfEditTransaction transaction, PdfAFontUse use, Dictionary<PdfDictionary, PdfObjectId> objects, CancellationToken cancellationToken)
    {
        var raw = use.Dictionary.GetRaw(KnownName.ToUnicode);
        var existing = PdfAToUnicode.Read(store, use.Dictionary.Get(KnownName.ToUnicode), cancellationToken);

        // A font written directly in a resource dictionary has no object to replace.
        if (PdfAConversionToUnicodeRepair.Build(use.Dictionary, existing) is not { } bytes || (!raw.IsReference && !objects.ContainsKey(use.Dictionary)))
        {
            return;
        }

        var stream = PdfValue.FromStream(new(new PdfDictionary(store, 0), bytes));
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), stream);
            return;
        }

        var copy = use.Dictionary.Clone();
        copy.Set(KnownName.ToUnicode, PdfValue.FromReference(transaction.Add(stream)));
        transaction.Replace(objects[use.Dictionary], PdfValue.FromDictionary(copy));
    }

    /// <summary>Finds the object of each font dictionary that a resource dictionary names by reference.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="resources">The resource dictionaries the walk read.</param>
    /// <returns>The object of each font dictionary, by the dictionary instance.</returns>
    private static Dictionary<PdfDictionary, PdfObjectId> FindFontObjects(PdfObjectStore store, HashSet<PdfDictionary> resources)
    {
        var objects = new Dictionary<PdfDictionary, PdfObjectId>(ReferenceEqualityComparer.Instance);
        foreach (var resource in resources)
        {
            if (resource.GetDictionary(KnownName.Font) is not { } fonts)
            {
                continue;
            }

            for (var i = 0; i < fonts.Count; i++)
            {
                var raw = fonts.GetValueAt(i);
                if (raw.IsReference && StoreReading.Resolve(store, raw).AsDictionary() is { } font)
                {
                    objects[font] = raw.AsReference();
                }
            }
        }

        return objects;
    }
}
