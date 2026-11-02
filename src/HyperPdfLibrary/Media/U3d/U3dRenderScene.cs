// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A model with its resources indexed by name, so that drawing a frame finds them without searching.</summary>
internal sealed class U3dRenderScene
{
    /// <summary>The meshes by the name of their model resource; a free-form object has one mesh for each surface.</summary>
    private readonly Dictionary<string, List<U3dMesh>> _meshes = [];

    /// <summary>The shaders by name.</summary>
    private readonly Dictionary<string, U3dShader> _shaders = [];

    /// <summary>The materials by name.</summary>
    private readonly Dictionary<string, U3dMaterial> _materials = [];

    /// <summary>The textures by name.</summary>
    private readonly Dictionary<string, U3dTexture> _textures = [];

    /// <summary>The shading modifiers by the name of their node chain.</summary>
    private readonly Dictionary<string, U3dShadingBinding> _shadings = [];

    /// <summary>The light resources by name.</summary>
    private readonly Dictionary<string, U3dLight> _lights = [];

    /// <summary>Initializes a new instance of the <see cref="U3dRenderScene"/> class.</summary>
    /// <param name="model">The model.</param>
    internal U3dRenderScene(U3dModel model)
    {
        Model = model;
        foreach (var mesh in model.Meshes)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_meshes, mesh.Name, out _);
            list ??= [];
            list.Add(mesh);
        }

        Index(model.Shaders, _shaders, static shader => shader.Name);
        Index(model.Materials, _materials, static material => material.Name);
        Index(model.Textures, _textures, static texture => texture.Name);
        Index(model.Shadings, _shadings, static shading => shading.Name);
        Index(model.Lights, _lights, static light => light.Name);
    }

    /// <summary>Gets the model.</summary>
    internal U3dModel Model { get; }

    /// <summary>Gets the meshes of a model resource.</summary>
    /// <param name="resource">The resource name.</param>
    /// <returns>The meshes; none when the resource has none.</returns>
    internal IReadOnlyList<U3dMesh> MeshesOf(string resource) => _meshes.TryGetValue(resource, out var list) ? list : [];

    /// <summary>Gets a light resource.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The light, or <see langword="null"/> when the file defines none of that name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal U3dLight? LightNamed(string name) => _lights.GetValueOrDefault(name);

    /// <summary>Works out the style of each shader list a model node's mesh can use.</summary>
    /// <param name="nodeName">The model node's name, which is also the name of its shading modifier.</param>
    /// <param name="lists">The number of shader lists the mesh has.</param>
    /// <returns>One style for each list: the first shader of the list, or the default style.</returns>
    internal U3dSurfaceStyle[] StylesFor(string nodeName, int lists)
    {
        var styles = new U3dSurfaceStyle[Math.Max(1, lists)];
        var binding = _shadings.GetValueOrDefault(nodeName);
        for (var i = 0; i < styles.Length; i++)
        {
            var names = binding is not null && i < binding.Lists.Length ? binding.Lists[i] : [];
            styles[i] = names.Length > 0 && _shaders.TryGetValue(names[0], out var shader) ? StyleOf(shader) : U3dSurfaceStyle.Default;
        }

        return styles;
    }

    /// <summary>Indexes items by name, keeping the first of any name.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="index">Receives the items.</param>
    /// <param name="name">Gets an item's name.</param>
    private static void Index<T>(T[] items, Dictionary<string, T> index, Func<T, string> name)
    {
        foreach (var item in items)
        {
            _ = index.TryAdd(name(item), item);
        }
    }

    /// <summary>Makes the style a shader gives.</summary>
    /// <param name="shader">The shader.</param>
    /// <returns>The style.</returns>
    private U3dSurfaceStyle StyleOf(U3dShader shader)
    {
        var material = _materials.GetValueOrDefault(shader.Material, U3dMaterial.Default);
        var texture = shader.Textures.Length > 0 ? _textures.GetValueOrDefault(shader.Textures[0]) : null;
        return U3dSurfaceStyle.Of(material, texture, shader.UsesVertexColor);
    }
}
