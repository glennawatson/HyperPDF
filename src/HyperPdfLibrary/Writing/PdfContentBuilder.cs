// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Builds a content stream or appearance stream operator by operator into a pooled buffer, without allocating. Operands
/// are separated by single spaces and every operator ends its line. Names are given unescaped, without the slash, and
/// are escaped as needed. Dispose returns the buffer.
/// </summary>
[DebuggerDisplay("PdfContentBuilder: {Length} bytes")]
public ref struct PdfContentBuilder
{
    /// <summary>The number of entries in a matrix.</summary>
    private const int MatrixLength = 6;

    /// <summary>The content.</summary>
    private PooledBuffer _buffer;

    /// <summary>The items written in the open TJ array.</summary>
    private int _arrayItems;

    /// <summary>Gets the number of bytes written.</summary>
    public readonly int Length => _buffer.Length;

    /// <summary>Gets the content written.</summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    /// <summary>Saves the graphics state (<c>q</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SaveState() => Operator("q"u8);

    /// <summary>Restores the graphics state (<c>Q</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RestoreState() => Operator("Q"u8);

    /// <summary>Concatenates a matrix with the current transformation matrix (<c>cm</c>).</summary>
    /// <param name="a">The a entry.</param>
    /// <param name="b">The b entry.</param>
    /// <param name="c">The c entry.</param>
    /// <param name="d">The d entry.</param>
    /// <param name="e">The horizontal translation.</param>
    /// <param name="f">The vertical translation.</param>
    public void Transform(float a, float b, float c, float d, float e, float f)
    {
        Operands(a, b, c, d);
        Operands(e, f);
        Operator("cm"u8);
    }

    /// <summary>Sets the line width (<c>w</c>).</summary>
    /// <param name="width">The width.</param>
    public void SetLineWidth(float width)
    {
        Operand(width);
        Operator("w"u8);
    }

    /// <summary>Sets the line cap style (<c>J</c>).</summary>
    /// <param name="style">0 butt, 1 round, 2 projecting square.</param>
    public void SetLineCap(int style)
    {
        Operand(style);
        Operator("J"u8);
    }

    /// <summary>Sets the line join style (<c>j</c>).</summary>
    /// <param name="style">0 miter, 1 round, 2 bevel.</param>
    public void SetLineJoin(int style)
    {
        Operand(style);
        Operator("j"u8);
    }

    /// <summary>Sets the miter limit (<c>M</c>).</summary>
    /// <param name="limit">The limit.</param>
    public void SetMiterLimit(float limit)
    {
        Operand(limit);
        Operator("M"u8);
    }

    /// <summary>Sets the dash pattern (<c>d</c>).</summary>
    /// <param name="dashes">The dash and gap lengths; empty for a solid line.</param>
    /// <param name="phase">The distance into the pattern to start.</param>
    public void SetDash(scoped ReadOnlySpan<float> dashes, float phase)
    {
        _buffer.WriteByte((byte)'[');
        for (var i = 0; i < dashes.Length; i++)
        {
            if (i > 0)
            {
                _buffer.WriteByte((byte)' ');
            }

            PdfSyntax.WriteNumber(ref _buffer, dashes[i]);
        }

        _buffer.Write("] "u8);
        Operand(phase);
        Operator("d"u8);
    }

    /// <summary>Sets the rendering intent (<c>ri</c>).</summary>
    /// <param name="intent">The intent name, such as <c>Perceptual</c>.</param>
    public void SetRenderingIntent(ReadOnlySpan<byte> intent)
    {
        NameOperand(intent);
        Operator("ri"u8);
    }

    /// <summary>Sets the flatness tolerance (<c>i</c>).</summary>
    /// <param name="flatness">The tolerance, 0 to 100.</param>
    public void SetFlatness(float flatness)
    {
        Operand(flatness);
        Operator("i"u8);
    }

    /// <summary>Applies a named graphics state parameter dictionary (<c>gs</c>).</summary>
    /// <param name="name">The name in the resources' /ExtGState.</param>
    public void SetGraphicsState(ReadOnlySpan<byte> name)
    {
        NameOperand(name);
        Operator("gs"u8);
    }

    /// <summary>Starts a new subpath (<c>m</c>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void MoveTo(float x, float y)
    {
        Operands(x, y);
        Operator("m"u8);
    }

    /// <summary>Appends a straight line (<c>l</c>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void LineTo(float x, float y)
    {
        Operands(x, y);
        Operator("l"u8);
    }

    /// <summary>Appends a cubic Bézier curve with two control points (<c>c</c>).</summary>
    /// <param name="x1">The first control point's x.</param>
    /// <param name="y1">The first control point's y.</param>
    /// <param name="x2">The second control point's x.</param>
    /// <param name="y2">The second control point's y.</param>
    /// <param name="x3">The end point's x.</param>
    /// <param name="y3">The end point's y.</param>
    public void CurveTo(float x1, float y1, float x2, float y2, float x3, float y3)
    {
        Operands(x1, y1, x2, y2);
        Operands(x3, y3);
        Operator("c"u8);
    }

    /// <summary>Appends a curve whose first control point is the current point (<c>v</c>).</summary>
    /// <param name="x2">The second control point's x.</param>
    /// <param name="y2">The second control point's y.</param>
    /// <param name="x3">The end point's x.</param>
    /// <param name="y3">The end point's y.</param>
    public void CurveToFromCurrent(float x2, float y2, float x3, float y3)
    {
        Operands(x2, y2, x3, y3);
        Operator("v"u8);
    }

    /// <summary>Appends a curve whose second control point is the end point (<c>y</c>).</summary>
    /// <param name="x1">The first control point's x.</param>
    /// <param name="y1">The first control point's y.</param>
    /// <param name="x3">The end point's x.</param>
    /// <param name="y3">The end point's y.</param>
    public void CurveToEnd(float x1, float y1, float x3, float y3)
    {
        Operands(x1, y1, x3, y3);
        Operator("y"u8);
    }

    /// <summary>Closes the subpath (<c>h</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClosePath() => Operator("h"u8);

    /// <summary>Appends a rectangle (<c>re</c>).</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void Rectangle(float x, float y, float width, float height)
    {
        Operands(x, y, width, height);
        Operator("re"u8);
    }

    /// <summary>Strokes the path (<c>S</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stroke() => Operator("S"u8);

    /// <summary>Closes and strokes the path (<c>s</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseAndStroke() => Operator("s"u8);

    /// <summary>Fills the path with the nonzero winding rule (<c>f</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill() => Operator("f"u8);

    /// <summary>Fills the path with the even-odd rule (<c>f*</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillEvenOdd() => Operator("f*"u8);

    /// <summary>Fills and strokes the path (<c>B</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillAndStroke() => Operator("B"u8);

    /// <summary>Fills with the even-odd rule and strokes the path (<c>B*</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillEvenOddAndStroke() => Operator("B*"u8);

    /// <summary>Closes, fills and strokes the path (<c>b</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseFillAndStroke() => Operator("b"u8);

    /// <summary>Closes, fills with the even-odd rule and strokes the path (<c>b*</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CloseFillEvenOddAndStroke() => Operator("b*"u8);

    /// <summary>Ends the path without painting it (<c>n</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndPath() => Operator("n"u8);

    /// <summary>Intersects the clip with the path, nonzero rule (<c>W</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clip() => Operator("W"u8);

    /// <summary>Intersects the clip with the path, even-odd rule (<c>W*</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClipEvenOdd() => Operator("W*"u8);

    /// <summary>Sets a grey fill colour (<c>g</c>).</summary>
    /// <param name="gray">The grey level, 0 black to 1 white.</param>
    public void SetFillGray(float gray)
    {
        Operand(gray);
        Operator("g"u8);
    }

    /// <summary>Sets a grey stroke colour (<c>G</c>).</summary>
    /// <param name="gray">The grey level, 0 black to 1 white.</param>
    public void SetStrokeGray(float gray)
    {
        Operand(gray);
        Operator("G"u8);
    }

    /// <summary>Sets an RGB fill colour (<c>rg</c>).</summary>
    /// <param name="red">The red component, 0 to 1.</param>
    /// <param name="green">The green component, 0 to 1.</param>
    /// <param name="blue">The blue component, 0 to 1.</param>
    public void SetFillRgb(float red, float green, float blue)
    {
        Operands(red, green);
        Operand(blue);
        Operator("rg"u8);
    }

    /// <summary>Sets an RGB stroke colour (<c>RG</c>).</summary>
    /// <param name="red">The red component, 0 to 1.</param>
    /// <param name="green">The green component, 0 to 1.</param>
    /// <param name="blue">The blue component, 0 to 1.</param>
    public void SetStrokeRgb(float red, float green, float blue)
    {
        Operands(red, green);
        Operand(blue);
        Operator("RG"u8);
    }

    /// <summary>Sets a CMYK fill colour (<c>k</c>).</summary>
    /// <param name="cyan">The cyan component, 0 to 1.</param>
    /// <param name="magenta">The magenta component, 0 to 1.</param>
    /// <param name="yellow">The yellow component, 0 to 1.</param>
    /// <param name="black">The black component, 0 to 1.</param>
    public void SetFillCmyk(float cyan, float magenta, float yellow, float black)
    {
        Operands(cyan, magenta, yellow, black);
        Operator("k"u8);
    }

    /// <summary>Sets a CMYK stroke colour (<c>K</c>).</summary>
    /// <param name="cyan">The cyan component, 0 to 1.</param>
    /// <param name="magenta">The magenta component, 0 to 1.</param>
    /// <param name="yellow">The yellow component, 0 to 1.</param>
    /// <param name="black">The black component, 0 to 1.</param>
    public void SetStrokeCmyk(float cyan, float magenta, float yellow, float black)
    {
        Operands(cyan, magenta, yellow, black);
        Operator("K"u8);
    }

    /// <summary>Sets the fill colour space (<c>cs</c>).</summary>
    /// <param name="colorSpace">A device colour space name, or a name in the resources' /ColorSpace.</param>
    public void SetFillColorSpace(ReadOnlySpan<byte> colorSpace)
    {
        NameOperand(colorSpace);
        Operator("cs"u8);
    }

    /// <summary>Sets the stroke colour space (<c>CS</c>).</summary>
    /// <param name="colorSpace">A device colour space name, or a name in the resources' /ColorSpace.</param>
    public void SetStrokeColorSpace(ReadOnlySpan<byte> colorSpace)
    {
        NameOperand(colorSpace);
        Operator("CS"u8);
    }

    /// <summary>Sets the fill colour in the current colour space (<c>sc</c>).</summary>
    /// <param name="components">The colour components.</param>
    public void SetFillColor(scoped ReadOnlySpan<float> components)
    {
        Operands(components);
        Operator("sc"u8);
    }

    /// <summary>Sets the stroke colour in the current colour space (<c>SC</c>).</summary>
    /// <param name="components">The colour components.</param>
    public void SetStrokeColor(scoped ReadOnlySpan<float> components)
    {
        Operands(components);
        Operator("SC"u8);
    }

    /// <summary>Sets the fill colour in any colour space (<c>scn</c>).</summary>
    /// <param name="components">The colour components.</param>
    public void SetFillColorN(scoped ReadOnlySpan<float> components)
    {
        Operands(components);
        Operator("scn"u8);
    }

    /// <summary>Sets the fill to a pattern (<c>scn</c>).</summary>
    /// <param name="components">The colour components for an uncoloured pattern, or empty.</param>
    /// <param name="pattern">The pattern name in the resources' /Pattern.</param>
    public void SetFillColorN(scoped ReadOnlySpan<float> components, ReadOnlySpan<byte> pattern)
    {
        Operands(components);
        NameOperand(pattern);
        Operator("scn"u8);
    }

    /// <summary>Sets the stroke colour in any colour space (<c>SCN</c>).</summary>
    /// <param name="components">The colour components.</param>
    public void SetStrokeColorN(scoped ReadOnlySpan<float> components)
    {
        Operands(components);
        Operator("SCN"u8);
    }

    /// <summary>Sets the stroke to a pattern (<c>SCN</c>).</summary>
    /// <param name="components">The colour components for an uncoloured pattern, or empty.</param>
    /// <param name="pattern">The pattern name in the resources' /Pattern.</param>
    public void SetStrokeColorN(scoped ReadOnlySpan<float> components, ReadOnlySpan<byte> pattern)
    {
        Operands(components);
        NameOperand(pattern);
        Operator("SCN"u8);
    }

    /// <summary>Begins a text object (<c>BT</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginText() => Operator("BT"u8);

    /// <summary>Ends a text object (<c>ET</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndText() => Operator("ET"u8);

    /// <summary>Sets the font and size (<c>Tf</c>).</summary>
    /// <param name="font">The font name in the resources' /Font.</param>
    /// <param name="size">The size.</param>
    public void SetFont(scoped ReadOnlySpan<byte> font, float size)
    {
        NameOperand(font);
        Operand(size);
        Operator("Tf"u8);
    }

    /// <summary>Moves to the start of the next line, offset from the start of this one (<c>Td</c>).</summary>
    /// <param name="x">The horizontal offset.</param>
    /// <param name="y">The vertical offset.</param>
    public void MoveText(float x, float y)
    {
        Operands(x, y);
        Operator("Td"u8);
    }

    /// <summary>Moves to the next line and sets the leading to minus the vertical offset (<c>TD</c>).</summary>
    /// <param name="x">The horizontal offset.</param>
    /// <param name="y">The vertical offset.</param>
    public void MoveTextAndSetLeading(float x, float y)
    {
        Operands(x, y);
        Operator("TD"u8);
    }

    /// <summary>Sets the text matrix and text line matrix (<c>Tm</c>).</summary>
    /// <param name="a">The a entry.</param>
    /// <param name="b">The b entry.</param>
    /// <param name="c">The c entry.</param>
    /// <param name="d">The d entry.</param>
    /// <param name="e">The horizontal translation.</param>
    /// <param name="f">The vertical translation.</param>
    public void SetTextMatrix(float a, float b, float c, float d, float e, float f)
    {
        Operands(a, b, c, d);
        Operands(e, f);
        Operator("Tm"u8);
    }

    /// <summary>Moves to the start of the next line (<c>T*</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void NextLine() => Operator("T*"u8);

    /// <summary>Sets the character spacing (<c>Tc</c>).</summary>
    /// <param name="spacing">The spacing in unscaled text units.</param>
    public void SetCharacterSpacing(float spacing)
    {
        Operand(spacing);
        Operator("Tc"u8);
    }

    /// <summary>Sets the word spacing (<c>Tw</c>).</summary>
    /// <param name="spacing">The spacing in unscaled text units.</param>
    public void SetWordSpacing(float spacing)
    {
        Operand(spacing);
        Operator("Tw"u8);
    }

    /// <summary>Sets the horizontal scaling (<c>Tz</c>).</summary>
    /// <param name="percent">The scale in percent; 100 is normal.</param>
    public void SetHorizontalScaling(float percent)
    {
        Operand(percent);
        Operator("Tz"u8);
    }

    /// <summary>Sets the leading (<c>TL</c>).</summary>
    /// <param name="leading">The distance between baselines.</param>
    public void SetLeading(float leading)
    {
        Operand(leading);
        Operator("TL"u8);
    }

    /// <summary>Sets the text rendering mode (<c>Tr</c>).</summary>
    /// <param name="mode">The mode, 0 to 7; 3 is invisible.</param>
    public void SetTextRenderingMode(int mode)
    {
        Operand(mode);
        Operator("Tr"u8);
    }

    /// <summary>Sets the text rise (<c>Ts</c>).</summary>
    /// <param name="rise">The rise.</param>
    public void SetTextRise(float rise)
    {
        Operand(rise);
        Operator("Ts"u8);
    }

    /// <summary>Shows a string as a literal string (<c>Tj</c>).</summary>
    /// <param name="text">The string's bytes, already encoded for the font.</param>
    public void ShowText(ReadOnlySpan<byte> text)
    {
        PdfSyntax.WriteLiteralString(ref _buffer, text);
        _buffer.WriteByte((byte)' ');
        Operator("Tj"u8);
    }

    /// <summary>Starts a <c>TJ</c> array of strings and position adjustments.</summary>
    public void BeginTextArray()
    {
        _buffer.WriteByte((byte)'[');
        _arrayItems = 0;
    }

    /// <summary>Adds a string to the open <c>TJ</c> array.</summary>
    /// <param name="text">The string's bytes, already encoded for the font.</param>
    public void AddTextArrayString(ReadOnlySpan<byte> text)
    {
        ArraySeparator();
        PdfSyntax.WriteLiteralString(ref _buffer, text);
    }

    /// <summary>Adds a position adjustment to the open <c>TJ</c> array.</summary>
    /// <param name="adjustment">The adjustment in thousandths of a text unit; positive moves left.</param>
    public void AddTextArrayAdjustment(float adjustment)
    {
        ArraySeparator();
        PdfSyntax.WriteNumber(ref _buffer, adjustment);
    }

    /// <summary>Closes the <c>TJ</c> array and shows it.</summary>
    public void EndTextArray()
    {
        _buffer.Write("] "u8);
        Operator("TJ"u8);
    }

    /// <summary>Paints an XObject (<c>Do</c>).</summary>
    /// <param name="name">The name in the resources' /XObject.</param>
    public void DrawXObject(ReadOnlySpan<byte> name)
    {
        NameOperand(name);
        Operator("Do"u8);
    }

    /// <summary>Paints a shading over the current clip (<c>sh</c>).</summary>
    /// <param name="name">The name in the resources' /Shading.</param>
    public void PaintShading(ReadOnlySpan<byte> name)
    {
        NameOperand(name);
        Operator("sh"u8);
    }

    /// <summary>Begins a marked-content sequence (<c>BMC</c>).</summary>
    /// <param name="tag">The tag.</param>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag)
    {
        NameOperand(tag);
        Operator("BMC"u8);
    }

    /// <summary>Begins a marked-content sequence with a named property list (<c>BDC</c>).</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="properties">The property list's name in the resources' /Properties.</param>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, ReadOnlySpan<byte> properties)
    {
        NameOperand(tag);
        NameOperand(properties);
        Operator("BDC"u8);
    }

    /// <summary>Begins a marked-content sequence that has a marked content identifier (<c>BDC</c> with <c>/MCID</c>, ISO 32000-2 14.7.4.2).</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="identifier">The marked content identifier, unique on the page.</param>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, int identifier)
    {
        NameOperand(tag);
        _buffer.Write("<</MCID "u8);
        PdfSyntax.WriteInteger(ref _buffer, identifier);
        _buffer.Write(">> "u8);
        Operator("BDC"u8);
    }

    /// <summary>Begins a marked-content sequence with an inline property list (<c>BDC</c>).</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="properties">The property list, written inline; it must not hold references.</param>
    /// <param name="names">The name table the property list's names come from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The property list nests too deeply.</exception>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary properties, PdfNameTable names)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(names);
        NameOperand(tag);
        var writer = new PdfObjectWriter(names);
        _buffer.Swap(ref writer.Buffer);
        try
        {
            writer.WriteValue(PdfValue.FromDictionary(properties));
        }
        finally
        {
            _buffer.Swap(ref writer.Buffer);
            writer.Dispose();
        }

        _buffer.WriteByte((byte)' ');
        Operator("BDC"u8);
    }

    /// <summary>Ends a marked-content sequence (<c>EMC</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndMarkedContent() => Operator("EMC"u8);

    /// <summary>Appends content written elsewhere, such as operators this builder has no method for.</summary>
    /// <param name="content">The content bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<byte> content) => _buffer.Write(content);

    /// <summary>Clears the content so the builder can be reused.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _buffer.Length = 0;

    /// <summary>Copies the content into a new array.</summary>
    /// <returns>The content.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToArray() => _buffer.ToArray();

    /// <summary>Copies the content to a buffer writer.</summary>
    /// <param name="destination">The destination.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public readonly void WriteTo(IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(_buffer.WrittenSpan);
    }

    /// <summary>Copies the content to a pooled buffer.</summary>
    /// <param name="destination">The destination.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void WriteTo(ref PooledBuffer destination) => destination.Write(_buffer.WrittenSpan);

    /// <summary>Creates a Form XObject holding the content, Flate-compressed when that is smaller.</summary>
    /// <param name="owner">The document the stream belongs to, or <see langword="null"/>.</param>
    /// <param name="boundingBox">The form's /BBox.</param>
    /// <param name="resources">The form's /Resources, or <see langword="null"/>.</param>
    /// <returns>The stream, ready to add as an indirect object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PdfStream ToFormXObject(PdfObjectStore? owner, PdfRectangle boundingBox, PdfDictionary? resources) =>
        ToFormXObject(owner, boundingBox, [], resources);

    /// <summary>Creates a Form XObject holding the content, Flate-compressed when that is smaller.</summary>
    /// <param name="owner">The document the stream belongs to, or <see langword="null"/>.</param>
    /// <param name="boundingBox">The form's /BBox.</param>
    /// <param name="matrix">The form's six-entry /Matrix, or empty for the identity.</param>
    /// <param name="resources">The form's /Resources, or <see langword="null"/>.</param>
    /// <returns>The stream, ready to add as an indirect object.</returns>
    /// <exception cref="ArgumentException"><paramref name="matrix"/> is neither empty nor six entries.</exception>
    public readonly PdfStream ToFormXObject(PdfObjectStore? owner, PdfRectangle boundingBox, ReadOnlySpan<float> matrix, PdfDictionary? resources)
    {
        if (!matrix.IsEmpty && matrix.Length != MatrixLength)
        {
            throw new ArgumentException("A matrix has six entries.", nameof(matrix));
        }

        var dictionary = new PdfDictionary(owner);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        dictionary.Set(KnownName.BBox, PdfValue.FromArray(boundingBox.ToArray(owner)));
        if (!matrix.IsEmpty)
        {
            dictionary.Set(KnownName.Matrix, PdfValue.FromArray(PdfArray.FromNumbers(owner, matrix)));
        }

        if (resources is not null)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return new(dictionary, CompressIfSmaller(_buffer.WrittenSpan, dictionary));
    }

    /// <summary>Returns the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _buffer.Dispose();

    /// <summary>Flate-compresses data when that makes it smaller, marking the dictionary's /Filter.</summary>
    /// <param name="data">The data.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns>The bytes to store.</returns>
    private static byte[] CompressIfSmaller(ReadOnlySpan<byte> data, PdfDictionary dictionary)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            if (compressed.Length >= data.Length)
            {
                return data.ToArray();
            }

            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return compressed.ToArray();
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Writes a number operand and a space.</summary>
    /// <param name="value">The number.</param>
    private void Operand(double value)
    {
        PdfSyntax.WriteNumber(ref _buffer, value);
        _buffer.WriteByte((byte)' ');
    }

    /// <summary>Writes two number operands.</summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    private void Operands(float first, float second)
    {
        Operand(first);
        Operand(second);
    }

    /// <summary>Writes four number operands.</summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <param name="third">The third.</param>
    /// <param name="fourth">The fourth.</param>
    private void Operands(float first, float second, float third, float fourth)
    {
        Operands(first, second);
        Operands(third, fourth);
    }

    /// <summary>Writes number operands.</summary>
    /// <param name="values">The numbers.</param>
    private void Operands(scoped ReadOnlySpan<float> values)
    {
        foreach (var value in values)
        {
            Operand(value);
        }
    }

    /// <summary>Writes a name operand and a space.</summary>
    /// <param name="name">The name, unescaped and without the slash.</param>
    private void NameOperand(scoped ReadOnlySpan<byte> name)
    {
        PdfSyntax.WriteName(ref _buffer, name);
        _buffer.WriteByte((byte)' ');
    }

    /// <summary>Writes an operator and ends the line.</summary>
    /// <param name="name">The operator.</param>
    private void Operator(ReadOnlySpan<byte> name)
    {
        _buffer.Write(name);
        _buffer.WriteByte((byte)'\n');
    }

    /// <summary>Separates items in the open <c>TJ</c> array.</summary>
    private void ArraySeparator()
    {
        if (_arrayItems > 0)
        {
            _buffer.WriteByte((byte)' ');
        }

        _arrayItems++;
    }
}
