// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The strings of a subset's String INDEX: the custom glyph names and the names in <c>ROS</c>, numbered from SID 391.</summary>
/// <param name="face">The font the strings are copied from.</param>
[DebuggerDisplay("CffStringTable: {_items.Count} strings")]
internal sealed class CffStringTable(CffFace face)
{
    /// <summary>The first SID of a string stored in the String INDEX.</summary>
    private const int FirstCustomSid = 391;

    /// <summary>The output strings.</summary>
    private readonly List<byte[]> _items = [];

    /// <summary>The output SID of each source SID already copied.</summary>
    private readonly Dictionary<int, int> _copied = [];

    /// <summary>Adds a string.</summary>
    /// <param name="text">The string.</param>
    /// <returns>The string's SID.</returns>
    internal int Add(byte[] text)
    {
        _items.Add(text);
        return FirstCustomSid + _items.Count - 1;
    }

    /// <summary>Gets the output SID of a source SID, copying a custom string the first time.</summary>
    /// <param name="sid">The source SID.</param>
    /// <returns>The SID in the output.</returns>
    internal int Sid(int sid)
    {
        if (sid < CffStandardData.StandardStringCount)
        {
            return sid;
        }

        if (_copied.TryGetValue(sid, out var known))
        {
            return known;
        }

        var added = Add(face.Strings.Get(face.Data.Span, sid - CffStandardData.StandardStringCount).ToArray());
        _copied[sid] = added;
        return added;
    }

    /// <summary>Gets the strings.</summary>
    /// <returns>The strings in SID order.</returns>
    internal byte[][] ToItems() => [.. _items];
}
