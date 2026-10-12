// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Finds pages and references in a PDF's structure.</summary>
internal static class PdfPages
{
    /// <summary>Finds a page's object number by walking the page tree.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <returns>The page's object number.</returns>
    /// <exception cref="InvalidDataException">The page tree is damaged or the page does not exist.</exception>
    internal static int FindPage(PdfStructure structure, ReadOnlyMemory<byte> catalog, int pageIndex)
    {
        var node = ReadReference(catalog.Span, "Pages"u8);
        var remaining = pageIndex;
        for (var depth = 0; depth < byte.MaxValue; depth++)
        {
            var dictionary = PdfReader.GetObject(structure, node);
            var kids = PdfSyntax.FindKey(dictionary.Span, 0, "Kids"u8);
            if (kids < 0)
            {
                return remaining == 0 ? node : throw new InvalidDataException("The page does not exist.");
            }

            node = FindKid(structure, PdfReader.Resolve(structure, dictionary, kids), ref remaining);
        }

        throw new InvalidDataException("The page tree is too deep.");
    }

    /// <summary>Reads a reference held under a key.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The referenced object number.</returns>
    /// <exception cref="InvalidDataException">The key is missing or not a reference.</exception>
    internal static int ReadReference(ReadOnlySpan<byte> dictionary, ReadOnlySpan<byte> key)
    {
        var at = PdfSyntax.FindKey(dictionary, 0, key);
        return at >= 0 && PdfSyntax.TryReadReference(dictionary, at, out var number)
            ? number
            : throw new InvalidDataException($"/{Encoding.ASCII.GetString(key)} is missing.");
    }

    /// <summary>Finds the kid of a page tree node holding a page, skipping whole subtrees by their counts.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="kids">The kids array.</param>
    /// <param name="remaining">The page's index within this node; reduced by the pages skipped.</param>
    /// <returns>The kid's object number.</returns>
    /// <exception cref="InvalidDataException">The page does not exist.</exception>
    private static int FindKid(PdfStructure structure, ReadOnlyMemory<byte> kids, ref int remaining)
    {
        var span = kids.Span;
        var index = PdfSyntax.SkipSpace(span, 1);
        while (index < span.Length && span[index] != (byte)']')
        {
            if (!PdfSyntax.TryReadReference(span, index, out var kid))
            {
                break;
            }

            var dictionary = PdfReader.GetObject(structure, kid).Span;
            var countAt = PdfSyntax.FindKey(dictionary, 0, "Count"u8);
            var pages = countAt >= 0 && PdfSyntax.FindKey(dictionary, 0, "Kids"u8) >= 0 && PdfSyntax.ReadLong(dictionary, countAt, out var count) >= 0 ? (int)count : 1;
            if (remaining < pages)
            {
                return kid;
            }

            remaining -= pages;
            index = PdfSyntax.SkipSpace(span, PdfSyntax.ValueEnd(span, index));
        }

        throw new InvalidDataException("The page does not exist.");
    }
}
