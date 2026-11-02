// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Decodes resource blocks other than textures: lights, view resources, lit texture shaders, materials and shading modifiers (ECMA-363 9.8, 9.7.5).</summary>
internal static class U3dResourceDecoder
{
    /// <summary>The bits of a lit texture shader's channel field that name active texture layers.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The bytes of a 32-bit value.</summary>
    private const int WordBytes = 4;

    /// <summary>The bytes of a transform matrix: sixteen 32-bit floats.</summary>
    private const int MatrixBytes = 64;

    /// <summary>The bytes of the fixed fields of one texture layer besides its name: intensity, blend function, blend source, constant, mode, two matrices and repeat.</summary>
    private const int LayerBytes = WordBytes + 1 + 1 + WordBytes + 1 + (2 * MatrixBytes) + 1;

    /// <summary>The bytes of an empty name.</summary>
    private const int EmptyNameBytes = 2;

    /// <summary>The 32-bit floats of a pass's fog colour with its near and far values.</summary>
    private const int FogValues = 6;

    /// <summary>The bytes of a pass's fog colour with its near and far values.</summary>
    private const int FogBytes = FogValues * WordBytes;

    /// <summary>The words of a pass before its fog colour: the render attributes and the fog mode.</summary>
    private const int PassWords = 2;

    /// <summary>The fewest bytes a pass of a view resource takes: an empty root name, attributes, fog mode and fog.</summary>
    private const int PassMinimum = EmptyNameBytes + (PassWords * WordBytes) + FogBytes;

    /// <summary>The fewest bytes a shader list takes: its count.</summary>
    private const int ListMinimum = WordBytes;

    /// <summary>The light kinds the specification defines.</summary>
    private const byte LastLightKind = 3;

    /// <summary>The render attribute flag for fog.</summary>
    private const uint FogEnabled = 1;

    /// <summary>Reads a light resource.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the light.</param>
    internal static void ReadLight(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var attributes = reader.ReadU32();
        var kind = reader.ReadU8();
        var color = reader.ReadVector3();
        _ = reader.ReadF32();
        var attenuation = reader.ReadVector3();
        var spotAngle = reader.ReadF32();
        var intensity = reader.ReadF32();
        if (reader.Failed || kind > LastLightKind)
        {
            builder.Problem("A light resource is cut short or names a kind of light the format does not define.");
            return;
        }

        builder.Lights.Add(new(name, attributes, (U3dLightKind)kind, color, attenuation, spotAngle, intensity));
    }

    /// <summary>Reads a view resource: its passes with their root nodes and fog.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the view resource.</param>
    internal static void ReadViewResource(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var passes = reader.ReadU32();
        if (reader.Failed || passes == 0 || (long)passes * PassMinimum > reader.RemainingBytes)
        {
            builder.Problem("A view resource declares no passes or more passes than its block can hold.");
            return;
        }

        var root = reader.ReadString();
        var attributes = reader.ReadU32();
        _ = reader.ReadU32();
        var color = reader.ReadVector4();
        var near = reader.ReadF32();
        var far = reader.ReadF32();
        for (var pass = 1; pass < passes; pass++)
        {
            SkipPass(ref reader);
        }

        var fog = (attributes & FogEnabled) != 0;
        if (reader.Failed)
        {
            builder.Problem("A view resource is cut short.");
            return;
        }

        builder.ViewResources.Add(new(name, root, fog, color, near, far));
    }

    /// <summary>Reads a material resource.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the material.</param>
    internal static void ReadMaterial(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var attributes = reader.ReadU32();
        var ambient = reader.ReadVector3();
        var diffuse = reader.ReadVector3();
        var specular = reader.ReadVector3();
        var emissive = reader.ReadVector3();
        var reflectivity = reader.ReadF32();
        var opacity = reader.ReadF32();
        if (reader.Failed)
        {
            builder.Problem("A material resource is cut short.");
            return;
        }

        builder.Materials.Add(new(name, attributes, ambient, diffuse, specular, emissive, reflectivity, opacity));
    }

    /// <summary>Reads a lit texture shader: its flags, its material and the textures of its active layers.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the shader.</param>
    internal static void ReadShader(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var attributes = reader.ReadU32();
        _ = reader.ReadF32();
        _ = reader.ReadU32();
        _ = reader.ReadU32();
        _ = reader.ReadU32();
        var channels = reader.ReadU32() & ChannelMask;
        _ = reader.ReadU32();
        var material = reader.ReadString();
        var layers = BitOperations.PopCount(channels);
        if (reader.Failed || (long)layers * (LayerBytes + EmptyNameBytes) > reader.RemainingBytes)
        {
            builder.Problem("A shader is cut short.");
            return;
        }

        var textures = new string[layers];
        for (var i = 0; i < textures.Length; i++)
        {
            textures[i] = ReadLayer(ref reader);
        }

        if (reader.Failed)
        {
            builder.Problem("A shader is cut short.");
            return;
        }

        builder.Shaders.Add(new(name, attributes, material, textures));
    }

    /// <summary>Reads a shading modifier: for each shader list, the names of its shaders.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the shading binding.</param>
    internal static void ReadShading(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        _ = reader.ReadU32();
        _ = reader.ReadU32();
        var lists = reader.ReadU32();
        if (reader.Failed || (long)lists * ListMinimum > reader.RemainingBytes)
        {
            builder.Problem("A shading modifier declares more shader lists than its block can hold.");
            return;
        }

        var names = new string[lists][];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = ReadShaderNames(ref reader);
        }

        if (reader.Failed)
        {
            builder.Problem("A shading modifier is cut short.");
            return;
        }

        builder.Shadings.Add(new(name, names));
    }

    /// <summary>Reads past one later pass of a view resource, which is not drawn.</summary>
    /// <param name="reader">The block's reader, placed at the pass.</param>
    private static void SkipPass(ref U3dBitReader reader)
    {
        _ = reader.ReadString();
        _ = reader.ReadU32();
        _ = reader.ReadU32();
        _ = reader.ReadVector4();
        _ = reader.ReadF32();
        _ = reader.ReadF32();
    }

    /// <summary>Reads the names of one shader list.</summary>
    /// <param name="reader">The block's reader, placed at the shader count.</param>
    /// <returns>The names; empty when the block is cut short.</returns>
    private static string[] ReadShaderNames(ref U3dBitReader reader)
    {
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * EmptyNameBytes > reader.RemainingBytes)
        {
            reader.Fail();
            return [];
        }

        var names = new string[count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = reader.ReadString();
        }

        return names;
    }

    /// <summary>Reads one texture layer of a shader, keeping its texture name.</summary>
    /// <param name="reader">The block's reader, placed at the layer.</param>
    /// <returns>The texture's name.</returns>
    private static string ReadLayer(ref U3dBitReader reader)
    {
        var texture = reader.ReadString();
        for (var i = 0; i < LayerBytes; i++)
        {
            _ = reader.ReadU8();
        }

        return texture;
    }
}
