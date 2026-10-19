// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Resolves the inheritable ListNumbering attribute into a reading node's label scheme.</summary>
internal static class ListNumberingReader
{
    /// <summary>Uses the nearest explicit name, leaving private names unnumbered rather than guessing.</summary>
    /// <param name="element">The list's structure element.</param>
    /// <param name="cancellationToken">Cancels the ancestor walk.</param>
    /// <returns>The resolved scheme, or None when no recognised name is present.</returns>
    internal static PdfListNumbering Read(PdfStructureElement element, CancellationToken cancellationToken)
    {
        for (PdfStructureElement? current = element; current is not null; current = current.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (current.Attributes.ListNumbering is { Length: > 0 } name)
            {
                return ReadName(name);
            }
        }

        return PdfListNumbering.None;
    }

    /// <summary>Reads the unordered and description schemes without changing their source names.</summary>
    /// <param name="name">The explicit attribute name.</param>
    /// <returns>The recognised scheme, or None for a private name.</returns>
    private static PdfListNumbering ReadName(string name) => name switch
    {
        "None" => PdfListNumbering.None,
        "Disc" => PdfListNumbering.Disc,
        "Circle" => PdfListNumbering.Circle,
        "Square" => PdfListNumbering.Square,
        "Unordered" => PdfListNumbering.Unordered,
        "Description" => PdfListNumbering.Description,
        _ => ReadOrderedName(name),
    };

    /// <summary>Reads the standard ordered-list schemes.</summary>
    /// <param name="name">The explicit attribute name.</param>
    /// <returns>The recognised scheme, or None for a private name.</returns>
    private static PdfListNumbering ReadOrderedName(string name) => name switch
    {
        "Ordered" => PdfListNumbering.Ordered,
        "Decimal" => PdfListNumbering.Decimal,
        "UpperRoman" => PdfListNumbering.UpperRoman,
        "LowerRoman" => PdfListNumbering.LowerRoman,
        "UpperAlpha" => PdfListNumbering.UpperAlpha,
        "LowerAlpha" => PdfListNumbering.LowerAlpha,
        _ => PdfListNumbering.None,
    };
}
