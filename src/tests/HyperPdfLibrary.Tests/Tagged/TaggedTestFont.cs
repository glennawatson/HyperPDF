// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>A test font with one-byte codes that map to the same Unicode character, half an em wide and drawing nothing.</summary>
[DebuggerDisplay("TaggedTestFont")]
internal sealed class TaggedTestFont : PdfFont
{
    /// <summary>The advance in text space units.</summary>
    private const float Advance = 0.5F;

    /// <summary>Initializes a new instance of the <see cref="TaggedTestFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    internal TaggedTestFont(PdfDictionary dictionary)
        : base(dictionary)
    {
    }

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => Advance;

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code) => null;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        destination[0] = (char)code;
        return 1;
    }
}
