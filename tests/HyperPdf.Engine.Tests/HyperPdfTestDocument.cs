// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>A generated PDF opened with HyperPDF, whose annotations are edited natively; deleted on dispose.</summary>
internal sealed class HyperPdfTestDocument : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="HyperPdfTestDocument"/> class.</summary>
    /// <param name="pageCount">The page count.</param>
    internal HyperPdfTestDocument(int pageCount)
        : this(TestPdf.Create(pageCount))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTestDocument"/> class.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <exception cref="InvalidOperationException">The document has no text layer writer.</exception>
    internal HyperPdfTestDocument(byte[] bytes)
        : this(bytes, new HyperPdfEngine())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTestDocument"/> class.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="engine">The engine that opens the document.</param>
    /// <exception cref="InvalidOperationException">The document has no text layer writer.</exception>
    internal HyperPdfTestDocument(byte[] bytes, HyperPdfEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Pdf = bytes;
        FilePath = Path.Combine(Path.GetTempPath(), $"hyperpdf-annotations-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(FilePath, bytes);
        Document = (HyperPdfDocument)engine.Open(FilePath, null);
        Editor = HyperPdfAnnotationStateAccess.GetAnnotations(Document);
    }

    /// <summary>Gets the input PDF bytes.</summary>
    internal byte[] Pdf { get; }

    /// <summary>Gets the file path.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the document.</summary>
    internal HyperPdfDocument Document { get; }

    /// <summary>Gets the native editor.</summary>
    internal HyperPdfAnnotations Editor { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Document.Dispose();
        File.Delete(FilePath);
    }

    /// <summary>Saves an editor's document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The saved bytes.</returns>
    internal static byte[] Save(IAnnotationEditor editor)
    {
        using var stream = new MemoryStream();
        _ = editor.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Saves an editor's document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The saved bytes.</returns>
    internal static byte[] Save(HyperPdfAnnotations editor)
    {
        using var stream = new MemoryStream();
        _ = HyperPdfAnnotationSaving.Save(editor, stream);
        return stream.ToArray();
    }

    /// <summary>Reads a page's annotations from a file.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    internal static List<PageAnnotation> Read(IAnnotationEditor editor, int page)
    {
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(page, annotations);
        return annotations;
    }

    /// <summary>Reads a page's annotations from a file.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    internal static List<PageAnnotation> Read(HyperPdfAnnotations editor, int page)
    {
        var annotations = new List<PageAnnotation>();
        HyperPdfAnnotationReading.GetAnnotations(editor, page, annotations);
        return annotations;
    }

    /// <summary>Reopens saved bytes and reads the page's annotations.</summary>
    /// <param name="bytes">The saved PDF.</param>
    /// <param name="page">The zero-based page.</param>
    /// <returns>The annotations after reopening.</returns>
    internal static List<PageAnnotation> ReadSaved(byte[] bytes, int page)
    {
        using var reopened = new HyperPdfTestDocument(bytes);
        return Read(reopened.Editor, page);
    }

    /// <summary>Reads the annotation dictionaries of a page of saved bytes with the managed library.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="page">The page.</param>
    /// <returns>The dictionaries.</returns>
    internal static List<PdfDictionary> Dictionaries(byte[] bytes, int page)
    {
        var document = PdfDocumentReader.Open(bytes, null);
        var result = new List<PdfDictionary>();
        if (PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, page)) is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                result.Add(array.GetDictionary(i)!);
            }
        }

        return result;
    }

    /// <summary>Gets a name's spelling in a dictionary's document.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name, or empty.</returns>
    internal static string Name(PdfDictionary dictionary, KnownName key) => dictionary.Owner!.Names.GetString(dictionary.GetName(key));
}
