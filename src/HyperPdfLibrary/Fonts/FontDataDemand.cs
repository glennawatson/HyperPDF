// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Fonts;

/// <summary>Finds fonts selected by content operators without loading unused resource entries.</summary>
internal static class FontDataDemand
{
    /// <summary>The most nested streams inspected for one page.</summary>
    private const int MaxStreams = 4096;

    /// <summary>Collects the fonts a requested page can draw.</summary>
    /// <param name = "page">The requested page.</param>
    /// <param name = "cancellationToken">Cancels content inspection.</param>
    /// <returns>The selected font dictionaries.</returns>
    /// <exception cref = "InvalidDataException">The content graph exceeds the traversal limit.</exception>
    internal static HashSet<PdfDictionary> Collect(PdfPage page, CancellationToken cancellationToken)
    {
        var fonts = new HashSet<PdfDictionary>();
        var queue = new Queue<FontDataContent>();
        var scope = new FontResourceScope(page.Resources, null);
        var content = new PooledBuffer(0);
        try
        {
            ContentExecution.DecodeContents(page, ref content, cancellationToken);
            Scan(content.WrittenSpan, page.Dictionary.Owner!.Names, scope, fonts, queue, cancellationToken);
        }
        finally
        {
            content.Dispose();
        }

        AddAnnotations(page, fonts, queue);
        var visited = new HashSet<FontDataContent>();
        while (queue.TryDequeue(out var next))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(next))
            {
                continue;
            }

            if (visited.Count > MaxStreams)
            {
                throw new InvalidDataException("The page has too many nested font resources.");
            }

            var bytes = next.Stream.DecodeToArray(cancellationToken);
            Scan(bytes, page.Dictionary.Owner.Names, next.Resources, fonts, queue, cancellationToken);
        }

        return fonts;
    }

    /// <summary>Scans operators and queues only referenced resources.</summary>
    /// <param name = "content">The decoded content.</param>
    /// <param name = "names">The document's names.</param>
    /// <param name = "scope">The resources visible to the stream.</param>
    /// <param name = "fonts">Receives selected fonts.</param>
    /// <param name = "queue">Receives referenced content streams.</param>
    /// <param name = "cancellationToken">Cancels inspection.</param>
    private static void Scan(ReadOnlySpan<byte> content, PdfNameTable names, FontResourceScope scope, HashSet<PdfDictionary> fonts, Queue<FontDataContent> queue, CancellationToken cancellationToken)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, names, operands);
        while (reader.Next(out var operation))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = reader.Operand(0).Name;
            switch (operation)
            {
                case ContentOperator.SetFont:
                    {
                        AddFont(scope.Find(KnownName.Font, name).AsDictionary(), scope, fonts, queue);
                        break;
                    }

                case ContentOperator.PaintXObject:
                    {
                        AddStream(scope.Find(KnownName.XObject, name).AsStream(), scope, queue);
                        break;
                    }

                case ContentOperator.SetFillColorN or ContentOperator.SetStrokeColorN:
                    {
                        var pattern = reader.Operand(reader.OperandCount - 1).Name;
                        AddStream(scope.Find(KnownName.Pattern, pattern).AsStream(), scope, queue);
                        break;
                    }

                case ContentOperator.SetGraphicsState:
                    {
                        AddGraphicsState(scope.Find(KnownName.ExtGState, name).AsDictionary(), scope, fonts, queue);
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }
    }

    /// <summary>Adds a selected font and the content of its Type 3 glyphs.</summary>
    /// <param name = "font">The selected font.</param>
    /// <param name = "scope">The inherited resources.</param>
    /// <param name = "fonts">Receives selected fonts.</param>
    /// <param name = "queue">Receives glyph streams.</param>
    private static void AddFont(PdfDictionary? font, FontResourceScope scope, HashSet<PdfDictionary> fonts, Queue<FontDataContent> queue)
    {
        if (font is null || !fonts.Add(font) || font.GetDictionary(KnownName.CharProcs) is not { } glyphs)
        {
            return;
        }

        var resources = new FontResourceScope(font.GetDictionary(KnownName.Resources), scope);
        for (var index = 0; index < glyphs.Count; index++)
        {
            AddStream(glyphs.Get(glyphs.GetKeyAt(index)).AsStream(), resources, queue);
        }
    }

    /// <summary>Queues a referenced form or pattern with its visible resources.</summary>
    /// <param name = "stream">The referenced stream.</param>
    /// <param name = "scope">The inherited resources.</param>
    /// <param name = "queue">Receives the stream.</param>
    private static void AddStream(PdfStream? stream, FontResourceScope scope, Queue<FontDataContent> queue)
    {
        if (stream is not null && !stream.Dictionary.IsName(KnownName.Subtype, KnownName.Image) && scope.Depth < PdfLimits.MaxNesting)
        {
            queue.Enqueue(new(stream, new(stream.Dictionary.GetDictionary(KnownName.Resources), scope)));
        }
    }

    /// <summary>Collects fonts and soft-mask content selected by a graphics state.</summary>
    /// <param name = "state">The selected state.</param>
    /// <param name = "scope">The visible resources.</param>
    /// <param name = "fonts">Receives selected fonts.</param>
    /// <param name = "queue">Receives mask streams.</param>
    private static void AddGraphicsState(PdfDictionary? state, FontResourceScope scope, HashSet<PdfDictionary> fonts, Queue<FontDataContent> queue)
    {
        AddFont(state?.GetArray(KnownName.Font)?.GetDictionary(0), scope, fonts, queue);
        AddStream(state?.GetDictionary(KnownName.SMask)?.GetStream(KnownName.G), scope, queue);
    }

    /// <summary>Collects normal appearances and the font of generated text appearances.</summary>
    /// <param name = "page">The requested page.</param>
    /// <param name = "fonts">Receives selected fonts.</param>
    /// <param name = "queue">Receives appearance streams.</param>
    private static void AddAnnotations(PdfPage page, HashSet<PdfDictionary> fonts, Queue<FontDataContent> queue)
    {
        if (page.Dictionary.GetArray(KnownName.Annots) is not { } annotations)
        {
            return;
        }

        var store = page.Dictionary.Owner!;
        var scope = new FontResourceScope(null, null);
        for (var index = 0; index < annotations.Count; index++)
        {
            if (annotations.GetDictionary(index) is not { } annotation)
            {
                continue;
            }

            if (PageRecorder.GetAppearance(annotation, store.Names) is { } appearance)
            {
                AddStream(appearance, scope, queue);
            }
            else if (annotation.IsName(KnownName.Subtype, KnownName.Widget) || annotation.IsName(KnownName.Subtype, KnownName.FreeText))
            {
                AddAppearanceFont(page, annotation, fonts, queue);
            }
        }
    }

    /// <summary>Finds the same default font used when an annotation appearance is generated.</summary>
    /// <param name = "page">The requested page.</param>
    /// <param name = "annotation">The widget or FreeText annotation.</param>
    /// <param name = "fonts">Receives the selected font.</param>
    /// <param name = "queue">Receives any Type 3 content.</param>
    private static void AddAppearanceFont(PdfPage page, PdfDictionary annotation, HashSet<PdfDictionary> fonts, Queue<FontDataContent> queue)
    {
        var store = page.Dictionary.Owner!;
        var form = store.Catalog.GetDictionary(KnownName.AcroForm);
        var appearance = DefaultAppearance.Find(annotation, annotation, form);
        var name = store.Names.Intern(Encoding.UTF8.GetBytes(appearance.FontName));
        var scope = new FontResourceScope(FieldAttributes.Find(annotation, KnownName.DR).AsDictionary(), new(form?.GetDictionary(KnownName.DR), new(page.Resources, null)));
        var font = scope.Find(KnownName.Font, name).AsDictionary() ?? FormFont.CreateFallback(store).Resource.AsDictionary();
        AddFont(font, scope, fonts, queue);
    }
}
