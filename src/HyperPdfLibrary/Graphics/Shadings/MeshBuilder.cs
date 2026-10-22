// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>Decodes the data of shading types 4 to 7 into triangles.</summary>
internal sealed partial class MeshBuilder
{
    /// <summary>The most vertices in one triangle chunk; a multiple of three below the 16-bit index limit.</summary>
    private const int ChunkVertices = 60_000;

    /// <summary>The width of the colour ramp, in pixels.</summary>
    private const int RampWidth = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The most bits a field has.</summary>
    private const int MaxFieldBits = 32;

    /// <summary>The numbers before the colour ranges in /Decode.</summary>
    private const int CoordinateDecode = 4;

    /// <summary>The vertices in a triangle.</summary>
    private const int TriangleVertices = 3;

    /// <summary>The vertices two triangles hold, which is what a cell between two lattice rows makes.</summary>
    private const int CellTriangleVertices = 6;

    /// <summary>The most vertices the lists are sized for ahead of reading; a larger shading grows them as it is read.</summary>
    private const int MaxReserved = 1 << 24;

    /// <summary>The rows a lattice needs before it has a cell.</summary>
    private const int MinLatticeRows = 2;

    /// <summary>The vertices a lattice row has at least.</summary>
    private const int MinRowVertices = 2;

    /// <summary>The share of a texel from its edge to its centre.</summary>
    private const float TexelCentre = 0.5F;

    /// <summary>The coordinates in a point.</summary>
    private const int PointFields = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The smallest decode range treated as a real range.</summary>
    private const float MinRange = 1e-9F;

    /// <summary>The shading.</summary>
    private readonly PdfShading _shading;

    /// <summary>The positions of the triangle vertices.</summary>
    private readonly List<PdfPoint> _positions = [];

    /// <summary>The vertex colours, when the shading has no function.</summary>
    private readonly List<PdfColor> _colors = [];

    /// <summary>The ramp texture coordinates, when the shading has a function.</summary>
    private readonly List<PdfPoint> _texels = [];

    /// <summary>The /Decode array.</summary>
    private readonly float[] _decode;

    /// <summary>The bits of a coordinate.</summary>
    private readonly int _coordinateBits;

    /// <summary>The bits of a colour component.</summary>
    private readonly int _componentBits;

    /// <summary>The bits of an edge flag.</summary>
    private readonly int _flagBits;

    /// <summary>The colour components in each vertex: one when a function maps them.</summary>
    private readonly int _componentCount;

    /// <summary>The device units one unit of shading space spans.</summary>
    private readonly float _scale;

    /// <summary>Initializes a new instance of the <see cref="MeshBuilder"/> class.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="decode">The /Decode array.</param>
    /// <param name="scale">The device units one unit of shading space spans, which sets how finely patches are cut.</param>
    private MeshBuilder(PdfShading shading, float[] decode, float scale)
    {
        _shading = shading;
        _decode = decode;
        _scale = scale;
        var dictionary = shading.Dictionary;
        _coordinateBits = Math.Clamp(dictionary.GetInt32(KnownName.BitsPerCoordinate), 1, MaxFieldBits);
        _componentBits = Math.Clamp(dictionary.GetInt32(KnownName.BitsPerComponent), 1, MaxFieldBits);
        _flagBits = Math.Clamp(dictionary.GetInt32(KnownName.BitsPerFlag, ByteBits), 0, MaxFieldBits);
        _componentCount = shading.Function is null ? shading.ColorSpace.Components : 1;
    }

    /// <summary>Gets a value indicating whether vertices carry a function input instead of a colour.</summary>
    private bool UsesRamp => _shading.Function is not null;

    /// <summary>Decodes a mesh shading.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="scale">The device units one unit of shading space spans, which sets how finely patches are cut.</param>
    /// <returns>The mesh, or null when it has no triangles or is damaged.</returns>
    internal static ShadingMesh? Build(PdfShading shading, float scale)
    {
        var decodeArray = shading.Dictionary.GetArray(KnownName.Decode);
        var components = shading.Function is null ? shading.ColorSpace.Components : 1;
        if (decodeArray is null || decodeArray.Count < CoordinateDecode + (components * PdfColorSpace.PairSize) || shading.Stream is null)
        {
            return null;
        }

        var decode = new float[decodeArray.Count];
        _ = decodeArray.ReadNumbers(decode);
        var builder = new MeshBuilder(shading, decode, scale);
        var data = default(PooledBuffer);
        try
        {
            _ = shading.Stream.Decode(ref data);
            builder.Read(data.WrittenSpan);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        finally
        {
            data.Dispose();
        }

        return builder.ToMesh();
    }

    /// <summary>Appends one triangle.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    internal void AddTriangle(in MeshVertex a, in MeshVertex b, in MeshVertex c)
    {
        Add(a);
        Add(b);
        Add(c);
    }

    /// <summary>Counts the vertices the triangles of a type 5 shading hold: two triangles for each cell between rows.</summary>
    /// <param name="scan">A copy of the reader at the start of the data.</param>
    /// <param name="perRow">The vertices in a row.</param>
    /// <param name="vertexBits">The bits of one vertex.</param>
    /// <returns>The vertices.</returns>
    private static int CountLatticeEntries(MeshBitReader scan, int perRow, int vertexBits)
    {
        var rowBits = (long)perRow * ((vertexBits + ByteBits - 1) / ByteBits * ByteBits);
        long rows = 0;
        while (scan.Remaining >= (long)vertexBits * perRow)
        {
            scan.Skip(rowBits);
            rows++;
        }

        return rows < MinLatticeRows ? 0 : (int)Math.Min(CellTriangleVertices * (rows - 1) * (perRow - 1), MaxReserved);
    }

    /// <summary>Maps a raw field to its range.</summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="bits">The field width.</param>
    /// <param name="low">The value of zero.</param>
    /// <param name="high">The value of the largest raw number.</param>
    /// <returns>The decoded value.</returns>
    private static float Decode(uint raw, int bits, float low, float high)
    {
        var max = bits >= MaxFieldBits ? uint.MaxValue : (1U << bits) - 1;
        return low + (raw * (high - low) / max);
    }

    /// <summary>Reads one coordinate.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="axis">0 for x, 1 for y.</param>
    /// <returns>The coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ReadCoordinate(ref MeshBitReader reader, int axis) =>
        Decode(reader.Read(_coordinateBits), _coordinateBits, _decode[axis * PdfColorSpace.PairSize], _decode[(axis * PdfColorSpace.PairSize) + 1]);

    /// <summary>Reads the vertices of the shading according to its type.</summary>
    /// <param name="data">The decoded stream.</param>
    private void Read(ReadOnlySpan<byte> data)
    {
        var reader = new MeshBitReader(data);
        switch (_shading.Kind)
        {
            case PdfShadingKind.FreeForm:
            {
                ReadFreeForm(ref reader);
                break;
            }

            case PdfShadingKind.Lattice:
            {
                ReadLattice(ref reader);
                break;
            }

            default:
            {
                ReadPatches(ref reader, _shading.Kind == PdfShadingKind.Tensor);
                break;
            }
        }
    }

    /// <summary>Reads a type 4 shading.</summary>
    /// <param name="reader">The reader.</param>
    private void ReadFreeForm(ref MeshBitReader reader)
    {
        var vertexBits = _flagBits + (PointFields * _coordinateBits) + (_componentCount * _componentBits);
        Reserve(CountFreeFormEntries(reader, vertexBits));
        MeshVertex a = default;
        MeshVertex b = default;
        MeshVertex c = default;
        var started = false;
        while (reader.Remaining >= vertexBits)
        {
            var flag = reader.Read(_flagBits);
            var vertex = ReadVertex(ref reader);
            if (flag == 0 || !started)
            {
                if (reader.Remaining < (PointFields * vertexBits))
                {
                    return;
                }

                started = true;
                _ = reader.Read(_flagBits);
                b = ReadVertex(ref reader);
                _ = reader.Read(_flagBits);
                c = ReadVertex(ref reader);
                a = vertex;
            }
            else if (flag == 1)
            {
                a = b;
                b = c;
                c = vertex;
            }
            else
            {
                b = c;
                c = vertex;
            }

            AddTriangle(a, b, c);
        }
    }

    /// <summary>Reads a type 5 shading.</summary>
    /// <param name="reader">The reader.</param>
    private void ReadLattice(ref MeshBitReader reader)
    {
        var perRow = _shading.Dictionary.GetInt32(KnownName.VerticesPerRow);
        if (perRow < MinRowVertices)
        {
            return;
        }

        var vertexBits = (PointFields * _coordinateBits) + (_componentCount * _componentBits);
        Reserve(CountLatticeEntries(reader, perRow, vertexBits));
        var previous = new MeshVertex[perRow];
        var current = new MeshVertex[perRow];
        var first = true;
        while (reader.Remaining >= (long)vertexBits * perRow)
        {
            for (var i = 0; i < perRow; i++)
            {
                current[i] = ReadVertex(ref reader);
            }

            if (!first)
            {
                for (var i = 0; i < perRow - 1; i++)
                {
                    AddTriangle(previous[i], previous[i + 1], current[i]);
                    AddTriangle(previous[i + 1], current[i + 1], current[i]);
                }
            }

            current.CopyTo(previous, 0);
            first = false;
        }
    }

    /// <summary>Sizes the lists for the vertices about to be read, so they are filled without growing.</summary>
    /// <param name="entries">The vertices the triangles will hold, or more than are wanted when the shading has too many to size for.</param>
    private void Reserve(int entries)
    {
        var count = Math.Min(entries, MaxReserved);
        _ = _positions.EnsureCapacity(count);
        _ = UsesRamp ? _texels.EnsureCapacity(count) : _colors.EnsureCapacity(count);
    }

    /// <summary>Counts the vertices the triangles of a type 4 shading hold, by walking the data the way the reader will.</summary>
    /// <param name="scan">A copy of the reader at the start of the data.</param>
    /// <param name="vertexBits">The bits of one vertex, with its flag.</param>
    /// <returns>The vertices.</returns>
    private int CountFreeFormEntries(MeshBitReader scan, int vertexBits)
    {
        var recordBits = (vertexBits + ByteBits - 1) / ByteBits * ByteBits;
        var entries = 0;
        var started = false;
        while (scan.Remaining >= vertexBits)
        {
            var flag = scan.Read(_flagBits);
            scan.Skip(recordBits - _flagBits);
            if (flag == 0 || !started)
            {
                if (scan.Remaining < (PointFields * vertexBits))
                {
                    break;
                }

                started = true;
                scan.Skip((long)PointFields * recordBits);
            }

            entries += TriangleVertices;
        }

        return entries;
    }

    /// <summary>Reads a position and colour, then skips to the next byte.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The vertex.</returns>
    private MeshVertex ReadVertex(ref MeshBitReader reader)
    {
        var x = ReadCoordinate(ref reader, 0);
        var y = ReadCoordinate(ref reader, 1);
        var vertex = ReadColor(ref reader, new(x, y));
        reader.Align();
        return vertex;
    }

    /// <summary>Reads a point.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The point.</returns>
    private PdfPoint ReadPoint(ref MeshBitReader reader)
    {
        var x = ReadCoordinate(ref reader, 0);
        var y = ReadCoordinate(ref reader, 1);
        return new(x, y);
    }

    /// <summary>Reads the colour components of a vertex at a position.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="point">The position.</param>
    /// <returns>The vertex.</returns>
    private MeshVertex ReadColor(ref MeshBitReader reader, PdfPoint point)
    {
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        var count = Math.Min(_componentCount, components.Length);
        for (var i = 0; i < _componentCount; i++)
        {
            var low = _decode[CoordinateDecode + (i * PdfColorSpace.PairSize)];
            var high = _decode[CoordinateDecode + (i * PdfColorSpace.PairSize) + 1];
            var value = Decode(reader.Read(_componentBits), _componentBits, low, high);
            if (i < count)
            {
                components[i] = value;
            }
        }

        return UsesRamp ? new(point, default, components[0]) : new(point, PdfShading.ToColor(_shading.ColorSpace, components), 0);
    }

    /// <summary>Appends a vertex to the lists.</summary>
    /// <param name="vertex">The vertex.</param>
    private void Add(in MeshVertex vertex)
    {
        _positions.Add(vertex.Point);
        if (!UsesRamp)
        {
            _colors.Add(vertex.Color);
            return;
        }

        var low = _decode[CoordinateDecode];
        var high = _decode[CoordinateDecode + 1];
        var share = MathF.Abs(high - low) < MinRange ? 0 : (vertex.T - low) / (high - low);
        _texels.Add(new((Math.Clamp(share, 0, 1) * (RampWidth - 1)) + TexelCentre, TexelCentre));
    }

    /// <summary>Packs the lists into backend triangle resources.</summary>
    /// <returns>The mesh, or null when it is empty.</returns>
    private ShadingMesh? ToMesh()
    {
        var total = _positions.Count - (_positions.Count % TriangleVertices);
        if (total == 0)
        {
            return null;
        }

        var chunks = new List<IPdfRenderVertices>();
        var backend = PdfDrawingServices.For(_shading.Dictionary).Backend;
        for (var start = 0; start < total; start += ChunkVertices)
        {
            var count = Math.Min(ChunkVertices, total - start);

            // A slice of the list's span is copied once; GetRange would build a second list first.
            var positions = CollectionsMarshal.AsSpan(_positions).Slice(start, count).ToArray();
            chunks.Add(UsesRamp
                ? backend.CreateVertices(positions, CollectionsMarshal.AsSpan(_texels).Slice(start, count).ToArray(), null!)
                : backend.CreateVertices(positions, CollectionsMarshal.AsSpan(_colors).Slice(start, count).ToArray()));
        }

        return new([..chunks], UsesRamp ? CreateRamp() : null);
    }

    /// <summary>Builds the shader that turns texture coordinates into colours.</summary>
    /// <returns>The shader.</returns>
    private IPdfRenderShader CreateRamp()
    {
        var colors = new PdfColor[RampWidth];
        ShadingShaders.SampleColors(_shading, _decode[CoordinateDecode], _decode[CoordinateDecode + 1], colors);
        var pixels = new byte[RampWidth * BytesPerPixel];
        for (var i = 0; i < colors.Length; i++)
        {
            pixels[i * BytesPerPixel] = colors[i].Blue;
            pixels[(i * BytesPerPixel) + 1] = colors[i].Green;
            pixels[(i * BytesPerPixel) + RedOffset] = colors[i].Red;
            pixels[(i * BytesPerPixel) + AlphaOffset] = byte.MaxValue;
        }

        var backend = PdfDrawingServices.For(_shading.Dictionary).Backend;
        using var image = backend.CreateImage(new(RampWidth, 1, PdfImagePixelFormat.Bgra8888), pixels, RampWidth * BytesPerPixel);
        return backend.CreateImageShader(image, PdfShaderTileMode.Clamp, PdfShaderTileMode.Clamp, true, System.Numerics.Matrix3x2.Identity);
    }
}
