// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks that a graphic in the appearance of a signature has alternative text (WTPDF 1.0, clause 8.10.3.5): each path, shading or image the
/// normal appearance paints sits inside marked content that has an <c>Alt</c> entry.
/// </summary>
internal static class WtpdfSignatureScan
{
    /// <summary>The most nested marked content sequences followed; deeper ones are read as if they ended.</summary>
    private const int MaxDepth = 64;

    /// <summary>Checks the signature widgets of every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfDocumentPages.GetPage(document, index).Dictionary.GetArray(KnownName.Annots) is { } annotations)
            {
                CheckAnnotations(document.Objects, annotations, findings);
            }
        }
    }

    /// <summary>Determines whether a field's type, inherited from its parents, is signature.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns><see langword="true"/> for a signature field.</returns>
    private static bool IsSignature(PdfDictionary widget)
    {
        for (var current = widget; current is not null; current = current.GetDictionary(KnownName.Parent))
        {
            if (!current.Get(KnownName.FT).IsNull)
            {
                return current.GetName(KnownName.FT).ToKnownName() == KnownName.Sig;
            }
        }

        return false;
    }

    /// <summary>Checks the signature widgets among a page's annotations.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="annotations">The page's annotations.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckAnnotations(PdfObjectStore store, PdfArray annotations, List<PdfWtpdfFinding> findings)
    {
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation || annotation.GetName(KnownName.Subtype).ToKnownName() != KnownName.Widget || !IsSignature(annotation))
            {
                continue;
            }

            if (annotation.GetDictionary(KnownName.AP)?.GetStream(KnownName.N) is { } appearance && HasGraphicWithoutAlt(store, appearance))
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.3.5", "A graphic in the appearance of a signature needs alternative text: put it in marked content that has an Alt entry.", 0));
            }
        }
    }

    /// <summary>Determines whether an appearance paints a graphic outside marked content that has an <c>Alt</c>.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="appearance">The appearance stream.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasGraphicWithoutAlt(PdfObjectStore store, PdfStream appearance)
    {
        var content = default(PooledBuffer);
        try
        {
            _ = appearance.Decode(ref content);
            Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
            var reader = new ContentReader(content.WrittenSpan, store.Names, operands);
            var resources = appearance.Dictionary.GetDictionary(KnownName.Resources);
            ulong alts = 0;
            var depth = 0;
            while (reader.Next(out var op))
            {
                switch (op)
                {
                    case ContentOperator.BeginMarkedContent:
                    {
                        Push(ref alts, ref depth, false);
                        break;
                    }

                    case ContentOperator.BeginMarkedContentProperties:
                    {
                        Push(ref alts, ref depth, HasAlt(store, resources, ref reader));
                        break;
                    }

                    case ContentOperator.EndMarkedContent:
                    {
                        alts >>= 1;
                        depth = Math.Max(0, depth - 1);
                        break;
                    }

                    default:
                    {
                        if (alts == 0 && PaintsGraphic(resources, op, ref reader))
                        {
                            return true;
                        }

                        break;
                    }
                }
            }

            return false;
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Records a marked content sequence.</summary>
    /// <param name="alts">One bit for each open sequence, set when it has an Alt entry.</param>
    /// <param name="depth">How many sequences are open.</param>
    /// <param name="hasAlt">Whether the new sequence has an Alt entry.</param>
    private static void Push(ref ulong alts, ref int depth, bool hasAlt)
    {
        if (depth >= MaxDepth)
        {
            return;
        }

        alts = (alts << 1) | (hasAlt ? 1UL : 0UL);
        depth++;
    }

    /// <summary>Determines whether the property list of a <c>BDC</c> has an <c>Alt</c> entry.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="resources">The appearance's resources.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasAlt(PdfObjectStore store, PdfDictionary? resources, ref ContentReader reader)
    {
        var operand = reader.Operand(1);
        var properties = operand.Kind switch
        {
            ContentOperandKind.Dictionary => PdfParser.ForWindow(reader.Body(operand), 0, store, store.Names, null).ParseValue().AsDictionary(),
            ContentOperandKind.Name => resources?.GetDictionary(KnownName.Properties)?.GetDictionary(operand.Name),
            _ => null,
        };
        return properties is not null && !properties.Get(KnownName.Alt).IsNull;
    }

    /// <summary>Determines whether an operator paints a path, shading or image.</summary>
    /// <param name="resources">The appearance's resources.</param>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns><see langword="true"/> when it paints a graphic.</returns>
    private static bool PaintsGraphic(PdfDictionary? resources, ContentOperator op, ref ContentReader reader) => op switch
    {
        ContentOperator.PaintShading or ContentOperator.EndInlineImage => true,
        ContentOperator.PaintXObject => IsImage(resources, reader.Operand(0).Name),
        _ => PaintsPath(op),
    };

    /// <summary>Determines whether an operator fills or strokes a path.</summary>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="true"/> when it paints the current path.</returns>
    private static bool PaintsPath(ContentOperator op) =>
        op is ContentOperator.Fill or ContentOperator.FillEvenOdd or ContentOperator.Stroke or ContentOperator.CloseStroke
            or ContentOperator.FillStroke or ContentOperator.FillStrokeEvenOdd or ContentOperator.CloseFillStroke or ContentOperator.CloseFillStrokeEvenOdd;

    /// <summary>Determines whether a named external object is an image.</summary>
    /// <param name="resources">The appearance's resources.</param>
    /// <param name="name">The object's name.</param>
    /// <returns><see langword="true"/> for an image.</returns>
    private static bool IsImage(PdfDictionary? resources, PdfName name) =>
        resources?.GetDictionary(KnownName.XObject)?.GetStream(name) is { } stream && stream.Dictionary.GetName(KnownName.Subtype).ToKnownName() == KnownName.Image;
}
