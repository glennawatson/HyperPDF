// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Creation;

/// <summary>Validates standard type spellings before structure objects are reserved.</summary>
internal static class PdfTagTypeNames
{
    /// <summary>The longest fixed standard spelling.</summary>
    private const int FixedNameLength = 32;

    /// <summary>Resolves a spelling and rejects types unavailable in a PDF 1.7-only tree.</summary>
    /// <param name="type">The requested type spelling.</param>
    /// <param name="allowsPdf20">Whether the tree can explicitly identify the PDF 2.0 namespace.</param>
    /// <returns>The resolved standard type.</returns>
    /// <exception cref="ArgumentException">The type is unknown or needs an unavailable namespace.</exception>
    internal static PdfStructureType Validate(string type, bool allowsPdf20)
    {
        Span<byte> bytes = stackalloc byte[FixedNameLength];
        var resolved = type.Length switch
        {
            <= FixedNameLength => PdfStructureTypes.Find(bytes[..Encoding.ASCII.GetBytes(type, bytes)]),
            _ when NumberedHeadings.IsName(type.AsSpan()) => PdfStructureType.NumberedHeading,
            _ => PdfStructureType.Unknown,
        };

        if (resolved == PdfStructureType.Unknown)
        {
            throw new ArgumentException($"'{type}' is not a standard structure type.", nameof(type));
        }

        // Extended PDF 1.7 headings receive a role map when the tree is finished.
        if (!allowsPdf20 && resolved != PdfStructureType.NumberedHeading
            && !PdfStructureTypes.IsStandardIn(resolved, PdfStructureTypes.Pdf17Flag))
        {
            throw new ArgumentException($"'{type}' requires the PDF 2.0 structure namespace.", nameof(type));
        }

        return resolved;
    }
}
