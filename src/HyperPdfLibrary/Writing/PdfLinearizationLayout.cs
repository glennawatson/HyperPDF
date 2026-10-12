// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Records the byte positions that the next linearized write pass declares.</summary>
/// <param name="objectCount">The highest indirect object number.</param>
/// <param name="pageCount">The number of page groups.</param>
internal sealed class PdfLinearizationLayout(int objectCount, int pageCount)
{
    /// <summary>Gets each indirect object's byte offset.</summary>
    internal long[] ObjectOffsets { get; } = new long[objectCount + 1];

    /// <summary>Gets the byte length of each indirect object.</summary>
    internal long[] ObjectLengths { get; } = new long[objectCount + 1];

    /// <summary>Gets each page group's starting offset.</summary>
    internal long[] PageStarts { get; } = new long[pageCount];

    /// <summary>Gets the offset after each page group.</summary>
    internal long[] PageEnds { get; } = new long[pageCount];

    /// <summary>Gets or sets the first cross-reference table's offset.</summary>
    internal long FirstXrefOffset { get; set; }

    /// <summary>Gets or sets the hint object offset.</summary>
    internal long HintOffset { get; set; }

    /// <summary>Gets or sets the hint object's byte length.</summary>
    internal long HintLength { get; set; }

    /// <summary>Gets or sets the main cross-reference table's offset.</summary>
    internal long MainXrefOffset { get; set; }

    /// <summary>Gets or sets the object-zero entry's offset in the main cross-reference table.</summary>
    internal long MainXrefItemOffset { get; set; }

    /// <summary>Gets or sets the whole file length.</summary>
    internal long FileLength { get; set; }

    /// <summary>Gets or sets the /ID array the trailers carry, or a null value for none.</summary>
    internal PdfValue FileId { get; set; }

    /// <summary>Checks whether another pass wrote all measured positions unchanged.</summary>
    /// <param name="other">The preceding pass.</param>
    /// <returns>Whether all header and hint positions are stable.</returns>
    internal bool Matches(PdfLinearizationLayout other) =>
        FileLength == other.FileLength
        && FirstXrefOffset == other.FirstXrefOffset
        && HintOffset == other.HintOffset
        && HintLength == other.HintLength
        && MainXrefOffset == other.MainXrefOffset
        && MainXrefItemOffset == other.MainXrefItemOffset
        && ObjectOffsets.AsSpan().SequenceEqual(other.ObjectOffsets)
        && ObjectLengths.AsSpan().SequenceEqual(other.ObjectLengths)
        && PageStarts.AsSpan().SequenceEqual(other.PageStarts)
        && PageEnds.AsSpan().SequenceEqual(other.PageEnds);
}
