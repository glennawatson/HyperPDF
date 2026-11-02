// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Adds the pieces of the test scenes to a file writer.</summary>
internal static class U3dTestSceneExtensions
{
    /// <summary>Adds scene pieces to a writer.</summary>
    /// <param name="writer">The file writer.</param>
    extension(U3dFileWriter writer)
    {
        /// <summary>Adds a mesh and the red material and shader the tests draw it with.</summary>
        /// <param name="mesh">The mesh.</param>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithMesh(U3dMeshSpec mesh) => writer.WithMesh(mesh, false);

        /// <summary>Adds a mesh and the red material and shader the tests draw it with.</summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="excludeNormals"><see langword="true"/> when the mesh's faces carry no normal indices.</param>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithMesh(U3dMeshSpec mesh, bool excludeNormals) => writer
            .Add(writer.Chain(mesh.Name, 1, writer.MeshDeclaration(mesh, excludeNormals)))
            .Add(writer.BaseMesh(mesh, excludeNormals))
            .WithRedShader();

        /// <summary>Adds the red material and the shader that uses it.</summary>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithRedShader() => writer
            .Add(writer.Material(RedMaterial, new(1, 0, 0), new(1, 0, 0), 1))
            .Add(writer.Shader(RedShader, 1, RedMaterial));

        /// <summary>Adds a directional light at the root, which shines along the Y axis like a camera looks.</summary>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithSun() => writer
            .Add(writer.Light(SunLight, 1, Directional, new(1, 1, 1), 1))
            .Add(writer.LightNode("SunNode", SunLight, Root));

        /// <summary>Adds a model node that draws a mesh with the red shader.</summary>
        /// <param name="resource">The model resource's name.</param>
        /// <param name="visibility">The visibility.</param>
        /// <param name="parents">The node's parents.</param>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithNode(string resource, uint visibility, params U3dParent[] parents) => writer
            .Add(writer.Chain(QuadNode, 0, writer.ModelNode(QuadNode, resource, visibility, parents), writer.ShadingModifier(QuadNode, [RedShader])));

        /// <summary>Adds an ambient light of full strength.</summary>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithAmbient() => writer
            .Add(writer.Light(AmbientLight, 1, 0, new(1, 1, 1), 1))
            .Add(writer.LightNode("AmbientNode", AmbientLight, Root));

        /// <summary>Adds a view resource and the perspective camera in front of the origin.</summary>
        /// <returns>The writer.</returns>
        internal U3dFileWriter WithCamera() => writer
            .Add(writer.ViewResource(ViewResource, 1))
            .Add(writer.PerspectiveView(CameraNode, ViewResource, Near, Far, FieldOfView, CameraPlace));
    }
}
