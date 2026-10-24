// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>A fixed "random" source for the encryption writer: bytes counting up from a seed, so keys and salts repeat.</summary>
/// <param name="seed">The first byte value.</param>
internal sealed class SequenceRandom(byte seed)
{
    /// <summary>The next value to hand out.</summary>
    private byte _next = seed;

    /// <summary>Fills a span with the next values.</summary>
    /// <param name="destination">The span.</param>
    internal void Fill(Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = _next;
            _next++;
        }
    }
}
