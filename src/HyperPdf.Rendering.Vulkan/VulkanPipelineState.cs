// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Writes the fixed function state structures of the compositor's graphics pipelines.</summary>
internal static unsafe class VulkanPipelineState
{
    /// <summary>The size of each structure buffer.</summary>
    internal const int PartSize = 80;

    /// <summary>The size of one VkPipelineShaderStageCreateInfo in bytes, which is the distance between stages in the array the driver reads.</summary>
    internal const int StageSize = 48;

    /// <summary>The number of dynamic states: the viewport and the scissor.</summary>
    internal const uint DynamicStateCount = 2;

    /// <summary>The number of rasterization fields the compositor leaves at zero.</summary>
    private const int UnusedRasterizationFields = 6;

    /// <summary>Writes a shader stage structure.</summary>
    /// <param name="target">The buffer, one stage size long. The stages sit next to each other in an array, so the writer must not clear past one stage.</param>
    /// <param name="stage">The stage bit.</param>
    /// <param name="module">The shader module.</param>
    /// <param name="entry">The null terminated entry point name.</param>
    internal static void Stage(byte* target, uint stage, ulong module, byte* entry)
    {
        var writer = new VkWriter(new(target, StageSize));
        writer.U32(VkEnums.PipelineShaderStageType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(stage);
        writer.U64(module);
        writer.U64((ulong)entry);
        writer.U64(0);
    }

    /// <summary>Writes the vertex input state: none, because the vertex shader makes the quad from the vertex index.</summary>
    /// <param name="target">The buffer.</param>
    internal static void VertexInput(byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineVertexInputType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(0);
        writer.U64(0);
        writer.U32(0);
        writer.U64(0);
    }

    /// <summary>Writes the input assembly state: a triangle strip.</summary>
    /// <param name="target">The buffer.</param>
    internal static void InputAssembly(byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineInputAssemblyType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(VkEnums.TopologyTriangleStrip);
        writer.U32(0);
    }

    /// <summary>Writes the viewport state: one viewport and one scissor, both set when the commands are recorded.</summary>
    /// <param name="target">The buffer.</param>
    internal static void Viewport(byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineViewportType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(1);
        writer.U64(0);
        writer.U32(1);
        writer.U64(0);
    }

    /// <summary>Writes the rasterization state: filled triangles with no culling.</summary>
    /// <param name="target">The buffer.</param>
    internal static void Rasterization(byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineRasterizationType);
        writer.U64(0);
        writer.U32(0);

        // Depth clamp, discard, polygon mode, cull mode, front face and depth bias are all off or zero.
        for (var i = 0; i < UnusedRasterizationFields; i++)
        {
            writer.U32(0);
        }

        writer.F32(0);
        writer.F32(0);
        writer.F32(0);
        writer.F32(1);
    }

    /// <summary>Writes the multisample state: one sample per pixel.</summary>
    /// <param name="target">The buffer.</param>
    internal static void Multisample(byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineMultisampleType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(1);
        writer.U32(0);
        writer.F32(0);
        writer.U64(0);
        writer.U32(0);
        writer.U32(0);
    }

    /// <summary>Writes the colour blend state: premultiplied alpha on the one colour attachment.</summary>
    /// <param name="attachment">The buffer for the attachment state.</param>
    /// <param name="target">The buffer for the blend state, which points at the attachment.</param>
    internal static void ColorBlend(byte* attachment, byte* target)
    {
        var state = new VkWriter(new(attachment, PartSize));
        state.U32(1);
        state.U32(VkEnums.BlendOne);
        state.U32(VkEnums.BlendOneMinusSourceAlpha);
        state.U32(0);
        state.U32(VkEnums.BlendOne);
        state.U32(VkEnums.BlendOneMinusSourceAlpha);
        state.U32(0);
        state.U32(VkEnums.ColorWriteAll);

        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineColorBlendType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(0);
        writer.U32(1);
        writer.U64((ulong)attachment);
    }

    /// <summary>Writes the dynamic state: the viewport and scissor change with the frame size.</summary>
    /// <param name="states">The two dynamic state ids.</param>
    /// <param name="target">The buffer.</param>
    internal static void Dynamic(uint* states, byte* target)
    {
        var writer = new VkWriter(new(target, PartSize));
        writer.U32(VkEnums.PipelineDynamicStateType);
        writer.U64(0);
        writer.U32(0);
        writer.U32(DynamicStateCount);
        writer.U64((ulong)states);
    }
}
