// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Writes the deprecated <c>F</c> fill operator as <c>f</c> in every content stream (ISO 32000-2 8.5.3.1, Table 59): page
/// contents, form XObjects, tiling patterns, Type 3 glyph procedures and annotation appearances. The two operators fill
/// the same way, so the page looks the same. A stream is rewritten only when it holds an <c>F</c>.
/// </summary>
internal static class Pdf20ConversionContent
{
    /// <summary>Checks cancellation after each 64 objects.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>Rewrites the streams that use <c>F</c>.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(Pdf20ConversionSession session)
    {
        var rewritten = 0;
        foreach (var id in FindContentStreams(session))
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            if (StoreReading.Resolve(session.Store, PdfValue.FromReference(id)).AsStream() is { } stream && Rewrite(session, id, stream))
            {
                rewritten++;
            }
        }

        if (rewritten > 0)
        {
            session.Changed(
                "8.5.3.1",
                string.Create(CultureInfo.InvariantCulture, $"Wrote the fill operator F as f in {rewritten} content streams, as PDF 2.0 deprecates F. The pages look the same."));
        }
    }

    /// <summary>Lists the objects that hold content: page contents, form XObjects, tiling patterns and Type 3 glyph procedures.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns>The objects.</returns>
    private static HashSet<PdfObjectId> FindContentStreams(Pdf20ConversionSession session)
    {
        var found = new HashSet<PdfObjectId>();
        var graph = PdfObjectGraph.Collect(session.Store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            if ((number & CancellationCheckMask) == 0)
            {
                session.CancellationToken.ThrowIfCancellationRequested();
            }

            var value = graph.GetValue(number);
            if (value.AsStream() is { } stream && IsContentStream(stream.Dictionary))
            {
                _ = found.Add(new(graph.GetOldNumber(number), 0));
            }

            AddReferencedContent(found, value.AsDictionary());
        }

        return found;
    }

    /// <summary>Adds the content a page or a Type 3 font names.</summary>
    /// <param name="found">The objects found.</param>
    /// <param name="dictionary">A dictionary of the document, or <see langword="null"/>.</param>
    private static void AddReferencedContent(HashSet<PdfObjectId> found, PdfDictionary? dictionary)
    {
        if (dictionary is null)
        {
            return;
        }

        AddPageContents(found, dictionary);
        AddGlyphProcedures(found, dictionary);
    }

    /// <summary>Determines whether a stream holds content: a form XObject or a tiling pattern.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsContentStream(PdfDictionary dictionary) =>
        dictionary.IsName(KnownName.Subtype, KnownName.Form) || dictionary.GetInt32(KnownName.PatternType, 0) == 1;

    /// <summary>Adds the streams a page dictionary names in /Contents.</summary>
    /// <param name="found">The objects found.</param>
    /// <param name="dictionary">A dictionary of the document.</param>
    private static void AddPageContents(HashSet<PdfObjectId> found, PdfDictionary dictionary)
    {
        if (!dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            return;
        }

        var contents = dictionary.GetRaw(KnownName.Contents);
        if (contents.IsReference)
        {
            _ = found.Add(contents.AsReference());
        }

        var parts = dictionary.GetArray(KnownName.Contents);
        for (var i = 0; parts is not null && i < parts.Count; i++)
        {
            if (parts.GetRaw(i).IsReference)
            {
                _ = found.Add(parts.GetRaw(i).AsReference());
            }
        }
    }

    /// <summary>Adds the glyph procedures of a Type 3 font.</summary>
    /// <param name="found">The objects found.</param>
    /// <param name="dictionary">A dictionary of the document.</param>
    private static void AddGlyphProcedures(HashSet<PdfObjectId> found, PdfDictionary dictionary)
    {
        if (!dictionary.IsName(KnownName.Subtype, KnownName.Type3) || dictionary.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return;
        }

        for (var i = 0; i < procedures.Count; i++)
        {
            if (procedures.GetValueAt(i).IsReference)
            {
                _ = found.Add(procedures.GetValueAt(i).AsReference());
            }
        }
    }

    /// <summary>Rewrites one content stream when it uses <c>F</c>.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="id">The stream's object.</param>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> when the stream was rewritten.</returns>
    private static bool Rewrite(Pdf20ConversionSession session, PdfObjectId id, PdfStream stream)
    {
        var decoded = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref decoded, session.CancellationToken);

            // The F operator needs an F byte, and most streams have none, so they are neither copied nor read.
            if (decoded.WrittenSpan.IndexOf((byte)'F') < 0)
            {
                return false;
            }

            var content = decoded.WrittenSpan.ToArray();
            if (!ReplaceFill(content, session.Store.Names))
            {
                return false;
            }

            var dictionary = stream.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Length);
            _ = dictionary.Remove(KnownName.DL);
            _ = dictionary.Remove(KnownName.DecodeParms);
            StreamRecompressor.Deflate(content, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            StoreEditing.Replace(session.Store, id, PdfValue.FromStream(new(dictionary, compressed.ToArray())));
            return true;
        }
        catch (Exception ex) when (ContentExecution.IsRecoverable(ex))
        {
            // A stream that cannot be read stays as it is; the writer handles it as it did when the file was opened.
            return false;
        }
        finally
        {
            decoded.Dispose();
            compressed.Dispose();
        }
    }

    /// <summary>Changes every <c>F</c> operator in the content to <c>f</c>, in place; both are one byte.</summary>
    /// <param name="content">The decoded content.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when an operator was changed.</returns>
    private static bool ReplaceFill(byte[] content, PdfNameTable names)
    {
        var changed = false;
        var found = new List<int>();
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, names, operands);
        while (reader.Next(out var op))
        {
            if (op == ContentOperator.Fill && content[reader.Position - 1] == (byte)'F')
            {
                found.Add(reader.Position - 1);
            }
        }

        foreach (var position in found)
        {
            content[position] = (byte)'f';
            changed = true;
        }

        return changed;
    }
}
