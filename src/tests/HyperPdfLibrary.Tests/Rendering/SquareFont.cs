// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A test font whose every glyph is a filled square one em wide, with a half em advance.</summary>
[DebuggerDisplay("SquareFont")]
internal sealed class SquareFont : PdfFont
{
    /// <summary>The glyph units in one em.</summary>
    private const float Em = 1000;

    /// <summary>The advance in text space units.</summary>
    private const float Advance = 0.5F;

    /// <summary>The glyph outline.</summary>
    private readonly PdfPath _square;

    /// <summary>Initializes a new instance of the <see cref="SquareFont"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    internal SquareFont(PdfDictionary dictionary)
        : base(dictionary)
    {
        var builder = new PdfPathBuilder();
        builder.AddRect(new(0, 0, Em, Em));
        _square = builder.Detach();
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
    public override PdfPath? GetOutline(int code) => code == ' ' ? null : _square;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        destination[0] = (char)code;
        return 1;
    }
}
