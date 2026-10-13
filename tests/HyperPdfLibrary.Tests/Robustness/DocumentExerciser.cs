// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Reads everything a viewer reads from a document and describes it as text, so two reads can be compared.</summary>
internal static class DocumentExerciser
{
    /// <summary>Reads a document: pages, outline, links, labels, attachments, annotations, layers and every stream.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A text description of what was read.</returns>
    internal static string Read(PdfDocument document)
    {
        var text = new StringBuilder();
        AppendPages(document, text);
        AppendNavigation(document, text);
        AppendLayers(document, text);
        AppendStreams(document, text);
        _ = text.Append(ReadMediaData(document, false));
        return text.ToString();
    }

    /// <summary>
    /// Reads the data of every multimedia, 3D and rich media annotation, the XFA packets and the document scripts, and describes them
    /// as text with a hash of each stream's decoded bytes. Nothing runs.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="includeRawBytes">Whether to also hash each stream's stored bytes, which only a save that keeps streams as they are preserves.</param>
    /// <returns>A text description of what was read.</returns>
    internal static string ReadMediaData(PdfDocument document, bool includeRawBytes)
    {
        var text = new StringBuilder();
        foreach (var annotation in PdfDocumentMedia.GetMultimediaAnnotations(document))
        {
            MediaDataDescription.Append(text, annotation, includeRawBytes);
        }

        MediaDataDescription.Append(text, PdfDocumentXfa.GetXfa(document));
        foreach (var script in PdfDocumentActions.GetDocumentScripts(document))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"js={script.Name}:{script.Script.Length}:{MediaDataDescription.Hash(Encoding.UTF8.GetBytes(script.Script))};");
        }

        return text.ToString();
    }

    /// <summary>Describes each page: geometry, label, links, annotations and widget scripts.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The description.</param>
    private static void AppendPages(PdfDocument document, StringBuilder text)
    {
        _ = text.Append(CultureInfo.InvariantCulture, $"pages={document.PageCount};");
        var scripts = new List<string>();
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            _ = text.Append(CultureInfo.InvariantCulture, $"p{i}={page.Width}x{page.Height}r{page.Rotation}L{PdfDocumentLabels.GetPageLabel(document, i)};");
            _ = text.Append(CultureInfo.InvariantCulture, $"links={PdfDocumentLinks.GetLinks(document, i).Count};ann={PdfDocumentContent.ScanAnnotations(document, i)};");
            scripts.Clear();
            PdfDocumentContent.GetWidgetScripts(document, i, scripts);
            _ = text.Append(CultureInfo.InvariantCulture, $"scripts={scripts.Count};");
        }
    }

    /// <summary>Describes the information, outline, attachments and signatures.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The description.</param>
    private static void AppendNavigation(PdfDocument document, StringBuilder text)
    {
        var info = PdfDocumentMetadata.GetInfo(document);
        _ = text.Append(CultureInfo.InvariantCulture, $"info={info.Title}|{info.Author}|{info.Version};");
        AppendOutline(PdfDocumentNavigation.GetOutline(document), text);
        foreach (var attachment in PdfDocumentAttachments.GetAttachments(document))
        {
            var size = attachment.Data?.DecodeToArray().Length ?? -1;
            _ = text.Append(CultureInfo.InvariantCulture, $"att={attachment.Name}:{size};");
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"sigs={PdfDocumentAttachments.GetSignatures(document).Count};js={PdfDocumentContent.GetJavaScriptActionCount(document)};");
    }

    /// <summary>Describes outline items depth first.</summary>
    /// <param name="items">The items.</param>
    /// <param name="text">The description.</param>
    private static void AppendOutline(IReadOnlyList<PdfOutlineItem> items, StringBuilder text)
    {
        foreach (var item in items)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"o={item.Title}:{item.Action.Value?.GetType().Name}:{item.IsOpen}(");
            AppendOutline(item.Children, text);
            _ = text.Append(')');
        }
    }

    /// <summary>Describes the layers.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The description.</param>
    private static void AppendLayers(PdfDocument document, StringBuilder text)
    {
        foreach (var layer in PdfDocumentLayers.GetOptionalContent(document).Layers)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"layer={layer.Id}:{layer.Name}:{layer.IsVisible};");
        }
    }

    /// <summary>Decodes every stream object and describes its length and content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The description.</param>
    private static void AppendStreams(PdfDocument document, StringBuilder text)
    {
        var objects = document.Objects;
        for (var number = 1; number < objects.Size; number++)
        {
            if (StoreReading.GetObject(objects, new(number, 0)).AsStream() is not { } stream)
            {
                continue;
            }

            var decoded = stream.DecodeToArray();
            var hash = default(HashCode);
            hash.AddBytes(decoded);
            _ = text.Append(CultureInfo.InvariantCulture, $"s{number}={decoded.Length}:{hash.ToHashCode()};");
        }
    }
}
