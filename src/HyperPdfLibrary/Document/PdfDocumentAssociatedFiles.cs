// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads files associated with the document.</summary>
public static class PdfDocumentAssociatedFiles
{
    /// <summary>The most structure elements visited when searching for associated files.</summary>
    private const int MaxStructureElements = 100_000;

    /// <summary>The relationship of a file specification that names none.</summary>
    private const string UnspecifiedRelationship = "Unspecified";

    /// <summary>Gets the files associated with the document itself (the catalog's <c>/AF</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The files.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfAssociatedFile> GetAssociatedFiles(PdfDocument document) =>
        PdfDocumentAssociatedFiles.GetAssociatedFiles(
        document,
        document.Catalog,
        PdfAssociatedOwner.Document,
        null);

    /// <summary>Gets the files associated with a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static IReadOnlyList<PdfAssociatedFile> GetAssociatedFiles(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return PdfDocumentAssociatedFiles.GetAssociatedFiles(document, page.Dictionary, PdfAssociatedOwner.Page, page.Index);
    }

    /// <summary>Gets the files listed in the <c>/AF</c> entry of any object: an annotation, structure element, XObject or other dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="owner">The dictionary holding the <c>/AF</c> entry (an XObject's stream dictionary counts).</param>
    /// <param name="kind">The kind of object, recorded on each result.</param>
    /// <param name="pageIndex">The page the object is on, or null.</param>
    /// <returns>The files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public static IReadOnlyList<PdfAssociatedFile> GetAssociatedFiles(PdfDocument document, PdfDictionary owner, PdfAssociatedOwner kind, int? pageIndex)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var list = owner.Value("AF");
        var files = new List<PdfAssociatedFile>();
        if (list.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                PdfDocumentAssociatedFiles.AddAssociatedFile(document, files, array.Get(i), kind, pageIndex);
            }
        }
        else
        {
            PdfDocumentAssociatedFiles.AddAssociatedFile(document, files, list, kind, pageIndex);
        }

        return files;
    }

    /// <summary>Gets every associated file in the document: the catalog's, then each page's with its annotations and XObjects, then the structure tree's.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The files, each file specification once, read once until the next edit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfAssociatedFile> GetAllAssociatedFiles(PdfDocument document) =>
        PdfReadSnapshots.Read(document, PdfReadKind.AssociatedFiles, PdfDocumentAssociatedFiles.ReadAllAssociatedFiles);

    /// <summary>Reads every associated file in the document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The files, each file specification once.</returns>
    private static IReadOnlyList<PdfAssociatedFile> ReadAllAssociatedFiles(PdfDocument document)
    {
        var files = new List<PdfAssociatedFile>(PdfDocumentAssociatedFiles.GetAssociatedFiles(document));
        foreach (var page in PdfDocumentPages.GetPageSet(document).Pages)
        {
            files.AddRange(PdfDocumentAssociatedFiles.GetAssociatedFiles(document, page));
            PdfDocumentAssociatedFiles.AddAnnotationFiles(document, files, page);
            PdfDocumentAssociatedFiles.AddXObjectFiles(document, files, page);
        }

        PdfDocumentAssociatedFiles.AddStructureFiles(document, files);
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        _ = files.RemoveAll(file => !seen.Add(file.Specification));
        return files.AsReadOnly();
    }

    /// <summary>Pushes the dictionary children of a structure element's <c>/K</c> value.</summary>
    /// <param name="pending">The elements still to visit.</param>
    /// <param name="kids">The <c>/K</c> value: a dictionary, an array, or an integer marked-content id.</param>
    private static void PushChildren(Stack<PdfDictionary> pending, PdfValue kids)
    {
        if (kids.AsArray() is { } array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                PdfDocumentAssociatedFiles.PushChild(pending, array.Get(i));
            }
        }
        else
        {
            PdfDocumentAssociatedFiles.PushChild(pending, kids);
        }
    }

    /// <summary>Pushes a child when it is a structure element (marked-content and object references are not).</summary>
    /// <param name="pending">The elements still to visit.</param>
    /// <param name="child">The child value.</param>
    private static void PushChild(Stack<PdfDictionary> pending, PdfValue child)
    {
        if (child.AsDictionary() is { } dictionary && !dictionary.IsName(KnownName.Type, KnownName.MCR) && !dictionary.IsName(KnownName.Type, KnownName.OBJR))
        {
            pending.Push(dictionary);
        }
    }

    /// <summary>Adds a file specification as an associated file when it is one.</summary>
    /// <param name="document">The document.</param>
    /// <param name="files">The result list.</param>
    /// <param name="value">The <c>/AF</c> element.</param>
    /// <param name="kind">The owner kind.</param>
    /// <param name="pageIndex">The page, or null.</param>
    private static void AddAssociatedFile(PdfDocument document, List<PdfAssociatedFile> files, PdfValue value, PdfAssociatedOwner kind, int? pageIndex)
    {
        if (value.AsDictionary() is not { } spec)
        {
            return;
        }

        var embedded = spec.GetDictionary(KnownName.EF);
        var data = embedded?.GetStream(KnownName.UF) ?? embedded?.GetStream(KnownName.F);
        files.Add(new(
        kind,
        pageIndex,
        spec.NameText("AFRelationship") ?? PdfDocumentAssociatedFiles.UnspecifiedRelationship,
        PdfDocumentFileSpecs.ReadFileSpec(
        document,
        value) ?? string.Empty,
        spec.GetText(KnownName.Desc),
        data?.Dictionary.NameText("Subtype"),
        data,
        spec));
    }

    /// <summary>Adds the files of a page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="files">The result list.</param>
    /// <param name="page">The page.</param>
    private static void AddAnnotationFiles(PdfDocument document, List<PdfAssociatedFile> files, PdfPage page)
    {
        var annots = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annots is not null && i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is { } annot)
            {
                files.AddRange(PdfDocumentAssociatedFiles.GetAssociatedFiles(document, annot, PdfAssociatedOwner.Annotation, page.Index));
            }
        }
    }

    /// <summary>Adds the files of the XObjects in a page's resources.</summary>
    /// <param name="document">The document.</param>
    /// <param name="files">The result list.</param>
    /// <param name="page">The page.</param>
    private static void AddXObjectFiles(PdfDocument document, List<PdfAssociatedFile> files, PdfPage page)
    {
        var xobjects = page.Resources?.GetDictionary(KnownName.XObject);
        for (var i = 0; xobjects is not null && i < xobjects.Count; i++)
        {
            if (xobjects.GetValueAt(i) is var raw && StoreReading.Resolve(document.Objects, raw).AsDictionary() is { } xobject)
            {
                files.AddRange(PdfDocumentAssociatedFiles.GetAssociatedFiles(document, xobject, PdfAssociatedOwner.XObject, page.Index));
            }
        }
    }

    /// <summary>Adds the files of the structure elements, walking <c>/StructTreeRoot</c> within fixed limits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="files">The result list.</param>
    private static void AddStructureFiles(PdfDocument document, List<PdfAssociatedFile> files)
    {
        if (document.Catalog.GetDictionary(KnownName.StructTreeRoot) is not { } root)
        {
            return;
        }

        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfDictionary>();
        pending.Push(root);
        while (pending.Count > 0 && visited.Count < PdfDocumentAssociatedFiles.MaxStructureElements)
        {
            var element = pending.Pop();
            if (!visited.Add(element))
            {
                continue;
            }

            if (!ReferenceEquals(element, root))
            {
                files.AddRange(PdfDocumentAssociatedFiles.GetAssociatedFiles(document, element, PdfAssociatedOwner.StructureElement, null));
            }

            PdfDocumentAssociatedFiles.PushChildren(pending, element.Get(KnownName.K));
        }
    }
}
