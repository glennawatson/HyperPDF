// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Decodes CLOD mesh blocks (ECMA-363 9.6.1): the declaration, and the base mesh, which is stored without quantisation. Resolution
/// updates are noted and not applied, so a mesh that has them is its base mesh.
/// </summary>
internal static class U3dMeshDecoder
{
    /// <summary>The attribute flag for faces without normal indices.</summary>
    private const uint ExcludeNormals = 1;

    /// <summary>The bytes of a position or normal: three 32-bit floats.</summary>
    private const int VectorBytes = 12;

    /// <summary>The bytes of a colour or texture coordinate: four 32-bit floats.</summary>
    private const int Vector4Bytes = 16;

    /// <summary>The mask that spaces the checks for cancellation: one in every 4096 faces.</summary>
    private const int CheckMask = 0xFFF;

    /// <summary>Reads a mesh declaration.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the declaration.</param>
    internal static void ReadDeclaration(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        _ = reader.ReadU32();
        var attributes = reader.ReadU32();
        var faces = reader.ReadU32();
        var positions = reader.ReadU32();
        var normals = reader.ReadU32();
        var diffuse = reader.ReadU32();
        var specular = reader.ReadU32();
        var textureCoordinates = reader.ReadU32();
        var shadings = U3dShadingList.ReadAll(ref reader);
        var minimum = reader.ReadU32();
        var final = reader.ReadU32();
        if (reader.Failed || shadings is null)
        {
            builder.Problem("A mesh declaration is cut short.");
            return;
        }

        if (faces > builder.Limits.MaxFaces || positions > builder.Limits.MaxPositions)
        {
            builder.Problem($"The mesh {name} is larger than will be decoded.");
            return;
        }

        builder.MeshDeclarations[name] = new(name, (attributes & ExcludeNormals) != 0, faces, positions, normals, diffuse, specular, textureCoordinates, shadings, minimum, final);
    }

    /// <summary>Reads the base mesh continuation of a declared mesh.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the mesh.</param>
    internal static void ReadBase(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var name = builder.Open(data, null).ReadString();
        if (!builder.MeshDeclarations.TryGetValue(name, out var declaration))
        {
            builder.Problem("A base mesh names a mesh the file does not declare.");
            return;
        }

        var reader = builder.Open(data, name);
        _ = reader.ReadString();
        _ = reader.ReadU32();
        var counts = ReadCounts(ref reader, declaration);
        if (counts is null || !Fits(counts, reader.RemainingBytes))
        {
            builder.Problem($"The base mesh of {name} declares more values than its block or the mesh declaration can hold.");
            return;
        }

        var positions = ReadVectors(ref reader, counts.Positions);
        var normals = ReadVectors(ref reader, counts.Normals);
        var diffuse = ReadVector4s(ref reader, counts.DiffuseColors);
        _ = ReadVector4s(ref reader, counts.SpecularColors);
        var texture = ReadVector4s(ref reader, counts.TextureCoordinates);
        var faces = ReadFaces(ref reader, declaration, counts, builder.Token);
        if (reader.Failed || faces is null)
        {
            builder.Problem($"The base mesh of {name} is cut short or holds a face that refers to a value it does not have.");
            return;
        }

        var complete = declaration.FinalResolution <= declaration.MinimumResolution;
        builder.Meshes.Add(new(name, positions, normals, diffuse, texture, faces, complete));
    }

    /// <summary>Reads the sizes of a base mesh and checks them against the declaration.</summary>
    /// <param name="reader">The block's reader, placed at the sizes.</param>
    /// <param name="declaration">The mesh declaration.</param>
    /// <returns>The sizes, or <see langword="null"/> when a size is larger than the declaration allows.</returns>
    private static BaseCounts? ReadCounts(ref U3dBitReader reader, U3dMeshDeclaration declaration)
    {
        var counts = new BaseCounts(reader.ReadU32(), reader.ReadU32(), reader.ReadU32(), reader.ReadU32(), reader.ReadU32(), reader.ReadU32());
        var within = counts.Faces <= declaration.Faces
            && counts.Positions <= declaration.Positions
            && counts.Normals <= declaration.Normals
            && counts.DiffuseColors <= declaration.DiffuseColors
            && counts.SpecularColors <= declaration.SpecularColors
            && counts.TextureCoordinates <= declaration.TextureCoordinates;
        return reader.Failed || !within ? null : counts;
    }

    /// <summary>Determines whether the values a base mesh declares could fit in the bytes left, so a damaged count cannot ask for more memory than the block has.</summary>
    /// <param name="counts">The sizes.</param>
    /// <param name="remaining">The bytes left in the block.</param>
    /// <returns><see langword="true"/> when the vertex data alone fits.</returns>
    private static bool Fits(BaseCounts counts, int remaining) =>
        (((long)(counts.Positions + counts.Normals) * VectorBytes) + ((long)(counts.DiffuseColors + counts.SpecularColors + counts.TextureCoordinates) * Vector4Bytes)) <= remaining;

    /// <summary>Reads three-component vectors.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="count">The number of vectors.</param>
    /// <returns>The vectors.</returns>
    private static Vector3[] ReadVectors(ref U3dBitReader reader, uint count)
    {
        var vectors = new Vector3[count];
        for (var i = 0; i < vectors.Length; i++)
        {
            vectors[i] = reader.ReadVector3();
        }

        return vectors;
    }

    /// <summary>Reads four-component vectors.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="count">The number of vectors.</param>
    /// <returns>The vectors.</returns>
    private static Vector4[] ReadVector4s(ref U3dBitReader reader, uint count)
    {
        var vectors = new Vector4[count];
        for (var i = 0; i < vectors.Length; i++)
        {
            vectors[i] = reader.ReadVector4();
        }

        return vectors;
    }

    /// <summary>Reads the faces of a base mesh.</summary>
    /// <param name="reader">The block's reader, placed at the first face.</param>
    /// <param name="declaration">The mesh declaration.</param>
    /// <param name="counts">The base mesh's sizes.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The faces, or <see langword="null"/> when a face names a shader list or value that does not exist.</returns>
    private static U3dFace[]? ReadFaces(ref U3dBitReader reader, U3dMeshDeclaration declaration, BaseCounts counts, CancellationToken cancellationToken)
    {
        var faces = new U3dFace[counts.Faces];
        for (var i = 0; i < faces.Length; i++)
        {
            if ((i & CheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var shading = reader.ReadCompressedU32(U3dContexts.Shading);
            if (reader.Failed || shading >= declaration.Shadings.Length)
            {
                return null;
            }

            var list = declaration.Shadings[shading];
            var a = ReadCorner(ref reader, declaration, counts, list);
            var b = ReadCorner(ref reader, declaration, counts, list);
            var c = ReadCorner(ref reader, declaration, counts, list);
            if (reader.Failed || a.Position < 0 || b.Position < 0 || c.Position < 0)
            {
                return null;
            }

            faces[i] = new((int)shading, a, b, c);
        }

        return faces;
    }

    /// <summary>Reads one corner of a face.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="declaration">The mesh declaration.</param>
    /// <param name="counts">The base mesh's sizes.</param>
    /// <param name="list">The shading description of the face.</param>
    /// <returns>The corner; its position is -1 when an index is out of range.</returns>
    private static U3dCorner ReadCorner(ref U3dBitReader reader, U3dMeshDeclaration declaration, BaseCounts counts, U3dShadingList list)
    {
        var position = ReadIndex(ref reader, counts.Positions);
        var normal = declaration.ExcludesNormals ? -1 : ReadIndex(ref reader, counts.Normals);
        var diffuse = list.UsesDiffuse ? ReadIndex(ref reader, counts.DiffuseColors) : -1;
        if (list.UsesSpecular)
        {
            _ = ReadIndex(ref reader, counts.SpecularColors);
        }

        var texture = -1;
        for (var layer = 0; layer < list.LayerDimensions.Length; layer++)
        {
            var index = ReadIndex(ref reader, counts.TextureCoordinates);
            texture = layer == 0 ? index : texture;
        }

        return position < 0 || (!declaration.ExcludesNormals && normal < 0) ? new(-1, -1, -1, -1) : new(position, normal, diffuse, texture);
    }

    /// <summary>Reads an index in a range, which the format writes with a context sized to the range.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="range">The number of values the index may name.</param>
    /// <returns>The index, or -1 when the range is empty or the index is outside it.</returns>
    private static int ReadIndex(ref U3dBitReader reader, uint range)
    {
        if (range == 0)
        {
            reader.Fail();
            return -1;
        }

        var index = reader.ReadCompressedU32(U3dContexts.Range(range));
        return index < range ? (int)index : -1;
    }

    /// <summary>The sizes of a base mesh.</summary>
    /// <param name="Faces">The faces.</param>
    /// <param name="Positions">The positions.</param>
    /// <param name="Normals">The normals.</param>
    /// <param name="DiffuseColors">The diffuse colours.</param>
    /// <param name="SpecularColors">The specular colours.</param>
    /// <param name="TextureCoordinates">The texture coordinates.</param>
    private sealed record BaseCounts(uint Faces, uint Positions, uint Normals, uint DiffuseColors, uint SpecularColors, uint TextureCoordinates);
}
