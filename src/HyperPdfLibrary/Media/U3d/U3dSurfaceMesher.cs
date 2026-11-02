// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Turns a free-form surface into a triangle mesh by evaluating it on a regular grid of parameters. Trim curves are not applied.</summary>
internal static class U3dSurfaceMesher
{
    /// <summary>The number of grid cells in each direction.</summary>
    internal const int Steps = 16;

    /// <summary>The vertices in each direction of the grid.</summary>
    private const int Side = Steps + 1;

    /// <summary>The triangles each grid cell is split into.</summary>
    private const int TrianglesPerCell = 2;

    /// <summary>Evaluates a surface on a grid and joins the grid points into triangles.</summary>
    /// <param name="name">The name for the mesh.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>The mesh: smooth normals, texture coordinates running from 0 to 1, and two triangles to each grid cell.</returns>
    internal static U3dMesh Mesh(string name, U3dSurface surface)
    {
        var positions = new Vector3[Side * Side];
        var coordinates = new Vector4[positions.Length];
        var domainU = U3dSurfaceBasis.DomainOf(surface.KnotsU, surface.DegreeU, surface.IsBezier);
        var domainV = U3dSurfaceBasis.DomainOf(surface.KnotsV, surface.DegreeV, surface.IsBezier);
        for (var i = 0; i < Side; i++)
        {
            var u = (double)i / Steps;
            for (var j = 0; j < Side; j++)
            {
                var v = (double)j / Steps;
                positions[(i * Side) + j] = Evaluate(surface, Lerp(domainU, u), Lerp(domainV, v));
                coordinates[(i * Side) + j] = new((float)u, (float)v, 0, 0);
            }
        }

        var faces = Triangulate(surface.Shading);
        return new(name, positions, Normals(positions, faces), [], coordinates, faces, true);
    }

    /// <summary>Evaluates a surface at a parameter pair.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="u">The U parameter.</param>
    /// <param name="v">The V parameter.</param>
    /// <returns>The point.</returns>
    internal static Vector3 Evaluate(U3dSurface surface, double u, double v)
    {
        Span<double> blendU = stackalloc double[U3dSurfaceBasis.MaxDegree + 1];
        Span<double> blendV = stackalloc double[U3dSurfaceBasis.MaxDegree + 1];
        var firstU = U3dSurfaceBasis.Evaluate(surface.KnotsU, surface.DegreeU, surface.CountU, surface.IsBezier, u, blendU);
        var firstV = U3dSurfaceBasis.Evaluate(surface.KnotsV, surface.DegreeV, surface.CountV, surface.IsBezier, v, blendV);
        var sum = Vector3.Zero;
        var weights = 0.0;
        for (var a = 0; a <= surface.DegreeU; a++)
        {
            for (var b = 0; b <= surface.DegreeV; b++)
            {
                var point = surface.Points[((firstU + a) * surface.CountV) + firstV + b];
                var weight = blendU[a] * blendV[b] * point.W;
                sum += new Vector3(point.X, point.Y, point.Z) * (float)weight;
                weights += weight;
            }
        }

        return weights == 0 ? Vector3.Zero : sum / (float)weights;
    }

    /// <summary>Maps a fraction onto a parameter range.</summary>
    /// <param name="domain">The range.</param>
    /// <param name="fraction">The fraction, from 0 to 1.</param>
    /// <returns>The parameter.</returns>
    private static double Lerp(U3dSurfaceBasis.Domain domain, double fraction) => domain.Low + ((domain.High - domain.Low) * fraction);

    /// <summary>Joins the grid points into two triangles for each cell.</summary>
    /// <param name="shading">The shader list index for every triangle.</param>
    /// <returns>The triangles.</returns>
    private static U3dFace[] Triangulate(int shading)
    {
        var faces = new U3dFace[Steps * Steps * TrianglesPerCell];
        for (var i = 0; i < Steps; i++)
        {
            for (var j = 0; j < Steps; j++)
            {
                var a = (i * Side) + j;
                var b = a + 1;
                var c = a + Side;
                var d = c + 1;
                var at = ((i * Steps) + j) * TrianglesPerCell;
                faces[at] = new(shading, Corner(a), Corner(b), Corner(c));
                faces[at + 1] = new(shading, Corner(b), Corner(d), Corner(c));
            }
        }

        return faces;
    }

    /// <summary>Makes a corner whose position, normal and texture coordinate share one index.</summary>
    /// <param name="index">The grid point's index.</param>
    /// <returns>The corner.</returns>
    private static U3dCorner Corner(int index) => new(index, index, -1, index);

    /// <summary>Averages the normals of the triangles around each position.</summary>
    /// <param name="positions">The positions.</param>
    /// <param name="faces">The triangles.</param>
    /// <returns>A unit normal for each position; zero where no triangle has an area.</returns>
    private static Vector3[] Normals(Vector3[] positions, U3dFace[] faces)
    {
        var normals = new Vector3[positions.Length];
        foreach (var face in faces)
        {
            var edge1 = positions[face.B.Position] - positions[face.A.Position];
            var edge2 = positions[face.C.Position] - positions[face.A.Position];
            var normal = Vector3.Cross(edge1, edge2);
            normals[face.A.Position] += normal;
            normals[face.B.Position] += normal;
            normals[face.C.Position] += normal;
        }

        for (var i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].LengthSquared() > 0 ? Vector3.Normalize(normals[i]) : Vector3.Zero;
        }

        return normals;
    }
}
