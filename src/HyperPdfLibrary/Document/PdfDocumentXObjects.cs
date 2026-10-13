// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.XObjects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads image alternates and form XObject references. Reading never opens an external file.</summary>
public static class PdfDocumentXObjects
{
    /// <summary>The most <c>/Alternates</c> entries examined for one image.</summary>
    private const int MaxAlternates = 256;

    /// <summary>The number of byte strings in a reference's <c>/ID</c> array.</summary>
    private const int IdLength = 2;

    /// <summary>Gets the alternate versions of an image (its <c>/Alternates</c> array).</summary>
    /// <param name="document">The document.</param>
    /// <param name="image">The image XObject.</param>
    /// <returns>
    /// The alternates in array order. An entry that is not an image, repeats an earlier one, or names the image itself is skipped, and an
    /// alternate's own <c>/Alternates</c> are not followed, so a loop cannot occur.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    public static PdfImageAlternate[] GetImageAlternates(PdfDocument document, PdfStream image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Dictionary.GetArray(KnownName.Alternates) is not { } list)
        {
            return [];
        }

        var seen = new HashSet<PdfStream>(ReferenceEqualityComparer.Instance) { image };
        var alternates = new List<PdfImageAlternate>();
        for (var i = 0; i < Math.Min(list.Count, PdfDocumentXObjects.MaxAlternates); i++)
        {
            if (list.GetDictionary(i) is { } entry && PdfDocumentXObjects.ReadAlternate(entry) is { } alternate && seen.Add(alternate.Image))
            {
                alternates.Add(alternate);
            }
        }

        return [.. alternates];
    }

    /// <summary>
    /// Chooses the image to draw. Screen use always draws the main image. Print use draws the alternate marked
    /// <c>/DefaultForPrinting</c> when there is one, and the main image otherwise.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="image">The main image XObject.</param>
    /// <param name="use">Where the image is going.</param>
    /// <returns>The image to draw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    public static PdfStream SelectImage(PdfDocument document, PdfStream image, PdfImageUse use)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (use != PdfImageUse.Print)
        {
            return image;
        }

        foreach (var alternate in PdfDocumentXObjects.GetImageAlternates(document, image))
        {
            if (alternate.DefaultForPrinting)
            {
                return alternate.Image;
            }
        }

        return image;
    }

    /// <summary>Gets the external page a form XObject stands in for (its <c>/Ref</c> dictionary). Nothing is opened.</summary>
    /// <param name="document">The document.</param>
    /// <param name="form">The form XObject.</param>
    /// <returns>The reference, or <see langword="null"/> when the form has no readable <c>/Ref</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="form"/> is null.</exception>
    public static PdfReferenceXObject? GetReference(PdfDocument document, PdfStream form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.Dictionary.GetDictionary(KnownName.Ref) is not { } reference)
        {
            return null;
        }

        var fileName = PdfDocumentFileSpecs.ReadFileSpec(document, reference.Get(KnownName.F), out var isUrl) ?? string.Empty;
        var page = reference.Value("Page");
        var ids = reference.Array("ID");
        return new(
            fileName,
            isUrl,
            page.IsNumber && page.AsInteger() >= 0 ? page.AsInt32() : null,
            page.Kind == PdfKind.String ? PdfText.Decode(page.AsStringBytes()) : null,
            PdfDocumentXObjects.ReadId(ids, 0),
            PdfDocumentXObjects.ReadId(ids, 1),
            form);
    }

    /// <summary>Reads one alternate image dictionary.</summary>
    /// <param name="entry">The dictionary.</param>
    /// <returns>The alternate, or null when <c>/Image</c> is not an image XObject.</returns>
    private static PdfImageAlternate? ReadAlternate(PdfDictionary entry) =>
        entry.GetStream(KnownName.Image) is { } image && image.Dictionary.IsName(KnownName.Subtype, KnownName.Image)
            ? new(image, entry.Flag("DefaultForPrinting", false), entry.Dict("OC"))
            : null;

    /// <summary>Reads one string of a reference's <c>/ID</c> array.</summary>
    /// <param name="ids">The array, or null.</param>
    /// <param name="index">The position, 0 or 1.</param>
    /// <returns>The bytes, or null when the array is not a pair of strings.</returns>
    private static byte[]? ReadId(PdfArray? ids, int index) =>
        ids is { } array && array.Count == PdfDocumentXObjects.IdLength && array.Get(index).Kind == PdfKind.String ? array.Get(index).AsStringBytes().ToArray() : null;
}
