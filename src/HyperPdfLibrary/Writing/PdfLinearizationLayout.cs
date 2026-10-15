// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Records the byte positions that the next linearized write pass declares. The positions live in arrays rented from
/// the shared pool, which <see cref="Dispose"/> returns.
/// </summary>
internal ref struct PdfLinearizationLayout : IDisposable
{
    /// <summary>The number of indirect objects plus the free entry.</summary>
    private readonly int _objectSlots;

    /// <summary>The number of page groups.</summary>
    private readonly int _pageCount;

    /// <summary>Each indirect object's byte offset.</summary>
    private long[]? _objectOffsets;

    /// <summary>The byte length of each indirect object.</summary>
    private long[]? _objectLengths;

    /// <summary>Each page group's starting offset.</summary>
    private long[]? _pageStarts;

    /// <summary>The offset after each page group.</summary>
    private long[]? _pageEnds;

    /// <summary>Initializes a new instance of the <see cref="PdfLinearizationLayout"/> struct with every position zero.</summary>
    /// <param name="objectCount">The highest indirect object number.</param>
    /// <param name="pageCount">The number of page groups.</param>
    internal PdfLinearizationLayout(int objectCount, int pageCount)
    {
        _objectSlots = objectCount + 1;
        _pageCount = pageCount;
        _objectOffsets = Rent(_objectSlots);
        _objectLengths = Rent(_objectSlots);
        _pageStarts = Rent(pageCount);
        _pageEnds = Rent(pageCount);
        FirstXrefOffset = 0;
        HintOffset = 0;
        HintLength = 0;
        MainXrefOffset = 0;
        MainXrefItemOffset = 0;
        FileLength = 0;
        FileId = default;
    }

    /// <summary>Gets each indirect object's byte offset.</summary>
    internal readonly Span<long> ObjectOffsets => _objectOffsets.AsSpan(0, _objectSlots);

    /// <summary>Gets the byte length of each indirect object.</summary>
    internal readonly Span<long> ObjectLengths => _objectLengths.AsSpan(0, _objectSlots);

    /// <summary>Gets each page group's starting offset.</summary>
    internal readonly Span<long> PageStarts => _pageStarts.AsSpan(0, _pageCount);

    /// <summary>Gets the offset after each page group.</summary>
    internal readonly Span<long> PageEnds => _pageEnds.AsSpan(0, _pageCount);

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

    /// <summary>Returns the arrays to the pool.</summary>
    public void Dispose()
    {
        Return(ref _objectOffsets);
        Return(ref _objectLengths);
        Return(ref _pageStarts);
        Return(ref _pageEnds);
    }

    /// <summary>Sets every position to zero.</summary>
    internal void Reset()
    {
        ObjectOffsets.Clear();
        ObjectLengths.Clear();
        PageStarts.Clear();
        PageEnds.Clear();
        FirstXrefOffset = 0;
        HintOffset = 0;
        HintLength = 0;
        MainXrefOffset = 0;
        MainXrefItemOffset = 0;
        FileLength = 0;
        FileId = default;
    }

    /// <summary>Checks whether another pass wrote all measured positions unchanged.</summary>
    /// <param name="other">The preceding pass.</param>
    /// <returns>Whether all header and hint positions are stable.</returns>
    internal readonly bool Matches(PdfLinearizationLayout other) =>
        FileLength == other.FileLength
        && FirstXrefOffset == other.FirstXrefOffset
        && HintOffset == other.HintOffset
        && HintLength == other.HintLength
        && MainXrefOffset == other.MainXrefOffset
        && MainXrefItemOffset == other.MainXrefItemOffset
        && ObjectOffsets.SequenceEqual(other.ObjectOffsets)
        && ObjectLengths.SequenceEqual(other.ObjectLengths)
        && PageStarts.SequenceEqual(other.PageStarts)
        && PageEnds.SequenceEqual(other.PageEnds);

    /// <summary>Rents an array of positions that are zero.</summary>
    /// <param name="length">The number of positions.</param>
    /// <returns>The array.</returns>
    private static long[] Rent(int length)
    {
        var array = ArrayPool<long>.Shared.Rent(Math.Max(length, 1));
        array.AsSpan(0, length).Clear();
        return array;
    }

    /// <summary>Returns an array to the pool.</summary>
    /// <param name="array">The array; set to <see langword="null"/>.</param>
    private static void Return(ref long[]? array)
    {
        if (array is not null)
        {
            ArrayPool<long>.Shared.Return(array);
        }

        array = null;
    }
}
