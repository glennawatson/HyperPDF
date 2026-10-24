// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>The square Data Matrix ECC 200 sizes of ISO/IEC 16022, smallest first.</summary>
internal static class DataMatrixSizes
{
    /// <summary>The sizes: side, data codewords, error correction codewords, regions along a side, blocks.</summary>
    private static readonly DataMatrixSize[] All =
    [
        new(10, 3, 5, 1, 1),
        new(12, 5, 7, 1, 1),
        new(14, 8, 10, 1, 1),
        new(16, 12, 12, 1, 1),
        new(18, 18, 14, 1, 1),
        new(20, 22, 18, 1, 1),
        new(22, 30, 20, 1, 1),
        new(24, 36, 24, 1, 1),
        new(26, 44, 28, 1, 1),
        new(32, 62, 36, 2, 1),
        new(36, 86, 42, 2, 1),
        new(40, 114, 48, 2, 1),
        new(44, 144, 56, 2, 1),
        new(48, 174, 68, 2, 1),
        new(52, 204, 84, 2, 2),
        new(64, 280, 112, 4, 2),
        new(72, 368, 144, 4, 4),
        new(80, 456, 192, 4, 4),
        new(88, 576, 224, 4, 4),
        new(96, 696, 272, 4, 4),
        new(104, 816, 336, 4, 6),
        new(120, 1050, 408, 6, 6),
        new(132, 1304, 496, 6, 8),
        new(144, 1558, 620, 6, 10),
    ];

    /// <summary>Finds the smallest size that holds some codewords.</summary>
    /// <param name="count">The data codewords.</param>
    /// <returns>The size.</returns>
    /// <exception cref="ArgumentException">No size holds them.</exception>
    internal static DataMatrixSize Find(int count)
    {
        foreach (var size in All)
        {
            if (size.DataBytes >= count)
            {
                return size;
            }
        }

        throw new ArgumentException("The text is too long for a Data Matrix.", nameof(count));
    }
}
