// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document content streams.</summary>
public static class PdfDocumentContent
{
    /// <summary>The most /Parent steps followed to find a field's inherited actions.</summary>
    internal const int MaxFieldDepth = 32;

    /// <summary>The levels of forms inside forms that are searched for unreadable images.</summary>
    private const int MaxFormLevels = 2;

    /// <summary>The array elements in one name tree pair: the name and its value.</summary>
    private const int NamePairLength = 2;

    /// <summary>The longest script decoded on the stack; longer ones use a pooled buffer.</summary>
    private const int StackScriptChars = 256;

    /// <summary>Finds the annotations on a page that need a media player or a 3D viewer, loading the page first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The content found; none for a page that does not exist.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfAnnotationContent> ScanAnnotationsAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var load = (uint)pageIndex < (uint)document.PageCount ? PdfPrefetcher.PrefetchAsync(
document.Objects,
PdfDocumentPages.GetPage(document, pageIndex).Dictionary,
PdfPrefetchKind.Page,
cancellationToken) : ValueTask.CompletedTask;
        return load.IsCompletedSuccessfully ? PdfDocumentContent.AnnotationsReady(document, pageIndex, cancellationToken) : PdfDocumentContent.AnnotationsAfterAsync(
document,
load,
pageIndex,
cancellationToken);
    }

    /// <summary>Gets a value indicating whether the document has an AcroForm.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when the document has an AcroForm dictionary.</returns>
    public static bool HasAcroForm(PdfDocument document) => document.Catalog.GetDictionary(KnownName.AcroForm) is not null;

    /// <summary>Gets a value indicating whether the AcroForm carries an XFA form.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when the AcroForm contains XFA data.</returns>
    public static bool HasXfaForm(PdfDocument document) => document.Catalog.GetDictionary(KnownName.AcroForm) is { } form && !form.GetRaw(KnownName.XFA).IsNull;

    /// <summary>Counts the document-level JavaScript actions in the /Names /JavaScript name tree.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The number of named scripts.</returns>
    public static int GetJavaScriptActionCount(PdfDocument document)
    {
        if (document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.JavaScript) is not { } tree)
        {
            return 0;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(tree, entries);
        return entries.Count;
    }

    /// <summary>Determines whether the /Names /JavaScript name tree holds at least one script, without listing them all.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when <see cref="GetJavaScriptActionCount"/> would be above zero.</returns>
    public static bool HasJavaScriptActions(PdfDocument document)
    {
        if (document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.JavaScript) is not { } tree)
        {
            return false;
        }

        // A root that holds a pair itself is answered at once; a tree of kids is walked.
        return tree.GetArray(KnownName.Names) is { Count: >= NamePairLength } || PdfDocumentContent.GetJavaScriptActionCount(document) > 0;
    }

    /// <summary>Finds the annotations on a page that need a media player or a 3D viewer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content found; none for a page that does not exist.</returns>
    public static PdfAnnotationContent ScanAnnotations(PdfDocument document, int pageIndex)
    {
        var content = PdfAnnotationContent.None;
        if ((uint)pageIndex >= (uint)document.PageCount || PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annots)
        {
            return content;
        }

        for (var i = 0; i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is { } annotation)
            {
                content |= PdfDocumentContent.Classify(annotation.GetName(KnownName.Subtype));
            }
        }

        return content;
    }

    /// <summary>
    /// Determines whether a page draws an image packed with a filter this library does not know, so it can only show a
    /// placeholder. It looks at the images in the page's resources and in the forms those draw, a few levels down.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="true"/> when such an image is found; <see langword="false"/> for a page that does not exist.</returns>
    public static bool HasUnreadableImages(PdfDocument document, int pageIndex) =>
        (uint)pageIndex < (uint)document.PageCount && PdfDocumentContent.HasUnreadableImages(PdfDocumentPages.GetPage(document, pageIndex).Resources, MaxFormLevels);

    /// <summary>Appends the JavaScript of the keystroke, format, validate and calculate actions of a page's form fields.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving each non-empty script.</param>
    public static void GetWidgetScripts(PdfDocument document, int pageIndex, List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if ((uint)pageIndex >= (uint)document.PageCount || PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annots)
        {
            return;
        }

        for (var i = 0; i < annots.Count; i++)
        {
            PdfDocumentContent.AddWidgetScripts(annots.GetDictionary(i), output);
        }
    }

    /// <summary>Hands each field script of a page to a visitor as characters, stopping when the visitor says so.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">Cancels annotation walking and script decoding.</param>
    /// <returns><see langword="true"/> when the visitor stopped the walk.</returns>
    internal static bool VisitWidgetScripts<TVisitor>(PdfDocument document, int pageIndex, ref TVisitor visitor, CancellationToken cancellationToken)
        where TVisitor : struct, IWidgetScriptVisitor
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((uint)pageIndex >= (uint)document.PageCount || PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annots)
        {
            return false;
        }

        for (var i = 0; i < annots.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (annots.GetDictionary(i) is { } annotation
                && annotation.IsName(KnownName.Subtype, KnownName.Widget)
                && PdfDocumentContent.GetAdditionalActions(annotation) is { } actions
                && PdfDocumentContent.VisitActionScripts(actions, ref visitor, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Visits the keystroke, format, validate and calculate scripts of one /AA dictionary.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="actions">The /AA dictionary.</param>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">Cancels script decoding.</param>
    /// <returns><see langword="true"/> when the visitor stopped the walk.</returns>
    private static bool VisitActionScripts<TVisitor>(PdfDictionary actions, ref TVisitor visitor, CancellationToken cancellationToken)
        where TVisitor : struct, IWidgetScriptVisitor =>
        PdfDocumentContent.VisitScript(actions, KnownName.K, ref visitor, cancellationToken)
        || PdfDocumentContent.VisitScript(actions, KnownName.F, ref visitor, cancellationToken)
        || PdfDocumentContent.VisitScript(actions, KnownName.V, ref visitor, cancellationToken)
        || PdfDocumentContent.VisitScript(actions, KnownName.C, ref visitor, cancellationToken);

    /// <summary>Visits the JavaScript of one additional action, when it has some.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="actions">The /AA dictionary.</param>
    /// <param name="key">The action's key.</param>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">Cancels script decoding.</param>
    /// <returns><see langword="true"/> when the visitor stopped the walk.</returns>
    private static bool VisitScript<TVisitor>(PdfDictionary actions, KnownName key, ref TVisitor visitor, CancellationToken cancellationToken)
        where TVisitor : struct, IWidgetScriptVisitor
    {
        var script = actions.GetDictionary(key)?.Get(KnownName.JS) ?? default;
        return script.Kind switch
        {
            PdfKind.String => PdfDocumentContent.VisitBytes(script.AsStringBytes(), ref visitor, cancellationToken),
            PdfKind.Stream => PdfDocumentContent.VisitStream(script.AsStream()!, ref visitor, cancellationToken),
            _ => false,
        };
    }

    /// <summary>Visits a script stream while its decoded bytes are held in rented storage.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="stream">The script stream.</param>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    /// <returns>Whether the visitor stopped the walk.</returns>
    private static bool VisitStream<TVisitor>(PdfStream stream, ref TVisitor visitor, CancellationToken cancellationToken)
        where TVisitor : struct, IWidgetScriptVisitor
    {
        var output = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref output, cancellationToken);
            return PdfDocumentContent.VisitBytes(output.WrittenSpan, ref visitor, cancellationToken);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes a script's text string into a buffer and visits it.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="bytes">The text string's bytes.</param>
    /// <param name="visitor">The visitor.</param>
    /// <param name="cancellationToken">Cancels before text decoding.</param>
    /// <returns><see langword="true"/> when the visitor stopped the walk.</returns>
    private static bool VisitBytes<TVisitor>(ReadOnlySpan<byte> bytes, ref TVisitor visitor, CancellationToken cancellationToken)
        where TVisitor : struct, IWidgetScriptVisitor
    {
        cancellationToken.ThrowIfCancellationRequested();
        var length = PdfText.GetDecodedLength(bytes);
        if (length == 0)
        {
            return false;
        }

        if (length <= StackScriptChars)
        {
            Span<char> stack = stackalloc char[StackScriptChars];
            var written = PdfText.Decode(bytes, stack);
            return visitor.Visit(stack[..written]);
        }

        var rented = ArrayPool<char>.Shared.Rent(length);
        try
        {
            var written = PdfText.Decode(bytes, rented);
            return visitor.Visit(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Scans a page's annotations once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed content, or a faulted task.</returns>
    private static ValueTask<PdfAnnotationContent> AnnotationsReady(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(PdfDocumentContent.ScanAnnotations(document, pageIndex));
        }
        catch (Exception ex) when (PdfDocumentAsyncTasks.IsTaskFault(ex))
        {
            return ValueTask.FromException<PdfAnnotationContent>(ex);
        }
    }

    /// <summary>Waits for a load, then scans a page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="load">The load.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The content found.</returns>
    private static async ValueTask<PdfAnnotationContent> AnnotationsAfterAsync(PdfDocument document, ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return PdfDocumentContent.ScanAnnotations(document, pageIndex);
    }

    /// <summary>Looks through a resource dictionary's images, and the forms it draws, for one with an unknown filter.</summary>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    /// <param name="levels">How many more levels of forms to search.</param>
    /// <returns><see langword="true"/> when an image with an unknown filter is found.</returns>
    private static bool HasUnreadableImages(PdfDictionary? resources, int levels)
    {
        if (resources?.GetDictionary(KnownName.XObject) is not { } xobjects)
        {
            return false;
        }

        for (var i = 0; i < xobjects.Count; i++)
        {
            if (xobjects.Get(xobjects.GetKeyAt(i)).AsStream() is not { } stream)
            {
                continue;
            }

            var subtype = stream.Dictionary.GetName(KnownName.Subtype);
            var unreadable = subtype.Is(KnownName.Image)
                ? PdfStreamDecoder.HasUnknownFilter(stream.Dictionary.Get(KnownName.Filter))
                : levels > 0 && subtype.Is(KnownName.Form) && PdfDocumentContent.HasUnreadableImages(stream.Dictionary.GetDictionary(KnownName.Resources), levels - 1);
            if (unreadable)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Says what a annotation subtype needs.</summary>
    /// <param name="subtype">The annotation's /Subtype.</param>
    /// <returns>The content it stands for.</returns>
    private static PdfAnnotationContent Classify(PdfName subtype) => subtype.ToKnownName() switch
    {
        KnownName.Sound or KnownName.Movie or KnownName.Screen or KnownName.RichMedia => PdfAnnotationContent.Multimedia,
        KnownName.ThreeD => PdfAnnotationContent.ThreeD,
        _ => PdfAnnotationContent.None,
    };

    /// <summary>Appends the scripts of one annotation when it is a form widget.</summary>
    /// <param name="annotation">The annotation, or <see langword="null"/>.</param>
    /// <param name="output">The list receiving each non-empty script.</param>
    private static void AddWidgetScripts(PdfDictionary? annotation, List<string> output)
    {
        if (annotation is null || !annotation.IsName(KnownName.Subtype, KnownName.Widget) || PdfDocumentContent.GetAdditionalActions(annotation) is not { } actions)
        {
            return;
        }

        PdfDocumentContent.AddScript(actions, KnownName.K, output);
        PdfDocumentContent.AddScript(actions, KnownName.F, output);
        PdfDocumentContent.AddScript(actions, KnownName.V, output);
        PdfDocumentContent.AddScript(actions, KnownName.C, output);
    }

    /// <summary>Finds a field's /AA dictionary, inherited from its parents.</summary>
    /// <param name="field">The field or widget.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    private static PdfDictionary? GetAdditionalActions(PdfDictionary field)
    {
        var node = field;
        for (var depth = 0; node is not null && depth < PdfDocumentContent.MaxFieldDepth; depth++)
        {
            if (node.GetDictionary(KnownName.AA) is { } actions)
            {
                return actions;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return null;
    }

    /// <summary>Appends the JavaScript of one additional action, when it has some.</summary>
    /// <param name="actions">The /AA dictionary.</param>
    /// <param name="key">The action's key.</param>
    /// <param name="output">The list receiving the script.</param>
    private static void AddScript(PdfDictionary actions, KnownName key, List<string> output)
    {
        var script = actions.GetDictionary(key)?.Get(KnownName.JS) ?? default;
        var text = script.Kind switch
        {
            PdfKind.String => PdfText.Decode(script.AsStringBytes()),
            PdfKind.Stream => PdfText.Decode(script.AsStream()!.DecodeToArray()),
            _ => string.Empty,
        };
        if (text.Length > 0)
        {
            output.Add(text);
        }
    }
}
