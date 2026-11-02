// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdf.Benchmarks;

/// <summary>Measures decoding a U3D sphere of about 8,000 triangles and drawing it; allocations come from the EventPipe trace.</summary>
public class U3dRenderBenchmarks
{
    /// <summary>The grid cells in each direction of the sphere.</summary>
    private const int Cells = 64;

    /// <summary>The side of the frame in pixels.</summary>
    private const int Side = 512;

    /// <summary>The distance of the camera.</summary>
    private const float Distance = 4;

    /// <summary>The field of view in degrees.</summary>
    private const float FieldOfView = 45;

    /// <summary>The far clipping distance.</summary>
    private const float Far = 100;

    /// <summary>The visibility of a node whose front and back faces are drawn.</summary>
    private const uint BothSides = 3;

    /// <summary>The near clipping distance.</summary>
    private const float Near = 0.1F;

    /// <summary>A full turn in radians.</summary>
    private const float FullTurn = MathF.PI * 2;

    /// <summary>The name of the sphere's model resource.</summary>
    private const string SphereName = "Sphere";

    /// <summary>The file.</summary>
    private byte[] _file = [];

    /// <summary>The decoded model.</summary>
    private U3dModel _model = null!;

    /// <summary>The frame options.</summary>
    private U3dRenderOptions _options = null!;

    /// <summary>Makes the sphere file and decodes it once for the drawing benchmark.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sphere = Sphere();
        var root = new U3dParent(string.Empty, Matrix4x4.Identity);
        var writer = new U3dFileWriter(true);
        _file = writer
            .Add(writer.Chain(SphereName, 1, writer.MeshDeclaration(sphere, false)))
            .Add(writer.BaseMesh(sphere, false))
            .Add(writer.Material("M", new(1, 0, 0), new(1, 0, 0), 1))
            .Add(writer.Shader("S", 1, "M"))
            .Add(writer.Chain("Node", 0, writer.ModelNode("Node", SphereName, BothSides, root), writer.ShadingModifier("Node", ["S"])))
            .Add(writer.Light("Sun", 1, 1, new(1, 1, 1), 1))
            .Add(writer.LightNode("SunNode", "Sun", root))
            .Add(writer.ViewResource("V", 1))
            .Add(writer.PerspectiveView("Cam", "V", Near, Far, FieldOfView, new U3dParent(string.Empty, Matrix4x4.CreateTranslation(0, -Distance, 0))))
            .ToArray();
        _model = U3dModelReader.Read(_file);
        _options = new(Side, Side, null, new(0, 0, 0, 1));
    }

    /// <summary>Decodes the file: the arithmetic code, the base mesh and the scene.</summary>
    /// <returns>The number of faces.</returns>
    [Benchmark]
    public int Decode() => U3dModelReader.Read(_file).Meshes[0].Faces.Length;

    /// <summary>Draws one frame.</summary>
    /// <returns>The number of triangles drawn.</returns>
    [Benchmark]
    public int Render() => U3dRenderer.Render(_model, _options, CancellationToken.None).TrianglesDrawn;

    /// <summary>Makes a sphere of unit radius, with normals equal to positions.</summary>
    /// <returns>The mesh.</returns>
    private static U3dMeshSpec Sphere()
    {
        var side = Cells + 1;
        var positions = new Vector3[side * side];
        for (var i = 0; i < side; i++)
        {
            var theta = MathF.PI * i / Cells;
            for (var j = 0; j < side; j++)
            {
                var phi = FullTurn * j / Cells;
                positions[(i * side) + j] = new(MathF.Sin(theta) * MathF.Cos(phi), MathF.Sin(theta) * MathF.Sin(phi), MathF.Cos(theta));
            }
        }

        var faces = new List<U3dFaceSpec>();
        for (var i = 0; i < Cells; i++)
        {
            for (var j = 0; j < Cells; j++)
            {
                var a = (uint)((i * side) + j);
                var b = a + 1;
                var c = a + (uint)side;
                var d = c + 1;
                faces.Add(new(0, [a, b, c], [a, b, c], [], []));
                faces.Add(new(0, [b, d, c], [b, d, c], [], []));
            }
        }

        return new(SphereName, positions, positions, [], [], [.. faces], [new(false, [])], (uint)positions.Length);
    }
}
