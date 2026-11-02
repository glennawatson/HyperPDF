// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Decodes a U3D file (ECMA-363, fourth edition): the scene nodes with their transforms, the base meshes of CLOD meshes, free-form
/// surfaces, materials, shaders, lights, views, textures and meta data. Counts are checked against <see cref="U3dLimits"/> and the
/// bytes a block has before anything is allocated, and a file that is damaged, hostile or uses what is not decoded keeps what was
/// read and lists what was not in <see cref="U3dModel.Problems"/>. Nothing is run, opened or loaded from another file.
/// </summary>
public static class U3dModelReader
{
    /// <summary>The profile flag for a file whose values are not compressed.</summary>
    private const uint NoCompression = 4;

    /// <summary>The bytes of a chain's bounding sphere: four 32-bit floats.</summary>
    private const int BoundingSphereBytes = 16;

    /// <summary>The bytes of a chain's bounding box: six 32-bit floats.</summary>
    private const int BoundingBoxBytes = 24;

    /// <summary>The chain attribute for a bounding sphere.</summary>
    private const uint HasSphere = 1;

    /// <summary>The chain attribute for a bounding box.</summary>
    private const uint HasBox = 2;

    /// <summary>The alignment of the fields of a chain block.</summary>
    private const int Alignment = 4;

    /// <summary>What reads each kind of block.</summary>
    private static readonly Dictionary<uint, Action<ReadOnlyMemory<byte>, U3dModelBuilder>> Decoders = new()
    {
        [(uint)U3dBlockType.GroupNode] = static (data, builder) => U3dNodeDecoder.Read(U3dNodeKind.Group, data, builder),
        [(uint)U3dBlockType.ModelNode] = static (data, builder) => U3dNodeDecoder.Read(U3dNodeKind.Model, data, builder),
        [(uint)U3dBlockType.LightNode] = static (data, builder) => U3dNodeDecoder.Read(U3dNodeKind.Light, data, builder),
        [(uint)U3dBlockType.ViewNode] = static (data, builder) => U3dNodeDecoder.Read(U3dNodeKind.View, data, builder),
        [(uint)U3dBlockType.ClodMeshDeclaration] = U3dMeshDecoder.ReadDeclaration,
        [(uint)U3dBlockType.ClodBaseMeshContinuation] = U3dMeshDecoder.ReadBase,
        [(uint)U3dBlockType.LightResource] = U3dResourceDecoder.ReadLight,
        [(uint)U3dBlockType.ViewResource] = U3dResourceDecoder.ReadViewResource,
        [(uint)U3dBlockType.LitTextureShader] = U3dResourceDecoder.ReadShader,
        [(uint)U3dBlockType.MaterialResource] = U3dResourceDecoder.ReadMaterial,
        [(uint)U3dBlockType.ShadingModifier] = U3dResourceDecoder.ReadShading,
        [(uint)U3dBlockType.TextureDeclaration] = U3dTextureDecoder.ReadDeclaration,
        [(uint)U3dBlockType.TextureContinuation] = U3dTextureDecoder.ReadContinuation,
        [(uint)U3dBlockType.NewObjectType] = U3dExtensionDecoder.Read,
    };

    /// <summary>What the reader says about each kind of block it does not draw or play.</summary>
    private static readonly Dictionary<uint, string> NotApplied = new()
    {
        [(uint)U3dBlockType.PointSetDeclaration] = "Point sets and line sets are not drawn.",
        [(uint)U3dBlockType.PointSetContinuation] = "Point sets and line sets are not drawn.",
        [(uint)U3dBlockType.LineSetDeclaration] = "Point sets and line sets are not drawn.",
        [(uint)U3dBlockType.LineSetContinuation] = "Point sets and line sets are not drawn.",
        [(uint)U3dBlockType.MotionResource] = "Animations are not played.",
        [(uint)U3dBlockType.GlyphModifier] = "Modifiers other than shading are not applied.",
        [(uint)U3dBlockType.SubdivisionModifier] = "Modifiers other than shading are not applied.",
        [(uint)U3dBlockType.AnimationModifier] = "Modifiers other than shading are not applied.",
        [(uint)U3dBlockType.BoneWeightModifier] = "Modifiers other than shading are not applied.",
        [(uint)U3dBlockType.ClodModifier] = "Modifiers other than shading are not applied.",
    };

    /// <summary>Decodes a U3D file with the default limits.</summary>
    /// <param name="data">The file's bytes, which the 3D stream of a PDF holds.</param>
    /// <returns>What the file holds, and what could not be decoded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static U3dModel Read(ReadOnlyMemory<byte> data) => Read(data, U3dLimits.Default, CancellationToken.None);

    /// <summary>Decodes a U3D file.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="limits">What the decode may read.</param>
    /// <param name="cancellationToken">Stops the decode between blocks.</param>
    /// <returns>What the file holds, and what could not be decoded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static U3dModel Read(ReadOnlyMemory<byte> data, U3dLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        var structure = U3dReader.Read(data.Span);
        var builder = new U3dModelBuilder(limits, (structure.Profile & NoCompression) != 0, cancellationToken);
        var at = 0;
        if (U3dFrames.Next(data, ref at) is { Type: (uint)U3dBlockType.FileHeader })
        {
            _ = ReadBlocks(data, at, builder, false);
        }

        return Finish(structure, builder);
    }

    /// <summary>Decodes the blocks of a region one after another.</summary>
    /// <param name="region">The bytes of the region.</param>
    /// <param name="start">The position of the first block.</param>
    /// <param name="builder">Receives what the blocks hold.</param>
    /// <param name="insideChain">Whether the region is the inside of a modifier chain.</param>
    /// <returns>The number of blocks read.</returns>
    private static int ReadBlocks(ReadOnlyMemory<byte> region, int start, U3dModelBuilder builder, bool insideChain)
    {
        var at = start;
        var count = 0;
        while (count < builder.Limits.MaxBlocks && U3dFrames.Next(region, ref at) is { } frame)
        {
            builder.Token.ThrowIfCancellationRequested();
            count++;
            if (frame.Type == (uint)U3dBlockType.ModifierChain && !insideChain)
            {
                count += ReadChain(in frame, builder);
                continue;
            }

            Decode(in frame, builder);
        }

        return count;
    }

    /// <summary>Decodes the blocks inside a modifier chain.</summary>
    /// <param name="chain">The chain block.</param>
    /// <param name="builder">Receives what the blocks hold.</param>
    /// <returns>The number of blocks read.</returns>
    private static int ReadChain(in U3dBlock chain, U3dModelBuilder builder)
    {
        var reader = builder.Open(chain.Data, null);
        _ = reader.ReadString();
        _ = reader.ReadU32();
        var attributes = reader.ReadU32();
        SkipBounds(ref reader, attributes);
        while (!reader.Failed && reader.BytePosition % Alignment != 0)
        {
            _ = reader.ReadU8();
        }

        var count = reader.ReadU32();
        if (reader.Failed || count > builder.Limits.MaxBlocks)
        {
            builder.Problem("A modifier chain is cut short or declares more modifiers than will be read.");
            return 0;
        }

        return ReadBlocks(chain.Data, reader.BytePosition, builder, true);
    }

    /// <summary>Reads past the bounding sphere and box a chain may carry.</summary>
    /// <param name="reader">The chain block's reader.</param>
    /// <param name="attributes">The chain's attributes.</param>
    private static void SkipBounds(ref U3dBitReader reader, uint attributes)
    {
        var bytes = ((attributes & HasSphere) != 0 ? BoundingSphereBytes : 0) + ((attributes & HasBox) != 0 ? BoundingBoxBytes : 0);
        for (var i = 0; i < bytes; i++)
        {
            _ = reader.ReadU8();
        }
    }

    /// <summary>Decodes one block that is not a modifier chain.</summary>
    /// <param name="frame">The block.</param>
    /// <param name="builder">Receives what the block holds.</param>
    private static void Decode(in U3dBlock frame, U3dModelBuilder builder)
    {
        U3dMetadataDecoder.Read(frame.Type, frame.Meta, builder);
        if (Decoders.TryGetValue(frame.Type, out var decoder))
        {
            decoder(frame.Data, builder);
        }
        else if (frame.Type != 0 && frame.Type == builder.FreeFormDeclarationType)
        {
            U3dFreeFormDecoder.ReadDeclaration(frame.Data, builder);
        }
        else if (frame.Type != 0 && frame.Type == builder.FreeFormContinuationType)
        {
            U3dFreeFormDecoder.ReadContinuation(frame.Data, builder);
        }
        else if (NotApplied.TryGetValue(frame.Type, out var text))
        {
            builder.Problem(text);
        }
    }

    /// <summary>Puts together the model from what the blocks held and notes the meshes that are not whole.</summary>
    /// <param name="structure">The file's structure.</param>
    /// <param name="builder">Holds what the blocks held.</param>
    /// <returns>The model.</returns>
    private static U3dModel Finish(U3dScene structure, U3dModelBuilder builder)
    {
        var textures = U3dTextureDecoder.Finish(builder);
        foreach (var declaration in builder.MeshDeclarations.Values)
        {
            NoteMesh(declaration, builder);
        }

        return new(
            structure,
            [.. builder.Nodes],
            [.. builder.Meshes],
            [.. builder.Materials],
            [.. builder.Shaders],
            [.. builder.Lights],
            [.. builder.ViewResources],
            textures,
            [.. builder.Shadings],
            [.. builder.Metadata],
            Merge(structure.Problems, builder.Problems));
    }

    /// <summary>Notes a mesh whose resolution updates are not applied, or that has no base mesh.</summary>
    /// <param name="declaration">The mesh declaration.</param>
    /// <param name="builder">Receives the problem.</param>
    private static void NoteMesh(U3dMeshDeclaration declaration, U3dModelBuilder builder)
    {
        if (declaration.MinimumResolution == 0)
        {
            builder.Problem($"The mesh {declaration.Name} has no base mesh, so it is not drawn.");
        }
        else if (declaration.FinalResolution > declaration.MinimumResolution)
        {
            builder.Problem($"The mesh {declaration.Name} has resolution updates, which are not applied; its base mesh is drawn.");
        }
    }

    /// <summary>Joins two lists of problems without repeats.</summary>
    /// <param name="first">The first list.</param>
    /// <param name="second">The second list.</param>
    /// <returns>The problems of both.</returns>
    private static string[] Merge(string[] first, string[] second)
    {
        var all = new List<string>(first);
        foreach (var text in second)
        {
            if (!all.Contains(text))
            {
                all.Add(text);
            }
        }

        return [.. all];
    }
}
