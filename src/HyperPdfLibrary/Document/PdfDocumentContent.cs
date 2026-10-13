// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document content streams.</summary>
public static class PdfDocumentContent
{
    /// <summary>The most /Parent steps followed to find a field's inherited actions.</summary>
    internal const int MaxFieldDepth = 32;

    /// <summary>The array elements in one name tree pair: the name and its value.</summary>
    private const int NamePairLength = 2;

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
