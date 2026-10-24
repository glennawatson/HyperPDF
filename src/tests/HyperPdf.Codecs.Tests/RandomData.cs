// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdf.Codecs.Tests;

/// <summary>Makes data from a seed, so a test that fails can be run again with the same data.</summary>
internal static class RandomData
{
    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>Makes random bytes.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Bytes(int seed, int length)
    {
        var random = new SeededRandom(seed);
        var data = new byte[length];
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = (byte)random.Next(ByteValues);
        }

        return data;
    }
}
