// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Makes the camera of a frame from a view node. A view node looks along its own Y axis with its Z axis up, the orientation
/// ECMA-363 8.3.3 prefers. A model with no view node is framed from the side by a camera that sees all of it.
/// </summary>
internal static class U3dCameras
{
    /// <summary>The field of view in degrees used when a view gives none.</summary>
    private const float DefaultFieldOfView = 34F;

    /// <summary>The nearest distance used when a view gives none.</summary>
    private const float DefaultNear = 0.1F;

    /// <summary>The farthest distance used when a view gives none.</summary>
    private const float DefaultFar = 1000F;

    /// <summary>The share of extra room left around a model by the camera that frames it.</summary>
    private const float Margin = 1.15F;

    /// <summary>The fraction of the radius that the clip planes of the framing camera reach beyond the model.</summary>
    private const float ClipMargin = 1.5F;

    /// <summary>The degrees in a half turn.</summary>
    private const float HalfTurn = 180F;

    /// <summary>The smallest radius a model is framed at, which keeps a single point from giving a zero-sized view.</summary>
    private const float MinimumRadius = 1e-3F;

    /// <summary>How many times the near distance the far distance is at least, when a view gives no far distance.</summary>
    private const float FarOverNear = 2F;

    /// <summary>The fraction of the camera distance the framing camera's near plane is kept at least away.</summary>
    private const float NearFraction = 0.01F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>Makes the camera to draw a frame from.</summary>
    /// <param name="scene">The model with its resources indexed.</param>
    /// <param name="graph">The scene's places.</param>
    /// <param name="viewName">The name of the view node to use, or <see langword="null"/> for the first.</param>
    /// <param name="aspect">The frame's width over its height.</param>
    /// <returns>The camera.</returns>
    internal static U3dCamera Create(U3dRenderScene scene, U3dSceneGraph graph, string? viewName, float aspect)
    {
        var nodes = scene.Model.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Kind == U3dNodeKind.View && (viewName is null || nodes[i].Name == viewName))
            {
                return FromView(nodes[i], graph.PlacesOf(i)[0], aspect);
            }
        }

        return Framing(scene, graph, aspect);
    }

    /// <summary>Makes the camera of a view node.</summary>
    /// <param name="node">The view node.</param>
    /// <param name="world">The node's place in the world.</param>
    /// <param name="aspect">The frame's width over its height.</param>
    /// <returns>The camera.</returns>
    private static U3dCamera FromView(U3dSceneNode node, Matrix4x4 world, float aspect)
    {
        var settings = node.View;
        var near = settings is { NearClip: > 0 } ? settings.NearClip : DefaultNear;
        var far = settings is not null && settings.FarClip > near ? settings.FarClip : MathF.Max(DefaultFar, near * FarOverNear);
        var position = world.Translation;
        var forward = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, world));
        var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, world));
        var projection = settings is { IsOrthographic: true, OrthographicHeight: > 0 }
            ? Matrix4x4.CreateOrthographic(settings.OrthographicHeight * aspect, settings.OrthographicHeight, near, far)
            : Matrix4x4.CreatePerspectiveFieldOfView(Radians(settings is { FieldOfView: > 0 } ? settings.FieldOfView : DefaultFieldOfView), aspect, near, far);
        return new(Matrix4x4.CreateLookAt(position, position + forward, up), projection, position, forward, node.Name);
    }

    /// <summary>Makes the camera that sees all of a model, looking along the world's Y axis.</summary>
    /// <param name="scene">The model with its resources indexed.</param>
    /// <param name="graph">The scene's places.</param>
    /// <param name="aspect">The frame's width over its height.</param>
    /// <returns>The camera.</returns>
    private static U3dCamera Framing(U3dRenderScene scene, U3dSceneGraph graph, float aspect)
    {
        var (centre, radius) = Bounds(scene, graph);
        var half = Radians(DefaultFieldOfView) * Half;
        var limiting = MathF.Min(1, aspect);
        var distance = radius * Margin / MathF.Sin(MathF.Atan(MathF.Tan(half) * limiting));
        var position = centre - (Vector3.UnitY * distance);
        var near = MathF.Max(distance - (radius * ClipMargin), distance * NearFraction);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(Radians(DefaultFieldOfView), aspect, near, distance + (radius * ClipMargin));
        return new(Matrix4x4.CreateLookAt(position, centre, Vector3.UnitZ), projection, position, Vector3.UnitY, string.Empty);
    }

    /// <summary>Finds the sphere around every vertex of every drawn mesh.</summary>
    /// <param name="scene">The model with its resources indexed.</param>
    /// <param name="graph">The scene's places.</param>
    /// <returns>The centre and radius.</returns>
    private static BoundingSphere Bounds(U3dRenderScene scene, U3dSceneGraph graph)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var nodes = scene.Model.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Kind != U3dNodeKind.Model)
            {
                continue;
            }

            foreach (var world in graph.PlacesOf(i))
            {
                foreach (var mesh in scene.MeshesOf(nodes[i].Resource))
                {
                    foreach (var position in mesh.Positions)
                    {
                        var transformed = Vector3.Transform(position, world);
                        min = Vector3.Min(min, transformed);
                        max = Vector3.Max(max, transformed);
                    }
                }
            }
        }

        return min.X > max.X ? new(Vector3.Zero, 1) : new((min + max) * Half, MathF.Max(((max - min) * Half).Length(), MinimumRadius));
    }

    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The angle in radians.</returns>
    private static float Radians(float degrees) => degrees * MathF.PI / HalfTurn;

    /// <summary>A sphere.</summary>
    /// <param name="Centre">The centre.</param>
    /// <param name="Radius">The radius.</param>
    private readonly record struct BoundingSphere(Vector3 Centre, float Radius);
}
