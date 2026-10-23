// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes the link annotations of a page: clickable areas that open an address or jump to a bookmark or a page.</summary>
internal static class LinkAnnotations
{
    /// <summary>The annotation flag that makes an annotation print.</summary>
    private const int PrintFlag = 4;

    /// <summary>The entries of a link annotation dictionary.</summary>
    private const int AnnotationEntries = 9;

    /// <summary>The characters up to this value, as UTF-16, are written in an address as they are.</summary>
    private const int LastAsciiCharacter = 127;

    /// <summary>The most bytes a character takes in UTF-8.</summary>
    private const int MaxUtf8Bytes = 4;

    /// <summary>The entries of an action dictionary.</summary>
    private const int ActionEntries = 2;

    /// <summary>Adds an annotation for every link drawn on the page.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="state">The document being written.</param>
    internal static void AddAll(ref PdfPageCanvas canvas, RenderState state)
    {
        foreach (var link in state.PendingLinks)
        {
            Add(ref canvas, state, link);
        }
    }

    /// <summary>Gets the name a link to a page jumps to.</summary>
    /// <param name="page">The page's number, counted from 1.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetPageName(int page) => string.Create(CultureInfo.InvariantCulture, $"__page{page}");

    /// <summary>Writes an address as ASCII, with the characters outside ASCII written as percent escapes of their UTF-8 bytes.</summary>
    /// <param name="uri">The address.</param>
    /// <returns>The ASCII bytes.</returns>
    internal static byte[] ToAscii(string uri)
    {
        var text = new StringBuilder(uri.Length);
        Span<byte> bytes = stackalloc byte[MaxUtf8Bytes];
        foreach (var rune in uri.EnumerateRunes())
        {
            if (rune.Value <= LastAsciiCharacter)
            {
                _ = text.Append((char)rune.Value);
                continue;
            }

            var written = rune.EncodeToUtf8(bytes);
            foreach (var value in bytes[..written])
            {
                _ = text.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    /// <summary>Adds the annotation of one link.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="state">The document being written.</param>
    /// <param name="link">The link.</param>
    private static void Add(ref PdfPageCanvas canvas, RenderState state, in PendingLink link)
    {
        var creator = state.Creator;
        var builder = creator.Builder;
        var owner = builder.Owner;
        var names = builder.Names;
        var box = link.Box;
        var annotation = new PdfDictionary(owner, AnnotationEntries);
        annotation.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        annotation.Set(names.Intern("Subtype"), PdfValue.FromName(KnownName.Link));
        annotation.Set(KnownName.Rect, PdfValue.FromArray(PdfArray.FromNumbers(owner, [box.X, box.Y, box.Right, box.Top])));
        annotation.Set(KnownName.Border, PdfValue.FromArray(PdfArray.FromNumbers(owner, [0F, 0F, 0F])));
        annotation.Set(names.Intern("F"), PdfValue.FromInteger(PrintFlag));
        SetTarget(annotation, link.Target, state, names, owner);
        annotation.Set(KnownName.Contents, PdfValue.FromString(PdfText.Encode(Describe(link))));
        var id = builder.Reserve();
        if (link.Node is { } node && creator.Tags is { } tags)
        {
            var key = tags.AddAnnotation(state.GetElement(node), canvas.Page, id);
            annotation.Set(KnownName.StructParent, PdfValue.FromInteger(key));
        }

        builder.Set(id, PdfValue.FromDictionary(annotation));
        canvas.AddAnnotation(id);
    }

    /// <summary>Describes where a link goes, for the annotation's contents.</summary>
    /// <param name="link">The link.</param>
    /// <returns>The description: the link's own, or where it goes.</returns>
    private static string Describe(in PendingLink link) => link.Node?.Options.AlternativeText ?? link.Target.Kind switch
    {
        PdfLinkKind.Uri => link.Target.Value,
        PdfLinkKind.Bookmark => $"Go to {link.Target.Value}",
        _ => $"Go to page {link.Target.Page}",
    };

    /// <summary>Sets what a link does when it is clicked.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="target">Where the link goes.</param>
    /// <param name="state">The document being written.</param>
    /// <param name="names">The document's names.</param>
    /// <param name="owner">The object store the dictionaries belong to.</param>
    private static void SetTarget(PdfDictionary annotation, PdfLinkTarget target, RenderState state, PdfNameTable names, PdfObjectStore? owner)
    {
        switch (target.Kind)
        {
            case PdfLinkKind.Uri:
            {
                var action = new PdfDictionary(owner, ActionEntries);
                action.Set(names.Intern("S"), PdfValue.FromName(names.Intern("URI")));
                action.Set(names.Intern("URI"), PdfValue.FromString(ToAscii(target.Value)));
                annotation.Set(names.Intern("A"), PdfValue.FromDictionary(action));
                break;
            }

            case PdfLinkKind.Bookmark:
            {
                _ = state.LinkedBookmarks.Add(target.Value);
                annotation.Set(KnownName.Dest, PdfValue.FromString(Encoding.UTF8.GetBytes(target.Value)));
                break;
            }

            default:
            {
                _ = state.LinkedPages.Add(target.Page);
                annotation.Set(KnownName.Dest, PdfValue.FromString(Encoding.UTF8.GetBytes(GetPageName(target.Page))));
                break;
            }
        }
    }
}
