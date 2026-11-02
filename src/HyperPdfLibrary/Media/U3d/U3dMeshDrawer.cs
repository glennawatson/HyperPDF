// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Draws one mesh, placed in the world, onto a canvas: it moves the mesh's positions to clip space, lights each corner, cuts at the near plane and fills.</summary>
internal static class U3dMeshDrawer
{
    /// <summary>The mask that spaces the checks for cancellation: one in every 1024 faces.</summary>
    private const int CheckMask = 0x3FF;

    /// <summary>Draws a mesh.</summary>
    /// <param name="job">What to draw and where.</param>
    /// <param name="canvas">Receives the pixels.</param>
    /// <param name="cancellationToken">Stops the drawing between faces.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Draw(in MeshJob job, IU3dTriangleTarget canvas, CancellationToken cancellationToken)
    {
        var mesh = job.Mesh;
        var world = ArrayPool<Vector3>.Shared.Rent(mesh.Positions.Length);
        var clip = ArrayPool<Vector4>.Shared.Rent(mesh.Positions.Length);
        try
        {
            var viewProjection = job.Camera.View * job.Camera.Projection;
            for (var i = 0; i < mesh.Positions.Length; i++)
            {
                world[i] = Vector3.Transform(mesh.Positions[i], job.World);
                clip[i] = Vector4.Transform(new Vector4(world[i], 1), viewProjection);
            }

            DrawFaces(job, new(world, clip), canvas, cancellationToken);
        }
        finally
        {
            ArrayPool<Vector3>.Shared.Return(world);
            ArrayPool<Vector4>.Shared.Return(clip);
        }
    }

    /// <summary>Draws the faces of a mesh whose positions have been moved.</summary>
    /// <param name="job">What to draw and where.</param>
    /// <param name="positions">The positions in world and clip space.</param>
    /// <param name="canvas">Receives the pixels.</param>
    /// <param name="cancellationToken">Stops the drawing between faces.</param>
    private static void DrawFaces(in MeshJob job, in MovedPositions positions, IU3dTriangleTarget canvas, CancellationToken cancellationToken)
    {
        Span<U3dRasterVertex> cut = stackalloc U3dRasterVertex[U3dClipper.BufferLength];
        var faces = job.Mesh.Faces;
        for (var i = 0; i < faces.Length; i++)
        {
            if ((i & CheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var face = faces[i];
            var style = job.Styles[Math.Min(Math.Max(face.Shading, 0), job.Styles.Length - 1)];
            var normal = FaceNormal(positions.World, face);
            var a = Vertex(job, positions, face.A, normal, style);
            var b = Vertex(job, positions, face.B, normal, style);
            var c = Vertex(job, positions, face.C, normal, style);
            var count = U3dClipper.Clip(in a, in b, in c, cut);
            for (var corner = 1; corner < count - 1; corner++)
            {
                canvas.Fill(in cut[0], in cut[corner], in cut[corner + 1], style, job.Visibility);
            }
        }
    }

    /// <summary>Works out the unit normal of a face from its positions.</summary>
    /// <param name="world">The positions in world space.</param>
    /// <param name="face">The face.</param>
    /// <returns>The normal; zero for a face with no area.</returns>
    private static Vector3 FaceNormal(Vector3[] world, in U3dFace face)
    {
        var normal = Vector3.Cross(world[face.B.Position] - world[face.A.Position], world[face.C.Position] - world[face.A.Position]);
        return normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.Zero;
    }

    /// <summary>Makes the vertex of one corner of a face: its clip position, its lit colour and its texture coordinate.</summary>
    /// <param name="job">What to draw and where.</param>
    /// <param name="positions">The positions in world and clip space.</param>
    /// <param name="corner">The corner.</param>
    /// <param name="faceNormal">The face's own normal, for a corner with no normal of its own.</param>
    /// <param name="style">The face's style.</param>
    /// <returns>The vertex.</returns>
    private static U3dRasterVertex Vertex(in MeshJob job, in MovedPositions positions, in U3dCorner corner, Vector3 faceNormal, U3dSurfaceStyle style)
    {
        var mesh = job.Mesh;
        var position = positions.World[corner.Position];
        var normal = CornerNormal(mesh, corner, job.World, faceNormal);
        if (Vector3.Dot(normal, job.Camera.Position - position) < 0)
        {
            normal = -normal;
        }

        var diffuse = style.VertexColor && corner.Diffuse >= 0 && corner.Diffuse < mesh.DiffuseColors.Length
            ? new Vector3(mesh.DiffuseColors[corner.Diffuse].X, mesh.DiffuseColors[corner.Diffuse].Y, mesh.DiffuseColors[corner.Diffuse].Z)
            : style.Diffuse;
        var color = style.Emissive + (style.Ambient * job.Rig.Ambient) + (diffuse * job.Rig.Direct(position, normal));
        var uv = corner.TextureCoordinate >= 0 && corner.TextureCoordinate < mesh.TextureCoordinates.Length
            ? new Vector2(mesh.TextureCoordinates[corner.TextureCoordinate].X, mesh.TextureCoordinates[corner.TextureCoordinate].Y)
            : Vector2.Zero;
        return new(positions.Clip[corner.Position], color, uv);
    }

    /// <summary>Gets the world-space unit normal at a corner.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="corner">The corner.</param>
    /// <param name="world">The mesh's world matrix.</param>
    /// <param name="faceNormal">The face's normal, used when the corner has none.</param>
    /// <returns>The normal.</returns>
    private static Vector3 CornerNormal(U3dMesh mesh, in U3dCorner corner, Matrix4x4 world, Vector3 faceNormal)
    {
        if (corner.Normal < 0 || corner.Normal >= mesh.Normals.Length)
        {
            return faceNormal;
        }

        var normal = Vector3.TransformNormal(mesh.Normals[corner.Normal], world);
        return normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : faceNormal;
    }

    /// <summary>One mesh placed in the world, with the camera and lights that draw it.</summary>
    /// <param name="Mesh">The mesh.</param>
    /// <param name="World">The mesh's world matrix.</param>
    /// <param name="Camera">The camera.</param>
    /// <param name="Rig">The lights.</param>
    /// <param name="Styles">The style of each shader list.</param>
    /// <param name="Visibility">Which faces to draw: 1 front, 2 back, 3 both.</param>
    internal readonly record struct MeshJob(U3dMesh Mesh, Matrix4x4 World, U3dCamera Camera, U3dLightRig Rig, U3dSurfaceStyle[] Styles, uint Visibility);

    /// <summary>A mesh's positions in world and clip space.</summary>
    /// <param name="World">The positions in world space.</param>
    /// <param name="Clip">The positions in clip space.</param>
    private readonly record struct MovedPositions(Vector3[] World, Vector4[] Clip);
}
