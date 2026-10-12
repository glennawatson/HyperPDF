// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>What the JP2 header reader has found so far.</summary>
[DebuggerDisplay("PdfAJpxBoxes: {Channels} channels, {Specifications} colour specifications")]
internal sealed class PdfAJpxBoxes
{
    /// <summary>Gets or sets the number of components.</summary>
    internal long Channels { get; set; }

    /// <summary>Gets or sets the bit depth.</summary>
    internal long BitDepth { get; set; }

    /// <summary>Gets or sets a value indicating whether a bits-per-component box is present.</summary>
    internal bool HasBitsPerComponentBox { get; set; }

    /// <summary>Gets or sets the number of colour specification boxes.</summary>
    internal long Specifications { get; set; }

    /// <summary>Gets or sets the number of colour specification boxes with APPROX 1.</summary>
    internal long Approximate { get; set; }

    /// <summary>Gets or sets the METH of the first colour specification box; zero before one is read.</summary>
    internal long FirstMethod { get; set; }

    /// <summary>Gets or sets the enumerated space of the first box that has one; -1 before one is read.</summary>
    internal long FirstEnumerated { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the first colour space has been read.</summary>
    internal bool FirstSpaceRead { get; set; }

    /// <summary>Gets or sets the first colour space read.</summary>
    internal PdfAJpxSpace? FirstSpace { get; set; }

    /// <summary>Gets or sets the METH of the first box with APPROX 1; zero before one is read.</summary>
    internal long ApproximateMethod { get; set; }

    /// <summary>Gets or sets the enumerated space of the first box with APPROX 1 that has one; -1 before one is read.</summary>
    internal long ApproximateEnumerated { get; set; } = -1;

    /// <summary>Gets or sets the colour space of the boxes with APPROX 1.</summary>
    internal PdfAJpxSpace? ApproximateSpace { get; set; }

    /// <summary>Builds the header: the APPROX 1 boxes decide the colour space when there are any, else a single box does.</summary>
    /// <returns>The header.</returns>
    internal PdfAJpxHeader Build()
    {
        if (Approximate > 0)
        {
            return new(Channels, BitDepth, HasBitsPerComponentBox, Specifications, Approximate, ApproximateMethod, ApproximateEnumerated, ApproximateSpace);
        }

        return Specifications == 1
            ? new(Channels, BitDepth, HasBitsPerComponentBox, Specifications, Approximate, FirstMethod, FirstEnumerated, FirstSpace)
            : new(Channels, BitDepth, HasBitsPerComponentBox, Specifications, Approximate, 0, -1, null);
    }
}
