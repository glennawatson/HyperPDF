// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Metal;

/// <summary>The compositor's shaders in the Metal Shading Language. Metal compiles them once when the backend starts, which is how Metal takes shaders that are not built with Xcode.</summary>
internal static class MetalShaderSource
{
    /// <summary>Gets the null terminated source: a vertex function that turns pixels into clip space and a fragment function that multiplies the tile by the vertex colour.</summary>
    internal static ReadOnlySpan<byte> Text =>
        """
        #include <metal_stdlib>
        using namespace metal;

        struct QuadVertex
        {
            packed_float2 position;
            packed_float2 uv;
            uint color;
        };

        struct Frame
        {
            float2 scale;
            float2 unused;
        };

        struct Varyings
        {
            float4 position [[position]];
            float2 uv;
            float4 color;
        };

        vertex Varyings vertexMain(uint id [[vertex_id]], const device QuadVertex* vertices [[buffer(0)]], constant Frame& frame [[buffer(1)]])
        {
            QuadVertex v = vertices[id];
            Varyings out;
            out.position = float4((v.position[0] * frame.scale.x) - 1.0, 1.0 - (v.position[1] * frame.scale.y), 0.0, 1.0);
            out.uv = float2(v.uv[0], v.uv[1]);
            out.color = float4(float(v.color & 0xFF), float((v.color >> 8) & 0xFF), float((v.color >> 16) & 0xFF), float(v.color >> 24)) / 255.0;
            return out;
        }

        fragment float4 fragmentMain(Varyings in [[stage_in]], texture2d<float> tile [[texture(0)]], sampler tileSampler [[sampler(0)]])
        {
            return tile.sample(tileSampler, in.uv) * in.color;
        }
        """u8 + "\0"u8;
}
