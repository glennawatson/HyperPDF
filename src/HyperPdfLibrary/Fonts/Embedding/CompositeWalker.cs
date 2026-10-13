// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Steps through the components of a composite TrueType glyph.</summary>
internal ref struct CompositeWalker
{
    /// <summary>The bytes of a glyph header.</summary>
    private const int GlyphHeader = 10;

    /// <summary>The bytes of a component's flags and glyph index.</summary>
    private const int ComponentHeader = 4;

    /// <summary>The offset of a component's glyph index.</summary>
    private const int ComponentGlyph = 2;

    /// <summary>The composite flag: arguments are 16-bit.</summary>
    private const int ArgsAreWords = 0x0001;

    /// <summary>The composite flag: one scale follows.</summary>
    private const int HasScale = 0x0008;

    /// <summary>The composite flag: more components follow.</summary>
    private const int MoreComponents = 0x0020;

    /// <summary>The composite flag: separate x and y scales follow.</summary>
    private const int HasXyScale = 0x0040;

    /// <summary>The composite flag: a two by two matrix follows.</summary>
    private const int HasMatrix = 0x0080;

    /// <summary>The bytes of two 16-bit arguments.</summary>
    private const int WordArgs = 4;

    /// <summary>The bytes of two 8-bit arguments.</summary>
    private const int ByteArgs = 2;

    /// <summary>The bytes of one 2.14 scale.</summary>
    private const int ScaleBytes = 2;

    /// <summary>The bytes of two 2.14 scales.</summary>
    private const int XyScaleBytes = 4;

    /// <summary>The bytes of a 2.14 matrix.</summary>
    private const int MatrixBytes = 8;

    /// <summary>The most components read from one composite glyph.</summary>
    private const int MaxComponents = 256;

    /// <summary>The glyph's data.</summary>
    private readonly ReadOnlySpan<byte> _glyph;

    /// <summary>The offset of the next component.</summary>
    private int _position;

    /// <summary>The number of components read.</summary>
    private int _read;

    /// <summary>Whether the last component said more follow.</summary>
    private bool _more;

    /// <summary>Initializes a new instance of the <see cref="CompositeWalker"/> struct.</summary>
    /// <param name="glyph">The glyph's data; a simple glyph has no components.</param>
    internal CompositeWalker(ReadOnlySpan<byte> glyph)
    {
        _glyph = glyph;
        _position = GlyphHeader;
        _more = glyph.Length >= GlyphHeader && BinaryPrimitives.ReadInt16BigEndian(glyph) < 0;
        GlyphOffset = 0;
        Glyph = 0;
    }

    /// <summary>Gets the offset of the current component's glyph index within the glyph's data.</summary>
    internal int GlyphOffset { get; private set; }

    /// <summary>Gets the current component's glyph index.</summary>
    internal int Glyph { get; private set; }

    /// <summary>Moves to the next component.</summary>
    /// <returns><see langword="true"/> when there is one.</returns>
    internal bool MoveNext()
    {
        if (!_more || _read >= MaxComponents || _position + ComponentHeader > _glyph.Length)
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(_glyph[_position..]);
        GlyphOffset = _position + ComponentGlyph;
        Glyph = BinaryPrimitives.ReadUInt16BigEndian(_glyph[GlyphOffset..]);
        _position += ComponentHeader + ArgumentBytes(flags);
        _more = (flags & MoreComponents) != 0;
        _read++;
        return true;
    }

    /// <summary>Gets the bytes of a component's arguments and transform.</summary>
    /// <param name="flags">The component's flags.</param>
    /// <returns>The bytes after its header.</returns>
    private static int ArgumentBytes(int flags)
    {
        var bytes = (flags & ArgsAreWords) != 0 ? WordArgs : ByteArgs;
        if ((flags & HasScale) != 0)
        {
            return bytes + ScaleBytes;
        }

        if ((flags & HasXyScale) != 0)
        {
            return bytes + XyScaleBytes;
        }

        return (flags & HasMatrix) != 0 ? bytes + MatrixBytes : bytes;
    }
}
