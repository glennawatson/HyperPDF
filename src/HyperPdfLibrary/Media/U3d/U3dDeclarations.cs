// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Reads what a declaration block of a U3D file says: scene nodes, the size of shapes, and the names of resources.</summary>
internal static class U3dDeclarations
{
    /// <summary>The most faces, points or lines one shape may declare before the reader says it is beyond what it will decode.</summary>
    internal const uint MaxPrimitives = 50_000_000;

    /// <summary>The bytes of a parent's transform: sixteen 32-bit floats.</summary>
    private const int TransformLength = 64;

    /// <summary>The fewest bytes a parent takes: an empty name's length and the transform.</summary>
    private const int ParentMinimum = TransformLength + 2;

    /// <summary>What each block type of the base profile is.</summary>
    private static readonly Dictionary<U3dBlockType, Category> Categories = new()
    {
        [U3dBlockType.GroupNode] = Category.Node,
        [U3dBlockType.ModelNode] = Category.Node,
        [U3dBlockType.LightNode] = Category.Node,
        [U3dBlockType.ViewNode] = Category.Node,
        [U3dBlockType.ClodMeshDeclaration] = Category.Shape,
        [U3dBlockType.PointSetDeclaration] = Category.Shape,
        [U3dBlockType.LineSetDeclaration] = Category.Shape,
        [U3dBlockType.LightResource] = Category.Resource,
        [U3dBlockType.ViewResource] = Category.Resource,
        [U3dBlockType.LitTextureShader] = Category.Resource,
        [U3dBlockType.MaterialResource] = Category.Resource,
        [U3dBlockType.TextureDeclaration] = Category.Resource,
        [U3dBlockType.MotionResource] = Category.Resource,
        [U3dBlockType.ClodBaseMeshContinuation] = Category.Data,
        [U3dBlockType.ClodProgressiveMeshContinuation] = Category.Data,
        [U3dBlockType.PointSetContinuation] = Category.Data,
        [U3dBlockType.LineSetContinuation] = Category.Data,
        [U3dBlockType.TextureContinuation] = Category.Data,
        [U3dBlockType.PriorityUpdate] = Category.Data,
        [U3dBlockType.GlyphModifier] = Category.Modifier,
        [U3dBlockType.SubdivisionModifier] = Category.Modifier,
        [U3dBlockType.AnimationModifier] = Category.Modifier,
        [U3dBlockType.BoneWeightModifier] = Category.Modifier,
        [U3dBlockType.ShadingModifier] = Category.Modifier,
        [U3dBlockType.ClodModifier] = Category.Modifier,
        [U3dBlockType.NewObjectType] = Category.Extension,
        [U3dBlockType.FileReference] = Category.Reference,
    };

    /// <summary>What a block is, as far as the reader is concerned.</summary>
    private enum Category
    {
        /// <summary>A scene node.</summary>
        Node = 0,

        /// <summary>A mesh, point set or line set declaration.</summary>
        Shape = 1,

        /// <summary>A named resource.</summary>
        Resource = 2,

        /// <summary>A block of data that is kept but not decoded.</summary>
        Data = 3,

        /// <summary>A modifier, which is counted.</summary>
        Modifier = 4,

        /// <summary>A declaration of a new kind of object.</summary>
        Extension = 5,

        /// <summary>A reference to another file, which is never loaded.</summary>
        Reference = 6,

        /// <summary>A block type that the file does not define.</summary>
        Unknown = 7,
    }

    /// <summary>Reads one block that is not a modifier chain.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="payload">The block's data.</param>
    /// <param name="builder">Receives what the block declares.</param>
    internal static void Apply(uint type, ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        switch (CategoryOf(type))
        {
            case Category.Node:
            {
                ReadNode(type, payload, builder);
                break;
            }

            case Category.Shape:
            {
                ReadShape(type, payload, builder);
                break;
            }

            case Category.Resource:
            {
                ReadResource(type, payload, builder);
                break;
            }

            case Category.Data:
            {
                builder.ContinuationBlocks++;
                break;
            }

            case Category.Extension:
            {
                var cursor = new U3dCursor(payload);
                builder.Extension(cursor.ReadString());
                break;
            }

            case Category.Reference:
            {
                builder.Problem("The file refers to another file, which is never loaded.");
                break;
            }

            case Category.Unknown:
            {
                // A file that uses the extensible profile may define more block types than this reader knows.
                builder.Problem(string.Create(CultureInfo.InvariantCulture, $"The file uses a block type this reader does not know ({type:X8})."));
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Sorts a block type.</summary>
    /// <param name="type">The block type.</param>
    /// <returns>What the block is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Category CategoryOf(uint type) => Categories.GetValueOrDefault((U3dBlockType)type, Category.Unknown);

    /// <summary>Gets the kind of node a block type declares.</summary>
    /// <param name="type">The block type.</param>
    /// <returns>The kind.</returns>
    private static U3dNodeKind NodeKindOf(uint type) => (U3dBlockType)type switch
    {
        U3dBlockType.ModelNode => U3dNodeKind.Model,
        U3dBlockType.LightNode => U3dNodeKind.Light,
        U3dBlockType.ViewNode => U3dNodeKind.View,
        _ => U3dNodeKind.Group,
    };

    /// <summary>Reads a scene node: its name, its parents, and the resource it uses.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="payload">The block's data.</param>
    /// <param name="builder">Receives the node.</param>
    private static void ReadNode(uint type, ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(payload);
        var name = cursor.ReadString();
        var count = cursor.ReadU32();
        if (cursor.Failed || (long)count * ParentMinimum > cursor.Remaining)
        {
            builder.Problem("A scene node declares more parents than its block can hold.");
            return;
        }

        var parents = new string[count];
        for (var i = 0; i < parents.Length; i++)
        {
            parents[i] = cursor.ReadString();
            _ = cursor.Skip(TransformLength);
        }

        var kind = NodeKindOf(type);
        var resource = kind == U3dNodeKind.Group ? string.Empty : cursor.ReadString();
        if (cursor.Failed)
        {
            builder.Problem("A scene node block is cut short.");
            return;
        }

        builder.Nodes.Add(new(kind, name, parents, resource));
    }

    /// <summary>Reads the size of a mesh, point set or line set.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="payload">The block's data.</param>
    /// <param name="builder">Receives the shape.</param>
    private static void ReadShape(uint type, ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(payload);
        var name = cursor.ReadString();
        _ = cursor.ReadU32();

        // Mesh attributes or the reserved field comes next; the counts that follow have the same order for all three shapes.
        _ = cursor.ReadU32();
        var primitives = cursor.ReadU32();
        var positions = cursor.ReadU32();
        var normals = cursor.ReadU32();
        _ = cursor.ReadU32();
        _ = cursor.ReadU32();
        var textureCoordinates = cursor.ReadU32();
        var shading = cursor.ReadU32();
        if (cursor.Failed)
        {
            builder.Problem("A shape declaration is cut short.");
            return;
        }

        if (primitives > MaxPrimitives)
        {
            builder.Problem($"The shape {name} declares more than {MaxPrimitives} faces, points or lines, which is more than will be read.");
        }

        builder.Geometry.Add(new(name, (U3dBlockType)type, primitives, positions, normals, textureCoordinates, shading));
    }

    /// <summary>Reads the name of a resource.</summary>
    /// <param name="type">The block type.</param>
    /// <param name="payload">The block's data.</param>
    /// <param name="builder">Receives the resource.</param>
    private static void ReadResource(uint type, ReadOnlySpan<byte> payload, U3dSceneBuilder builder)
    {
        var cursor = new U3dCursor(payload);
        var name = cursor.ReadString();
        if (cursor.Failed)
        {
            builder.Problem("A resource block is cut short.");
            return;
        }

        builder.Resources.Add(new((U3dBlockType)type, name));
    }
}
