// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes whole U3D files for tests from the block layouts of ECMA-363 (fourth edition, clauses 9 and 11): nodes with transforms,
/// base meshes, free-form surfaces, materials, shaders, lights, views, textures and meta data. A writer made for an uncompressed
/// file writes plain values; any other writes the arithmetic code of clause 10.4. The files come from this helper and the standard,
/// not from other software, so they check that the library follows the written layout.
/// </summary>
/// <param name="compressed"><see langword="true"/> to write the arithmetic code; <see langword="false"/> to write plain values and set the no-compression profile bit.</param>
public sealed class U3dFileWriter(bool compressed)
{
    /// <summary>The block type of the file header.</summary>
    private const uint HeaderType = 0x00443355;

    /// <summary>The block type of a modifier chain.</summary>
    private const uint ChainType = 0xFFFFFF14;

    /// <summary>The block type of a New Object Type block.</summary>
    private const uint NewObjectTypeType = 0xFFFFFF16;

    /// <summary>The profile flag for a file whose values are not compressed.</summary>
    private const uint NoCompressionProfile = 4;

    /// <summary>The profile flag for the extensible profile.</summary>
    private const uint ExtensibleProfile = 2;

    /// <summary>The bytes of a block's type, data size and meta data size.</summary>
    private const int FrameBytes = 12;

    /// <summary>The position of the data size in a block's frame.</summary>
    private const int DataSizeOffset = 4;

    /// <summary>The position of the meta data size in a block's frame.</summary>
    private const int MetaSizeOffset = 8;

    /// <summary>The view node attributes of a three-point perspective view.</summary>
    private const uint PerspectiveAttributes = 0;

    /// <summary>The view node attributes of an orthographic view.</summary>
    private const uint OrthographicAttributes = 2;

    /// <summary>The view node attributes of a one-point perspective view.</summary>
    private const uint OnePointAttributes = 6;

    /// <summary>The near clipping distance of a view that does not give one.</summary>
    private const float DefaultNear = 0.1F;

    /// <summary>The far clipping distance of a view that does not give one.</summary>
    private const float DefaultFar = 100F;

    /// <summary>The grey of a view resource's fog.</summary>
    private const float FogGrey = 0.5F;

    /// <summary>Where a view resource's fog is densest.</summary>
    private const float FogFar = 10F;

    /// <summary>The quality factors of a mesh declaration.</summary>
    private const int QualityFactors = 3;

    /// <summary>The inverse quantization factors of a mesh declaration.</summary>
    private const int InverseQuantizations = 5;

    /// <summary>The corners of a face.</summary>
    private const int Corners = 3;

    /// <summary>The alignment of a block's sections.</summary>
    private const int Alignment = 4;

    /// <summary>The character encoding number for UTF-8.</summary>
    private const uint Utf8 = 106;

    /// <summary>The bytes of the file size field.</summary>
    private const int FileSizeBytes = 8;

    /// <summary>The position of the file size field in the file.</summary>
    private const int FileSizeOffset = FrameBytes + 12;

    /// <summary>The block type the writer gives free-form object declarations.</summary>
    private const uint FreeFormDeclarationType = 0x00000101;

    /// <summary>The block type the writer gives free-form object continuations.</summary>
    private const uint FreeFormContinuationType = 0x00000102;

    /// <summary>The attribute flag for a Bezier surface.</summary>
    private const uint BezierFlag = 2;

    /// <summary>The attribute flag for a non-rational surface.</summary>
    private const uint NonRationalFlag = 1;

    /// <summary>The attribute flag for uniform knots in the U direction.</summary>
    private const uint UniformUFlag = 4;

    /// <summary>The attribute flag for uniform knots in the V direction.</summary>
    private const uint UniformVFlag = 8;

    /// <summary>The attribute flag for independent knots.</summary>
    private const uint IndependentFlag = 0x10;

    /// <summary>The free-form precision flag for 64-bit knots.</summary>
    private const uint DoubleKnots = 1;

    /// <summary>The free-form precision flag for 64-bit weights.</summary>
    private const uint DoubleWeights = 2;

    /// <summary>The blocks after the file header.</summary>
    private readonly List<byte[]> _blocks = [];

    /// <summary>The adaptive counts of each object, by name.</summary>
    private readonly Dictionary<string, U3dWriterContexts> _contexts = [];

    /// <summary>Gets the block type free-form object declarations are given.</summary>
    public static uint FreeFormDeclaration { get; } = FreeFormDeclarationType;

    /// <summary>Gets the block type free-form object continuations are given.</summary>
    public static uint FreeFormContinuation { get; } = FreeFormContinuationType;

    /// <summary>Gets or sets the profile flags written besides the compression flag.</summary>
    public uint ExtraProfile { get; set; }

    /// <summary>Gets or sets the free-form precision flags: 1 for 64-bit knots, 2 for 64-bit weights.</summary>
    public uint FreeFormAttributes { get; set; }

    /// <summary>Writes a block: its type, sizes, data and meta data, each padded to four bytes.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="data">The data section.</param>
    /// <returns>The block's bytes.</returns>
    public static byte[] Block(uint type, byte[] data) => Block(type, data, []);

    /// <summary>Writes a block with meta data.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="data">The data section.</param>
    /// <param name="meta">The meta data section.</param>
    /// <returns>The block's bytes.</returns>
    public static byte[] Block(uint type, byte[] data, byte[] meta)
    {
        var dataPadded = Padded(data.Length);
        var block = new byte[FrameBytes + dataPadded + Padded(meta.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(block, type);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(DataSizeOffset), (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(MetaSizeOffset), (uint)meta.Length);
        data.CopyTo(block, FrameBytes);
        meta.CopyTo(block, FrameBytes + dataPadded);
        return block;
    }

    /// <summary>Writes a meta data section of text entries.</summary>
    /// <param name="entries">The attributes, key and value of each entry.</param>
    /// <returns>The section's bytes.</returns>
    public static byte[] Meta(params U3dMetaEntry[] entries)
    {
        var writer = new U3dValueWriter(true).U32((uint)entries.Length);
        foreach (var (attributes, key, value) in entries)
        {
            _ = writer.U32(attributes).String(key).String(value);
        }

        return writer.ToArray();
    }

    /// <summary>Adds a block after the header.</summary>
    /// <param name="block">The block's bytes.</param>
    /// <returns>This writer.</returns>
    public U3dFileWriter Add(byte[] block)
    {
        _blocks.Add(block);
        return this;
    }

    /// <summary>Writes the file.</summary>
    /// <returns>The file's bytes.</returns>
    public byte[] ToArray()
    {
        var profile = (compressed ? 0U : NoCompressionProfile) | ExtraProfile;
        var body = _blocks.SelectMany(static block => block).ToArray();
        var header = new U3dValueWriter(true).U16(0).U16(0).U32(profile).U32((uint)body.Length).U32(0).U32(0).U32(Utf8).ToArray();
        var file = Block(HeaderType, header).Concat(body).ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(FileSizeOffset, FileSizeBytes), (ulong)file.Length);
        return file;
    }

    /// <summary>Writes a group node.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] Group(string name, params U3dParent[] parents) => Block(0xFFFFFF21, Node(name, parents).ToArray());

    /// <summary>Writes a model node.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The model resource's name.</param>
    /// <param name="visibility">The visibility: 0 none, 1 front, 2 back, 3 both.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] ModelNode(string name, string resource, uint visibility, params U3dParent[] parents) =>
        Block(0xFFFFFF22, Node(name, parents).String(resource).U32(visibility).ToArray());

    /// <summary>Writes a light node.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The light resource's name.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] LightNode(string name, string resource, params U3dParent[] parents) =>
        Block(0xFFFFFF23, Node(name, parents).String(resource).ToArray());

    /// <summary>Writes a view node with a three-point perspective projection.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The view resource's name.</param>
    /// <param name="near">The near clipping distance.</param>
    /// <param name="far">The far clipping distance.</param>
    /// <param name="fieldOfView">The field of view in degrees.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] PerspectiveView(string name, string resource, float near, float far, float fieldOfView, params U3dParent[] parents) =>
        ViewNode(name, resource, PerspectiveAttributes, near, far, fieldOfView, parents);

    /// <summary>Writes a view node with an orthographic projection.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The view resource's name.</param>
    /// <param name="near">The near clipping distance.</param>
    /// <param name="far">The far clipping distance.</param>
    /// <param name="height">The height of the view.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] OrthographicView(string name, string resource, float near, float far, float height, params U3dParent[] parents) =>
        ViewNode(name, resource, OrthographicAttributes, near, far, height, parents);

    /// <summary>Writes a view node with a one-point perspective projection, which carries a projection vector.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The view resource's name.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    public byte[] OnePointView(string name, string resource, params U3dParent[] parents) =>
        ViewNode(name, resource, OnePointAttributes, DefaultNear, DefaultFar, 0, parents);

    /// <summary>Writes a light resource.</summary>
    /// <param name="name">The resource's name.</param>
    /// <param name="attributes">The flags: 1 enabled.</param>
    /// <param name="kind">The kind: 0 ambient, 1 directional, 2 point, 3 spot.</param>
    /// <param name="color">The colour.</param>
    /// <param name="intensity">The intensity.</param>
    /// <returns>The block.</returns>
    public byte[] Light(string name, uint attributes, byte kind, Vector3 color, float intensity) => Block(
        0xFFFFFF51,
        Writer().String(name).U32(attributes).U8(kind).Vector3(color).F32(1).Vector3(new(1, 0, 0)).F32(0).F32(intensity).ToArray());

    /// <summary>Writes a view resource with one pass.</summary>
    /// <param name="name">The resource's name.</param>
    /// <param name="passes">The pass count to write; the pass itself is written once, then any extra passes.</param>
    /// <returns>The block.</returns>
    public byte[] ViewResource(string name, uint passes)
    {
        var writer = Writer().String(name).U32(passes);
        for (var pass = 0; pass < passes; pass++)
        {
            _ = writer.String(string.Empty).U32(1).U32(0).Vector4(new(FogGrey, FogGrey, FogGrey, 1)).F32(1).F32(FogFar);
        }

        return Block(0xFFFFFF52, writer.ToArray());
    }

    /// <summary>Writes a material resource.</summary>
    /// <param name="name">The resource's name.</param>
    /// <param name="ambient">The ambient colour.</param>
    /// <param name="diffuse">The diffuse colour.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The block.</returns>
    public byte[] Material(string name, Vector3 ambient, Vector3 diffuse, float opacity) => Block(
        0xFFFFFF54,
        Writer().String(name).U32(0x23).Vector3(ambient).Vector3(diffuse).Vector3(Vector3.Zero).Vector3(Vector3.Zero).F32(0).F32(opacity).ToArray());

    /// <summary>Writes a lit texture shader with its texture layers.</summary>
    /// <param name="name">The shader's name.</param>
    /// <param name="attributes">The flags: 1 lighting, 4 vertex colour.</param>
    /// <param name="material">The material's name.</param>
    /// <param name="textures">The names of the textures of the active layers.</param>
    /// <returns>The block.</returns>
    public byte[] Shader(string name, uint attributes, string material, params string[] textures)
    {
        var channels = (1U << textures.Length) - 1;
        var writer = Writer().String(name).U32(attributes).F32(0).U32(0x617).U32(0x606).U32(1).U32(channels).U32(0).String(material);
        foreach (var texture in textures)
        {
            _ = writer.String(texture).F32(1).U8(0).U8(0).F32(0).U8(0).Matrix(Matrix4x4.Identity).Matrix(Matrix4x4.Identity).U8(0);
        }

        return Block(0xFFFFFF53, writer.ToArray());
    }

    /// <summary>Writes a shading modifier.</summary>
    /// <param name="chain">The name of the node chain it belongs to.</param>
    /// <param name="lists">The shader names of each shader list.</param>
    /// <returns>The block.</returns>
    public byte[] ShadingModifier(string chain, params string[][] lists)
    {
        var writer = Writer().String(chain).U32(1).U32(1).U32((uint)lists.Length);
        foreach (var list in lists)
        {
            _ = writer.U32((uint)list.Length);
            foreach (var name in list)
            {
                _ = writer.String(name);
            }
        }

        return Block(0xFFFFFF45, writer.ToArray());
    }

    /// <summary>Writes a mesh declaration.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="excludeNormals"><see langword="true"/> to declare that faces carry no normal indices.</param>
    /// <returns>The block.</returns>
    public byte[] MeshDeclaration(U3dMeshSpec mesh, bool excludeNormals)
    {
        var writer = Writer().String(mesh.Name).U32(0).U32(excludeNormals ? 1U : 0U)
            .U32((uint)mesh.Faces.Length).U32((uint)mesh.Positions.Length).U32((uint)mesh.Normals.Length)
            .U32((uint)mesh.DiffuseColors.Length).U32(0).U32((uint)mesh.TextureCoordinates.Length)
            .U32((uint)mesh.Shadings.Length);
        foreach (var shading in mesh.Shadings)
        {
            _ = writer.U32(shading.DiffuseColors ? 1U : 0U).U32((uint)shading.LayerDimensions.Length);
            foreach (var dimensions in shading.LayerDimensions)
            {
                _ = writer.U32(dimensions);
            }

            _ = writer.U32(0);
        }

        _ = writer.U32((uint)mesh.Positions.Length).U32(Math.Max(mesh.FinalResolution, (uint)mesh.Positions.Length));
        for (var i = 0; i < QualityFactors; i++)
        {
            _ = writer.U32(0);
        }

        for (var i = 0; i < InverseQuantizations; i++)
        {
            _ = writer.F32(0);
        }

        _ = writer.F32(0).F32(0).F32(0).U32(0);
        return Block(0xFFFFFF31, writer.ToArray());
    }

    /// <summary>Writes the base mesh continuation of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="excludeNormals"><see langword="true"/> when faces carry no normal indices.</param>
    /// <returns>The block.</returns>
    public byte[] BaseMesh(U3dMeshSpec mesh, bool excludeNormals)
    {
        var writer = WriterFor(mesh.Name).String(mesh.Name).U32(0)
            .U32((uint)mesh.Faces.Length).U32((uint)mesh.Positions.Length).U32((uint)mesh.Normals.Length)
            .U32((uint)mesh.DiffuseColors.Length).U32(0).U32((uint)mesh.TextureCoordinates.Length);
        foreach (var position in mesh.Positions)
        {
            _ = writer.Vector3(position);
        }

        foreach (var normal in mesh.Normals)
        {
            _ = writer.Vector3(normal);
        }

        foreach (var color in mesh.DiffuseColors)
        {
            _ = writer.Vector4(color);
        }

        foreach (var coordinate in mesh.TextureCoordinates)
        {
            _ = writer.Vector4(coordinate);
        }

        foreach (var face in mesh.Faces)
        {
            _ = writer.Adaptive(U3dWriterContexts.Shading, face.Shading);
            WriteCorners(writer, mesh, face, excludeNormals);
        }

        return Block(0xFFFFFF3B, writer.ToArray());
    }

    /// <summary>Writes a texture declaration with one PNG continuation image.</summary>
    /// <param name="name">The texture's name.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="compression">The compression type: 2 for PNG.</param>
    /// <param name="channels">The channels the image supplies.</param>
    /// <returns>The block.</returns>
    public byte[] TextureDeclaration(string name, uint width, uint height, byte compression, byte channels) =>
        TextureDeclaration(name, width, height, new U3dImageSpec(compression, channels, false));

    /// <summary>Writes a texture declaration listing continuation images.</summary>
    /// <param name="name">The texture's name.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="images">The continuation images.</param>
    /// <returns>The block.</returns>
    public byte[] TextureDeclaration(string name, uint width, uint height, params U3dImageSpec[] images)
    {
        var writer = Writer().String(name).U32(height).U32(width).U8(0x0F).U32((uint)images.Length);
        foreach (var image in images)
        {
            _ = writer.U8(image.Compression).U8(image.Channels).U16(image.External ? (ushort)1 : (ushort)0);
            _ = image.External ? writer.U32(1).String("http://example.invalid/texture.png") : writer.U32(0);
        }

        return Block(0xFFFFFF55, writer.ToArray());
    }

    /// <summary>Writes a texture continuation holding a picture file.</summary>
    /// <param name="name">The texture's name.</param>
    /// <param name="index">The continuation image's index.</param>
    /// <param name="picture">The picture file's bytes.</param>
    /// <returns>The block.</returns>
    public byte[] TextureContinuation(string name, uint index, byte[] picture)
    {
        var writer = Writer().String(name).U32(index);
        foreach (var value in picture)
        {
            _ = writer.U8(value);
        }

        return Block(0xFFFFFF5C, writer.ToArray());
    }

    /// <summary>Writes a modifier chain block holding blocks.</summary>
    /// <param name="name">The chain's name.</param>
    /// <param name="chainType">0 node, 1 model resource, 2 texture resource.</param>
    /// <param name="blocks">The blocks inside the chain.</param>
    /// <returns>The chain block.</returns>
    public byte[] Chain(string name, uint chainType, params byte[][] blocks)
    {
        var head = Writer().String(name).U32(chainType).U32(0).ToArray();
        var padding = new byte[Padded(head.Length) - head.Length];
        return Block(ChainType, [.. head, .. padding, .. Writer().U32((uint)blocks.Length).ToArray(), .. blocks.SelectMany(static block => block)]);
    }

    /// <summary>Writes the New Object Type block of the free-form curve and surface extension.</summary>
    /// <returns>The block.</returns>
    public byte[] FreeFormType()
    {
        ExtraProfile |= ExtensibleProfile;
        return Block(
            NewObjectTypeType,
            Writer().String("Free-Form Object").U32(1).U32(0x095467DF).U16(0xEE24).U16(0x4C59)
                .U8(0x9A).U8(0xA5).U8(0x76).U8(0x8E).U8(0x39).U8(0xE2).U8(0x68).U8(0xDE)
                .U32(FreeFormDeclarationType).U32(1).U32(FreeFormContinuationType)
                .String("Ecma International").U32(1).String("http://www.ecma-international.org/publications/standards/Ecma-363.htm")
                .String("Free-form curves and surfaces").ToArray());
    }

    /// <summary>Writes the declaration of a free-form object.</summary>
    /// <param name="name">The object's name.</param>
    /// <param name="surfaces">The number of surfaces.</param>
    /// <param name="faces">The number of faces.</param>
    /// <returns>The block.</returns>
    public byte[] FreeFormObject(string name, uint surfaces, uint faces) => Block(
        FreeFormDeclarationType,
        Writer().String(name).U32(0).U32(FreeFormAttributes).U32(surfaces).U32(faces).U32(0).U32(1).U32(0).U32(0).U32(0).U32(0).ToArray());

    /// <summary>Writes the continuation of a free-form object holding surfaces.</summary>
    /// <param name="name">The object's name.</param>
    /// <param name="start">The index of the first surface.</param>
    /// <param name="surfaces">The surfaces.</param>
    /// <returns>The block.</returns>
    public byte[] FreeFormSurfaces(string name, uint start, params U3dSurfaceSpec[] surfaces)
    {
        var writer = WriterFor(name).String(name).U32(0).U32(start).U32(start + (uint)surfaces.Length);
        foreach (var surface in surfaces)
        {
            WriteSurface(writer, surface);
        }

        return Block(FreeFormContinuationType, writer.ToArray());
    }

    /// <summary>Rounds a length up to a whole number of 32-bit words.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The padded length.</returns>
    private static int Padded(int length) => (length + Alignment - 1) / Alignment * Alignment;

    /// <summary>Writes the three corners of a face.</summary>
    /// <param name="writer">The block's writer.</param>
    /// <param name="mesh">The mesh.</param>
    /// <param name="face">The face.</param>
    /// <param name="excludeNormals"><see langword="true"/> when faces carry no normal indices.</param>
    private static void WriteCorners(U3dValueWriter writer, U3dMeshSpec mesh, U3dFaceSpec face, bool excludeNormals)
    {
        var shading = mesh.Shadings[face.Shading];
        for (var corner = 0; corner < Corners; corner++)
        {
            _ = writer.Ranged((uint)mesh.Positions.Length, face.Positions[corner]);
            if (!excludeNormals)
            {
                _ = writer.Ranged((uint)mesh.Normals.Length, face.Normals[corner]);
            }

            if (shading.DiffuseColors)
            {
                _ = writer.Ranged((uint)mesh.DiffuseColors.Length, face.Diffuse[corner]);
            }

            foreach (var layer in shading.LayerDimensions)
            {
                _ = writer.Ranged((uint)mesh.TextureCoordinates.Length, face.TextureCoordinates[corner]);
            }
        }
    }

    /// <summary>Writes one free-form surface.</summary>
    /// <param name="writer">The block's writer.</param>
    /// <param name="surface">The surface.</param>
    private void WriteSurface(U3dValueWriter writer, U3dSurfaceSpec surface)
    {
        var attributes = (surface.IsBezier ? BezierFlag : 0) | (surface.IsRational ? 0 : NonRationalFlag) | (surface.UniformU ? UniformUFlag : 0)
            | (surface.UniformV ? UniformVFlag : 0) | (surface.IndependentKnots ? IndependentFlag : 0);
        _ = writer.U32(attributes).Adaptive(U3dWriterContexts.Shading, surface.Shading).U32(surface.DegreeU).U32(surface.DegreeV);
        WriteKnots(writer, surface.UniformU, surface.KnotsU, surface.MultiplicitiesU, U3dWriterContexts.KnotCountU, U3dWriterContexts.KnotMultiplicityU);
        WriteKnots(writer, surface.UniformV, surface.KnotsV, surface.MultiplicitiesV, U3dWriterContexts.KnotCountV, U3dWriterContexts.KnotMultiplicityV);
        _ = writer.U32(surface.CountU).U32(surface.CountV);
        foreach (var point in surface.Points)
        {
            _ = writer.F32(point.X).F32(point.Y).F32(point.Z);
            if (surface.IsRational)
            {
                _ = (FreeFormAttributes & DoubleWeights) != 0 ? writer.F64(point.W) : writer.F32(point.W);
            }
        }
    }

    /// <summary>Writes the knots of one direction of a free-form surface.</summary>
    /// <param name="writer">The block's writer.</param>
    /// <param name="uniform"><see langword="true"/> to write only the two bounds.</param>
    /// <param name="knots">The unique knots, or the two bounds.</param>
    /// <param name="multiplicities">The multiplicity of each unique knot.</param>
    /// <param name="countContext">The context of the unique knot count.</param>
    /// <param name="multiplicityContext">The context of a multiplicity.</param>
    private void WriteKnots(U3dValueWriter writer, bool uniform, float[] knots, uint[] multiplicities, uint countContext, uint multiplicityContext)
    {
        if (uniform)
        {
            WriteKnot(writer, knots[0]);
            WriteKnot(writer, knots[1]);
            return;
        }

        _ = writer.Adaptive(countContext, (uint)knots.Length);
        for (var i = 0; i < knots.Length; i++)
        {
            _ = writer.Adaptive(multiplicityContext, multiplicities[i]);
            WriteKnot(writer, knots[i]);
        }
    }

    /// <summary>Writes one knot in the precision the declaration gives.</summary>
    /// <param name="writer">The block's writer.</param>
    /// <param name="knot">The knot.</param>
    private void WriteKnot(U3dValueWriter writer, float knot) => _ = (FreeFormAttributes & DoubleKnots) != 0 ? writer.F64(knot) : writer.F32(knot);

    /// <summary>Starts a writer for a block with no compressed values.</summary>
    /// <returns>The writer.</returns>
    private U3dValueWriter Writer() => new(!compressed);

    /// <summary>Starts a writer for a block of an object, sharing the object's adaptive counts with its other blocks.</summary>
    /// <param name="name">The object's name.</param>
    /// <returns>The writer.</returns>
    private U3dValueWriter WriterFor(string name)
    {
        ref var contexts = ref CollectionsMarshal.GetValueRefOrAddDefault(_contexts, name, out _);
        contexts ??= new();
        return new(!compressed, contexts);
    }

    /// <summary>Starts a node block's data with its name and parents.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="parents">The parents.</param>
    /// <returns>The writer.</returns>
    private U3dValueWriter Node(string name, U3dParent[] parents)
    {
        var writer = Writer().String(name).U32((uint)parents.Length);
        foreach (var parent in parents)
        {
            _ = writer.String(parent.Name).Matrix(parent.Transform);
        }

        return writer;
    }

    /// <summary>Writes a view node block.</summary>
    /// <param name="name">The node's name.</param>
    /// <param name="resource">The view resource's name.</param>
    /// <param name="attributes">The view node attributes.</param>
    /// <param name="near">The near clipping distance.</param>
    /// <param name="far">The far clipping distance.</param>
    /// <param name="projection">The field of view or the height of the view; a one-point view writes a projection vector instead.</param>
    /// <param name="parents">The node's parents.</param>
    /// <returns>The block.</returns>
    private byte[] ViewNode(string name, string resource, uint attributes, float near, float far, float projection, U3dParent[] parents)
    {
        var writer = Node(name, parents).String(resource).U32(attributes).F32(near).F32(far);
        _ = attributes == OnePointAttributes ? writer.Vector3(Vector3.UnitY) : writer.F32(projection);
        _ = writer.Vector4(new(1, 1, 0, 0)).U32(1).String("Backdrop").F32(1).F32(0).F32(0).F32(0).I32(0).I32(0).F32(1).F32(1).U32(0);
        return Block(0xFFFFFF24, writer.ToArray());
    }
}
