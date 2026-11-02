// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>The names, numbers and small scenes the decoding and drawing tests share.</summary>
internal static class U3dTestScenes
{
    /// <summary>The distance of the camera from the origin.</summary>
    internal const float CameraDistance = 5;

    /// <summary>The camera's field of view in degrees.</summary>
    internal const float FieldOfView = 60;

    /// <summary>The nearest distance the camera sees.</summary>
    internal const float Near = 0.1F;

    /// <summary>The farthest distance the camera sees.</summary>
    internal const float Far = 100;

    /// <summary>The half size of the test quad.</summary>
    internal const float QuadHalf = 1;

    /// <summary>The number of corners of the quad.</summary>
    internal const int QuadCorners = 4;

    /// <summary>The index of the quad's first corner.</summary>
    internal const uint CornerA = 0;

    /// <summary>The index of the quad's second corner.</summary>
    internal const uint CornerB = 1;

    /// <summary>The index of the quad's third corner.</summary>
    internal const uint CornerC = 2;

    /// <summary>The index of the quad's fourth corner.</summary>
    internal const uint CornerD = 3;

    /// <summary>The visibility value for a node whose front and back faces are both drawn.</summary>
    internal const uint BothSides = 3;

    /// <summary>The visibility value for a node whose front faces are drawn.</summary>
    internal const uint FrontOnly = 1;

    /// <summary>The visibility value for a node whose back faces are drawn.</summary>
    internal const uint BackOnly = 2;

    /// <summary>The light kind of a directional light.</summary>
    internal const byte Directional = 1;

    /// <summary>The light kind of a point light.</summary>
    internal const byte Point = 2;

    /// <summary>The name of the quad's model resource.</summary>
    internal const string QuadResource = "QuadMesh";

    /// <summary>The name of the quad's node.</summary>
    internal const string QuadNode = "QuadNode";

    /// <summary>The name of the camera's view node.</summary>
    internal const string CameraNode = "Camera";

    /// <summary>The name of the red material.</summary>
    internal const string RedMaterial = "Red";

    /// <summary>The name of the red shader.</summary>
    internal const string RedShader = "RedShader";

    /// <summary>The name of the ambient light.</summary>
    internal const string AmbientLight = "AmbientLight";

    /// <summary>The name of the view resource.</summary>
    internal const string ViewResource = "ViewResource";

    /// <summary>The name of the sun.</summary>
    internal const string SunLight = "Sun";

    /// <summary>The normal every corner of the quad has: it faces the camera.</summary>
    private const float TowardCamera = -1;

    /// <summary>Gets the parent that places a node at the root of the scene.</summary>
    internal static U3dParent Root { get; } = new(string.Empty, Matrix4x4.Identity);

    /// <summary>Gets the parent that places the camera in front of the origin, looking along the Y axis.</summary>
    internal static U3dParent CameraPlace { get; } = new(string.Empty, Matrix4x4.CreateTranslation(0, -CameraDistance, 0));

    /// <summary>Makes a quad in the XZ plane that faces a camera looking along the Y axis.</summary>
    /// <param name="name">The mesh's name.</param>
    /// <param name="finalResolution">The final resolution to declare.</param>
    /// <returns>The mesh.</returns>
    internal static U3dMeshSpec Quad(string name, uint finalResolution) => new(
        name,
        [new(-QuadHalf, 0, -QuadHalf), new(QuadHalf, 0, -QuadHalf), new(QuadHalf, 0, QuadHalf), new(-QuadHalf, 0, QuadHalf)],
        [new(0, TowardCamera, 0)],
        [],
        [],
        [Face(CornerA, CornerB, CornerC), Face(CornerA, CornerC, CornerD)],
        [new(false, [])],
        finalResolution);

    /// <summary>Makes a face of the quad with one normal for all corners.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <param name="c">The third position.</param>
    /// <returns>The face.</returns>
    internal static U3dFaceSpec Face(uint a, uint b, uint c) => new(0, [a, b, c], [0, 0, 0], [], []);

    /// <summary>Writes a scene with a red quad lit by an ambient light and seen by a perspective camera.</summary>
    /// <param name="compressed"><see langword="true"/> to write the arithmetic code.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] RedQuad(bool compressed) => RedQuad(compressed, BothSides);

    /// <summary>Writes a scene with a red quad lit by an ambient light and seen by a perspective camera.</summary>
    /// <param name="compressed"><see langword="true"/> to write the arithmetic code.</param>
    /// <param name="visibility">The visibility of the quad's node.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] RedQuad(bool compressed, uint visibility) => new U3dFileWriter(compressed)
        .WithMesh(Quad(QuadResource, QuadCorners))
        .WithNode(QuadResource, visibility, Root)
        .WithAmbient()
        .WithCamera()
        .ToArray();
}
