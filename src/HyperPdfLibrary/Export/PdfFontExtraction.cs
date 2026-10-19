// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Takes the embedded font programs a page uses out of a document as files, as the document stores them: TrueType as
/// <c>.ttf</c>, OpenType as <c>.otf</c>, compact (CFF) as <c>.cff</c> and Type 1 as <c>.t1</c>. The fonts of forms the page
/// draws are included. A font used by several pages, or listed under several names, is written once; fonts that are not
/// embedded have no program to take.
/// </summary>
public static class PdfFontExtraction
{
    /// <summary>The deepest nesting of forms that is followed, so a form that uses itself cannot loop.</summary>
    private const int MaxFormDepth = 8;

    /// <summary>Gets the embedded fonts a page uses.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The font files, in the order the page's resources list them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    public static PdfExtractedFile[] GetFonts(PdfDocument document, int pageIndex)
    {
        var files = new List<PdfExtractedFile>();
        Collect(document, pageIndex, [], files);
        return [.. files];
    }

    /// <summary>Gets the embedded fonts of every page in order, each font once even when several pages use it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The font files, page by page.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async IAsyncEnumerable<PdfExtractedFile> GetFontsAsync(PdfDocument document, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HashSet<PdfObjectId> seen = [];
        var files = new List<PdfExtractedFile>();
        for (var page = 0; page < document.PageCount; page++)
        {
            await PdfDocumentPages.PrefetchPageAsync(document, page, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            files.Clear();
            Collect(document, page, seen, files);
            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    /// <summary>Collects the fonts of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="seen">The font program streams already taken.</param>
    /// <param name="files">Receives the files.</param>
    private static void Collect(PdfDocument document, int pageIndex, HashSet<PdfObjectId> seen, List<PdfExtractedFile> files)
    {
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        CollectFrom(page.Resources, pageIndex, seen, files, 0);
    }

    /// <summary>Collects the fonts of one set of resources, and of the forms in them.</summary>
    /// <param name="resources">The resources.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="seen">The font program streams already taken.</param>
    /// <param name="files">Receives the files.</param>
    /// <param name="depth">How many forms deep the resources are.</param>
    private static void CollectFrom(PdfDictionary? resources, int pageIndex, HashSet<PdfObjectId> seen, List<PdfExtractedFile> files, int depth)
    {
        if (resources is null)
        {
            return;
        }

        if (resources.GetDictionary(KnownName.Font) is { } fonts)
        {
            for (var i = 0; i < fonts.Count; i++)
            {
                AddFont(fonts.GetDictionary(fonts.GetKeyAt(i)), pageIndex, seen, files);
            }
        }

        if (depth >= MaxFormDepth || resources.GetDictionary(KnownName.XObject) is not { } xobjects)
        {
            return;
        }

        for (var i = 0; i < xobjects.Count; i++)
        {
            if (xobjects.GetStream(xobjects.GetKeyAt(i)) is { } form && form.Dictionary.IsName(KnownName.Subtype, KnownName.Form) && seen.Add(form.Id))
            {
                CollectFrom(form.Dictionary.GetDictionary(KnownName.Resources), pageIndex, seen, files, depth + 1);
            }
        }
    }

    /// <summary>Takes the program of a font, if it has one that is not taken yet.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="seen">The streams already taken.</param>
    /// <param name="files">Receives the file.</param>
    private static void AddFont(PdfDictionary? font, int pageIndex, HashSet<PdfObjectId> seen, List<PdfExtractedFile> files)
    {
        if (font is null)
        {
            return;
        }

        var descriptor = FontDescriptor.Read(
            font.GetDictionary(KnownName.FontDescriptor) ?? font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0)?.GetDictionary(KnownName.FontDescriptor));
        if (descriptor.FontFile is not { } program || (program.Id.IsValid && !seen.Add(program.Id)))
        {
            return;
        }

        var name = PdfNames.BaseFontOf(font);
        var file = string.Create(CultureInfo.InvariantCulture, $"{(name.Length > 0 ? name : "font")}{Extension(descriptor.FileKind, program)}");
        files.Add(new(pageIndex, Unique(file, files), PdfExtractedFileKind.Font, program.DecodeToArray()));
    }

    /// <summary>Gets the extension of a font program.</summary>
    /// <param name="kind">The kind of font file.</param>
    /// <param name="program">The program's stream, whose subtype tells OpenType from CFF.</param>
    /// <returns>The extension with its dot.</returns>
    private static string Extension(FontFileKind kind, PdfStream program) => kind switch
    {
        FontFileKind.TrueType => ".ttf",
        FontFileKind.Type1 => ".t1",
        _ when program.Dictionary.IsName(KnownName.Subtype, KnownName.OpenType) => ".otf",
        _ => ".cff",
    };

    /// <summary>Makes a file name that no file taken so far has, by numbering a repeat.</summary>
    /// <param name="name">The wanted name.</param>
    /// <param name="files">The files taken so far.</param>
    /// <returns>The name, or the name with a number before its extension.</returns>
    private static string Unique(string name, List<PdfExtractedFile> files)
    {
        var candidate = name;
        for (var number = 2; files.Exists(file => file.Name == candidate); number++)
        {
            candidate = string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileNameWithoutExtension(name)}-{number}{Path.GetExtension(name)}");
        }

        return candidate;
    }
}
