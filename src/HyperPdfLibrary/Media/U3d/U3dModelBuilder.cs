// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Collects what the block decoders read, and the limits and per-object state they share.</summary>
/// <param name="limits">What the decode may read.</param>
/// <param name="plain"><see langword="true"/> when the file's profile says its values are not compressed.</param>
/// <param name="cancellationToken">Stops the decode between blocks.</param>
internal sealed class U3dModelBuilder(U3dLimits limits, bool plain, CancellationToken cancellationToken)
{
    /// <summary>The most problems listed; the rest would only repeat them.</summary>
    private const int MaxProblems = 32;

    /// <summary>The problems found, without repeats.</summary>
    private readonly List<string> _problems = [];

    /// <summary>The adaptive counts of each object, by the object's name.</summary>
    private readonly Dictionary<string, U3dContextSet> _contexts = [];

    /// <summary>Gets what the decode may read.</summary>
    internal U3dLimits Limits { get; } = limits;

    /// <summary>Gets the token that stops the decode.</summary>
    internal CancellationToken Token { get; } = cancellationToken;

    /// <summary>Gets the scene nodes read so far.</summary>
    internal List<U3dSceneNode> Nodes { get; } = [];

    /// <summary>Gets the meshes read so far.</summary>
    internal List<U3dMesh> Meshes { get; } = [];

    /// <summary>Gets the materials read so far.</summary>
    internal List<U3dMaterial> Materials { get; } = [];

    /// <summary>Gets the shaders read so far.</summary>
    internal List<U3dShader> Shaders { get; } = [];

    /// <summary>Gets the lights read so far.</summary>
    internal List<U3dLight> Lights { get; } = [];

    /// <summary>Gets the view resources read so far.</summary>
    internal List<U3dViewResource> ViewResources { get; } = [];

    /// <summary>Gets the shading modifiers read so far.</summary>
    internal List<U3dShadingBinding> Shadings { get; } = [];

    /// <summary>Gets the meta data read so far.</summary>
    internal List<U3dMetadataEntry> Metadata { get; } = [];

    /// <summary>Gets the mesh declarations waiting for their data, by mesh name.</summary>
    internal Dictionary<string, U3dMeshDeclaration> MeshDeclarations { get; } = [];

    /// <summary>Gets the texture declarations and the image data that has arrived for them, by texture name.</summary>
    internal Dictionary<string, U3dTexturePieces> TexturePieces { get; } = [];

    /// <summary>Gets the free-form object declarations waiting for their data, by name.</summary>
    internal Dictionary<string, U3dFreeFormDeclaration> FreeForms { get; } = [];

    /// <summary>Gets or sets the block type the file gave to free-form object declarations, or zero when it declared none.</summary>
    internal uint FreeFormDeclarationType { get; set; }

    /// <summary>Gets or sets the block type the file gave to free-form object continuations, or zero when it declared none.</summary>
    internal uint FreeFormContinuationType { get; set; }

    /// <summary>Gets the problems found, in the order they were found.</summary>
    internal string[] Problems => [.. _problems];

    /// <summary>Notes a problem once.</summary>
    /// <param name="text">The problem, in plain words.</param>
    internal void Problem(string text)
    {
        if (_problems.Count < MaxProblems && !_problems.Contains(text))
        {
            _problems.Add(text);
        }
    }

    /// <summary>Opens a block's data section for reading.</summary>
    /// <param name="data">The data section.</param>
    /// <param name="objectName">The name of the object the block belongs to, or <see langword="null"/> when the block holds no compressed values.</param>
    /// <returns>The reader: plain for a file that is not compressed or a block with nothing compressed, and otherwise sharing the object's counts.</returns>
    internal U3dBitReader Open(ReadOnlyMemory<byte> data, string? objectName)
    {
        if (plain || objectName is null)
        {
            return new(data.Span, null);
        }

        ref var contexts = ref CollectionsMarshal.GetValueRefOrAddDefault(_contexts, objectName, out _);
        contexts ??= new();
        return new(data.Span, contexts);
    }
}
