// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;

namespace HyperPdf.Cms;

/// <summary>Walks BER and DER values over spans without allocating.</summary>
internal static class CmsDer
{
    /// <summary>Reads the value at the start of some bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="tag">The value's tag.</param>
    /// <param name="content">The value's content bytes.</param>
    /// <param name="consumed">The bytes the whole value takes.</param>
    /// <returns><see langword="true"/> when a complete value was read.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> data, out Asn1Tag tag, out ReadOnlySpan<byte> content, out int consumed)
    {
        content = default;
        if (!AsnDecoder.TryReadEncodedValue(data, AsnEncodingRules.BER, out tag, out var offset, out var length, out consumed))
        {
            return false;
        }

        content = data.Slice(offset, length);
        return true;
    }

    /// <summary>Reads the value at the start of some bytes, requiring a tag.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="expected">The tag required.</param>
    /// <param name="content">The value's content bytes.</param>
    /// <returns><see langword="true"/> when the value has the tag.</returns>
    internal static bool TryReadChild(ReadOnlySpan<byte> data, Asn1Tag expected, out ReadOnlySpan<byte> content) =>
        TryRead(data, out var tag, out content, out _) && tag == expected;

    /// <summary>Reads the next value of a container's content and moves past it.</summary>
    /// <param name="data">The container's content.</param>
    /// <param name="offset">Where the next value starts; moved past it.</param>
    /// <param name="tag">The value's tag.</param>
    /// <param name="start">Where the whole value starts.</param>
    /// <param name="content">The value's content bytes.</param>
    /// <returns><see langword="true"/> when a value was read.</returns>
    internal static bool TryNext(ReadOnlySpan<byte> data, scoped ref int offset, out Asn1Tag tag, out int start, out ReadOnlySpan<byte> content)
    {
        start = offset;
        if (offset >= data.Length || !TryRead(data[offset..], out tag, out content, out var consumed))
        {
            tag = default;
            content = default;
            return false;
        }

        offset += consumed;
        return true;
    }
}
