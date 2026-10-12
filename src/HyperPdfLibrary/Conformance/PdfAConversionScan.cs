// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a document once before a PDF/A conversion changes it: which device colour spaces it uses (ISO 19005-2 §6.2.4.3),
/// and the content stream features no fix can change without rewriting the content, such as undefined operators
/// (ISO 19005-2 §6.2.2) and LZW-compressed inline images (ISO 19005-2 §6.1.7.2). The scan is conservative: a device
/// colour space named anywhere counts as used.
/// </summary>
internal static class PdfAConversionScan
{
    /// <summary>Checks cancellation after each 64 objects.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>The bytes around an inline image dictionary's entries: two opening and two closing angle brackets.</summary>
    private const int DictionaryBrackets = BracketWidth + BracketWidth;

    /// <summary>The bytes that open or close a dictionary.</summary>
    private const int BracketWidth = 2;

    /// <summary>Scans the document.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns>What was found.</returns>
    internal static PdfAConversionFacts Scan(PdfAConversionSession session)
    {
        var facts = new PdfAConversionFacts();
        var store = session.Store;
        var graph = PdfObjectGraph.Collect(store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            if ((number & CancellationCheckMask) == 0)
            {
                session.CancellationToken.ThrowIfCancellationRequested();
            }

            var value = graph.GetValue(number);
            PdfAConversionSpaceColours.Mark(store, value, facts, 0);
            if (value.AsStream() is { } stream && IsContentStream(stream.Dictionary))
            {
                ScanContent(session, stream, stream.Dictionary.GetDictionary(KnownName.Resources), facts);
            }

            if (value.AsDictionary() is { } font && font.IsName(KnownName.Subtype, KnownName.Type3) && font.GetDictionary(KnownName.CharProcs) is { } procs)
            {
                ScanCharProcs(session, procs, font.GetDictionary(KnownName.Resources), facts);
            }
        }

        for (var i = 0; i < session.Document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(session.Document, i);
            ScanContents(session, page.Dictionary.Get(KnownName.Contents), page.Resources, facts);
        }

        return facts;
    }

    /// <summary>Determines whether a stream holds content: a form XObject or a tiling pattern.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsContentStream(PdfDictionary dictionary) =>
        dictionary.IsName(KnownName.Subtype, KnownName.Form) || dictionary.GetInt32(KnownName.PatternType, 0) == 1;

    /// <summary>Scans the glyph procedures of a Type 3 font.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="procs">The /CharProcs dictionary.</param>
    /// <param name="resources">The font's resources, or <see langword="null"/>.</param>
    /// <param name="facts">The facts.</param>
    private static void ScanCharProcs(PdfAConversionSession session, PdfDictionary procs, PdfDictionary? resources, PdfAConversionFacts facts)
    {
        for (var i = 0; i < procs.Count; i++)
        {
            if (StoreReading.Resolve(session.Store, procs.GetValueAt(i)).AsStream() is { } proc)
            {
                ScanContent(session, proc, resources, facts);
            }
        }
    }

    /// <summary>Scans a page's content: one stream or an array of them.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="contents">The /Contents value.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="facts">The facts.</param>
    private static void ScanContents(PdfAConversionSession session, PdfValue contents, PdfDictionary? resources, PdfAConversionFacts facts)
    {
        if (contents.AsStream() is { } single)
        {
            ScanContent(session, single, resources, facts);
            return;
        }

        var parts = contents.AsArray();
        for (var i = 0; parts is not null && i < parts.Count; i++)
        {
            if (parts.Get(i).AsStream() is { } part)
            {
                ScanContent(session, part, resources, facts);
            }
        }
    }

    /// <summary>Decodes and scans one content stream.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="resources">The resources its content names, or <see langword="null"/>.</param>
    /// <param name="facts">The facts.</param>
    private static void ScanContent(PdfAConversionSession session, PdfStream stream, PdfDictionary? resources, PdfAConversionFacts facts)
    {
        var buffer = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref buffer, session.CancellationToken);
            var defaults = new PdfAConversionDefaultSpaces(resources);
            ScanOperators(session, buffer.WrittenSpan, defaults, facts);
        }
        catch (Exception ex) when (ContentExecution.IsRecoverable(ex))
        {
            facts.UnreadableStreams++;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads the operators of decoded content.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="content">The content.</param>
    /// <param name="defaults">The default colour spaces the resources set.</param>
    /// <param name="facts">The facts.</param>
    private static void ScanOperators(PdfAConversionSession session, ReadOnlySpan<byte> content, PdfAConversionDefaultSpaces defaults, PdfAConversionFacts facts)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, session.Store.Names, operands);
        var depth = 0;
        while (reader.Next(out var op))
        {
            switch (op)
            {
                case ContentOperator.Unknown:
                    {
                        facts.UndefinedOperators++;
                        break;
                    }

                case ContentOperator.Save:
                    {
                        depth++;
                        facts.MaxSaveDepth = Math.Max(facts.MaxSaveDepth, depth);
                        break;
                    }

                case ContentOperator.Restore:
                    {
                        depth = Math.Max(0, depth - 1);
                        break;
                    }

                case ContentOperator.BeginInlineImage:
                    {
                        ScanInlineImage(session, ref reader, defaults, facts);
                        break;
                    }

                default:
                    {
                        PdfAConversionOperatorColours.Mark(session.Store.Names, op, reader.Operand(0), defaults, facts);
                        break;
                    }
            }
        }
    }

    /// <summary>Reads an inline image's dictionary for its colour space, filters and interpolation.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="reader">The reader, positioned after the image.</param>
    /// <param name="defaults">The default colour spaces.</param>
    /// <param name="facts">The facts.</param>
    private static void ScanInlineImage(PdfAConversionSession session, ref ContentReader reader, PdfAConversionDefaultSpaces defaults, PdfAConversionFacts facts)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = Math.Max(0, range.End.GetOffset(content.Length) - offset);
        var text = new byte[length + DictionaryBrackets];
        "<<"u8.CopyTo(text);
        content.Slice(offset, length).CopyTo(text.AsSpan(BracketWidth));
        ">>"u8.CopyTo(text.AsSpan(length + BracketWidth));
        var parser = new PdfParser(text, 0, session.Store, session.Store.Names);
        if (parser.ParseValue().AsDictionary() is not { } image)
        {
            return;
        }

        PdfAConversionInlineImages.Mark(session.Store, image, defaults, facts);
    }
}
