// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Finds the streams a page needs that are packed with a filter this library does not know, so part of the page is left out.</summary>
public static class PdfUnreadableStreams
{
    /// <summary>The levels of forms inside forms that are searched.</summary>
    private const int MaxFormLevels = 2;

    /// <summary>Determines whether a page's content, forms or fonts use a stream with an unknown filter.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="true"/> when such a stream is found; <see langword="false"/> for a page that does not exist.</returns>
    public static bool Exists(PdfDocument document, int pageIndex)
    {
        if ((uint)pageIndex >= (uint)document.PageCount)
        {
            return false;
        }

        var page = PdfDocumentPages.GetPage(document, pageIndex);
        return ContentIsUnreadable(page.Dictionary.Get(KnownName.Contents)) || ResourcesAreUnreadable(page.Resources, MaxFormLevels);
    }

    /// <summary>Determines whether a stream names a filter this library does not know.</summary>
    /// <param name="stream">The stream, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for an unknown filter.</returns>
    private static bool IsUnreadable(PdfStream? stream) => stream is not null && PdfStreamDecoder.HasUnknownFilter(stream.Dictionary.Get(KnownName.Filter));

    /// <summary>Checks a page's /Contents, which is one stream or an array of them.</summary>
    /// <param name="contents">The /Contents value.</param>
    /// <returns><see langword="true"/> when a content stream is unreadable.</returns>
    private static bool ContentIsUnreadable(PdfValue contents)
    {
        if (contents.AsArray() is not { } array)
        {
            return IsUnreadable(contents.AsStream());
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (IsUnreadable(array.Get(i).AsStream()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the forms and fonts of a resource dictionary.</summary>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    /// <param name="levels">How many more levels of forms to search.</param>
    /// <returns><see langword="true"/> when a form or font program is unreadable.</returns>
    private static bool ResourcesAreUnreadable(PdfDictionary? resources, int levels) =>
        resources is not null && (FormsAreUnreadable(resources.GetDictionary(KnownName.XObject), levels) || FontsAreUnreadable(resources.GetDictionary(KnownName.Font)));

    /// <summary>Checks the form XObjects of a dictionary, and the resources those forms use.</summary>
    /// <param name="xobjects">The /XObject dictionary, or <see langword="null"/>.</param>
    /// <param name="levels">How many more levels of forms to search.</param>
    /// <returns><see langword="true"/> when a form is unreadable.</returns>
    private static bool FormsAreUnreadable(PdfDictionary? xobjects, int levels)
    {
        for (var i = 0; xobjects is not null && i < xobjects.Count; i++)
        {
            if (xobjects.Get(xobjects.GetKeyAt(i)).AsStream() is { } form
                && form.Dictionary.GetName(KnownName.Subtype).Is(KnownName.Form)
                && (IsUnreadable(form) || (levels > 0 && ResourcesAreUnreadable(form.Dictionary.GetDictionary(KnownName.Resources), levels - 1))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the font programs of a /Font dictionary.</summary>
    /// <param name="fonts">The /Font dictionary, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a font program is unreadable.</returns>
    private static bool FontsAreUnreadable(PdfDictionary? fonts)
    {
        for (var i = 0; fonts is not null && i < fonts.Count; i++)
        {
            if (fonts.GetDictionary(fonts.GetKeyAt(i)) is { } font && FontIsUnreadable(font))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the font program of a font, or of its descendant fonts.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <returns><see langword="true"/> when a font program is unreadable.</returns>
    private static bool FontIsUnreadable(PdfDictionary font)
    {
        if (ProgramIsUnreadable(font.GetDictionary(KnownName.FontDescriptor)))
        {
            return true;
        }

        var descendants = font.GetArray(KnownName.DescendantFonts);
        for (var i = 0; descendants is not null && i < descendants.Count; i++)
        {
            if (ProgramIsUnreadable(descendants.GetDictionary(i)?.GetDictionary(KnownName.FontDescriptor)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the font file streams of a font descriptor.</summary>
    /// <param name="descriptor">The descriptor, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a font file is unreadable.</returns>
    private static bool ProgramIsUnreadable(PdfDictionary? descriptor) =>
        descriptor is not null
        && (IsUnreadable(descriptor.GetStream(KnownName.FontFile)) || IsUnreadable(descriptor.GetStream(KnownName.FontFile2)) || IsUnreadable(descriptor.GetStream(KnownName.FontFile3)));
}
