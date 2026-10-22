// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks the file identifiers (ISO 19005-1, 6.1.3; ISO 19005-2, 6.1.3) and the cross-reference stream ban of
/// PDF/A-1 (6.1.4). A linearized file is judged by its first-page trailer, as veraPDF judges it.
/// </summary>
internal static class PdfAFileIdentifiers
{
    /// <summary>The bytes of the file's start searched for the linearization dictionary.</summary>
    private const int LinearizationWindow = 1024;

    /// <summary>The numbers in an object header: the object number and the generation.</summary>
    private const int HeaderNumbers = 2;

    /// <summary>Gets the keyword that ends an object header.</summary>
    private static ReadOnlySpan<byte> ObjWord => "obj"u8;

    /// <summary>Gets the keyword that ends an object.</summary>
    private static ReadOnlySpan<byte> EndObjWord => "endobj"u8;

    /// <summary>Records the identifier and cross-reference rules the file breaks.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The cross-reference sections.</param>
    internal static void Check(PdfAFileScan scan, PdfAXrefLayout layout)
    {
        const string where = PdfAContexts.Trailer;
        var context = scan.Context;
        context.Require(layout.LineEnds, PdfARule.XrefLineEnds, where);
        context.Require(layout.SubsectionSpacing, PdfARule.XrefSubsectionHeader, where);
        context.Require(!layout.HasStream, PdfARule.XrefStreamForbidden, where);
        if (layout.First is not { } first || layout.Last is not { } last)
        {
            return;
        }

        var linearized = !ReferenceEquals(layout.Sections[0], last) && IsLinearized(scan);
        var firstId = first.Identifier;
        var lastId = last.Identifier;
        context.Require(linearized ? firstId is not null : lastId is not null, PdfARule.FileIdentifierPdfA1, where);
        context.Require((linearized ? firstId : lastId) is { Length: > 0 }, PdfARule.FileIdentifier, where);
        context.Require(!linearized || lastId is null || (firstId is not null && firstId.AsSpan().SequenceEqual(lastId)), PdfARule.LinearizedFirstIdMatchesLast, where);
    }

    /// <summary>
    /// Determines whether the file starts with a linearization dictionary whose /L is the file's length and whose object
    /// ends in the first kilobyte.
    /// </summary>
    /// <param name="scan">The pass.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsLinearized(PdfAFileScan scan)
    {
        var source = scan.Source;
        using var head = source.Lease(0, (int)Math.Min(LinearizationWindow, source.Length));
        var reader = new PdfASyntaxReader(head.Span, 0, scan.Buffer);
        var start = FindFirstObject(ref reader);
        if (start < 0 || !StoreParsing.TryParseObjectAt(scan.Store, start, out _, out var value)
            || value.AsDictionary() is not { } dictionary || !dictionary.ContainsKey(scan.Store.Names.Intern("Linearized"u8)))
        {
            return false;
        }

        var length = dictionary.Get(KnownName.L).AsInteger(0);
        if (length == 0 || length != source.Length)
        {
            return false;
        }

        // veraPDF also needs the whole object, up to endobj, inside the first kilobyte.
        var findings = scan.Findings;
        var ended = PdfASyntaxValues.Read(ref reader, findings, 0) == PdfASyntaxValue.Dictionary;
        findings.Discard();
        reader.Next();
        return ended && reader.IsWord(EndObjWord) && reader.Position < head.Span.Length;
    }

    /// <summary>Finds the first <c>N G obj</c> header.</summary>
    /// <param name="reader">The reader; left after the header.</param>
    /// <returns>The offset of the object number, or -1.</returns>
    private static int FindFirstObject(ref PdfASyntaxReader reader)
    {
        var numbers = 0;
        var numberStart = -1;
        var previousStart = -1;
        var result = -1;
        var searching = true;
        while (searching)
        {
            reader.SkipSpaces(true);
            var tokenStart = reader.Position;
            reader.Next();
            switch (reader.Kind)
            {
                case PdfASyntaxToken.End:
                {
                    searching = false;
                    break;
                }

                case PdfASyntaxToken.Integer:
                {
                    numbers++;
                    previousStart = numberStart;
                    numberStart = tokenStart;
                    break;
                }

                default:
                {
                    if (reader.IsWord(ObjWord) && numbers >= HeaderNumbers)
                    {
                        result = previousStart;
                        searching = false;
                    }
                    else
                    {
                        numbers = 0;
                    }

                    break;
                }
            }
        }

        return result;
    }
}
