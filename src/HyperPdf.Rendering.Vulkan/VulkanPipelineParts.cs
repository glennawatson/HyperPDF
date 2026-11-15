// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Builds the individual objects of <see cref="VulkanPipelines"/>.</summary>
internal static unsafe class VulkanPipelineParts
{
    /// <summary>A size that holds any structure built here.</summary>
    private const int InfoSize = 160;

    /// <summary>The size of one structure that other structures point to.</summary>
    private const int PartSize = VulkanPipelineState.PartSize;

    /// <summary>The number of bytes in a SPIR-V word.</summary>
    private const int WordSize = 4;

    /// <summary>The number of shader stages in a pipeline: vertex and fragment.</summary>
    private const uint StageCount = 2;

    /// <summary>The slot of the vertex input state in the state buffer.</summary>
    private const int VertexInputSlot = 0;

    /// <summary>The slot of the input assembly state in the state buffer.</summary>
    private const int AssemblySlot = 1;

    /// <summary>The slot of the viewport state in the state buffer.</summary>
    private const int ViewportSlot = 2;

    /// <summary>The slot of the rasterization state in the state buffer.</summary>
    private const int RasterizationSlot = 3;

    /// <summary>The slot of the multisample state in the state buffer.</summary>
    private const int MultisampleSlot = 4;

    /// <summary>The slot of the blend attachment state in the state buffer.</summary>
    private const int BlendAttachmentSlot = 5;

    /// <summary>The slot of the blend state in the state buffer.</summary>
    private const int BlendSlot = 6;

    /// <summary>The number of state slots.</summary>
    private const int SlotCount = 7;

    /// <summary>The bind point for graphics pipelines.</summary>
    private const uint GraphicsBindPoint = 0;

    /// <summary>The unused base pipeline index in a pipeline create info.</summary>
    private const int NoBasePipeline = -1;

    /// <summary>Destroys every object of a <see cref="VulkanPipelines"/>.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="pipelines">The pipelines.</param>
    internal static void Destroy(VulkanContext context, VulkanPipelines pipelines)
    {
        var device = context.Device;
        NativeMethods.DestroyPipeline(device, pipelines.TilePipeline, null);
        NativeMethods.DestroyPipeline(device, pipelines.SolidPipeline, null);
        NativeMethods.DestroySampler(device, pipelines.NearestSampler, null);
        NativeMethods.DestroySampler(device, pipelines.LinearSampler, null);
        NativeMethods.DestroyDescriptorPool(device, pipelines.DescriptorPool, null);
        NativeMethods.DestroyPipelineLayout(device, pipelines.PipelineLayout, null);
        NativeMethods.DestroyDescriptorSetLayout(device, pipelines.SetLayout, null);
        NativeMethods.DestroyRenderPass(device, pipelines.RenderPass, null);
    }

    /// <summary>Destroys the shader modules once the pipelines that use them exist.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="vertex">The vertex shader module.</param>
    /// <param name="tileFragment">The tile fragment shader module.</param>
    /// <param name="solidFragment">The solid fragment shader module.</param>
    internal static void DestroyShaderModules(VulkanContext context, ulong vertex, ulong tileFragment, ulong solidFragment)
    {
        NativeMethods.DestroyShaderModule(context.Device, vertex, null);
        NativeMethods.DestroyShaderModule(context.Device, tileFragment, null);
        NativeMethods.DestroyShaderModule(context.Device, solidFragment, null);
    }

    /// <summary>Creates the render pass that draws onto the frame image and keeps what it held.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <returns>The render pass.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the render pass.</exception>
    internal static ulong CreateRenderPass(VulkanContext context)
    {
        byte* attachment = stackalloc byte[PartSize];
        var description = new VkWriter(new(attachment, PartSize));
        description.U32(0);
        description.U32(VkEnums.FormatB8G8R8A8Unorm);
        description.U32(1);
        description.U32(VkEnums.LoadOpLoad);
        description.U32(VkEnums.StoreOpStore);
        description.U32(VkEnums.LoadOpDontCare);
        description.U32(VkEnums.StoreOpDontCare);
        description.U32(VkEnums.LayoutColorAttachment);
        description.U32(VkEnums.LayoutColorAttachment);

        byte* reference = stackalloc byte[PartSize];
        var attachmentReference = new VkWriter(new(reference, PartSize));
        attachmentReference.U32(0);
        attachmentReference.U32(VkEnums.LayoutColorAttachment);

        byte* subpass = stackalloc byte[PartSize];
        var subpassWriter = new VkWriter(new(subpass, PartSize));
        subpassWriter.U32(0);
        subpassWriter.U32(GraphicsBindPoint);
        subpassWriter.U32(0);
        subpassWriter.U64(0);
        subpassWriter.U32(1);
        subpassWriter.U64((ulong)reference);
        subpassWriter.U64(0);
        subpassWriter.U64(0);
        subpassWriter.U32(0);
        subpassWriter.U64(0);

        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.RenderPassCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(1);
        create.U64((ulong)attachment);
        create.U32(1);
        create.U64((ulong)subpass);
        create.U32(0);
        create.U64(0);
        ulong renderPass;
        return NativeMethods.CreateRenderPass(context.Device, info, null, &renderPass) == VkConstants.Success
            ? renderPass
            : throw new InvalidOperationException("The GPU rejected the render pass.");
    }

    /// <summary>Creates the layout of a descriptor set holding one sampled tile.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the layout.</exception>
    internal static ulong CreateSetLayout(VulkanContext context)
    {
        byte* binding = stackalloc byte[PartSize];
        var bindingWriter = new VkWriter(new(binding, PartSize));
        bindingWriter.U32(0);
        bindingWriter.U32(VkEnums.DescriptorCombinedImageSampler);
        bindingWriter.U32(1);
        bindingWriter.U32(VkEnums.ShaderStageFragment);
        bindingWriter.U64(0);

        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.DescriptorSetLayoutCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(1);
        create.U64((ulong)binding);
        ulong layout;
        return NativeMethods.CreateDescriptorSetLayout(context.Device, info, null, &layout) == VkConstants.Success
            ? layout
            : throw new InvalidOperationException("The GPU rejected the descriptor set layout.");
    }

    /// <summary>Creates the pipeline layout: one tile descriptor set and the push constants.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="setLayout">The descriptor set layout.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the layout.</exception>
    internal static ulong CreatePipelineLayout(VulkanContext context, ulong setLayout)
    {
        byte* range = stackalloc byte[PartSize];
        var rangeWriter = new VkWriter(new(range, PartSize));
        rangeWriter.U32(VkEnums.PushConstantStages);
        rangeWriter.U32(0);
        rangeWriter.U32(VkEnums.PushConstantSize);

        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.PipelineLayoutCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(1);
        create.U64((ulong)&setLayout);
        create.U32(1);
        create.U64((ulong)range);
        ulong layout;
        return NativeMethods.CreatePipelineLayout(context.Device, info, null, &layout) == VkConstants.Success
            ? layout
            : throw new InvalidOperationException("The GPU rejected the pipeline layout.");
    }

    /// <summary>Creates the pool the tile descriptor sets come from.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <returns>The pool.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the pool.</exception>
    internal static ulong CreateDescriptorPool(VulkanContext context)
    {
        byte* size = stackalloc byte[PartSize];
        var sizeWriter = new VkWriter(new(size, PartSize));
        sizeWriter.U32(VkEnums.DescriptorCombinedImageSampler);
        sizeWriter.U32(VulkanPipelines.MaxTiles);

        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.DescriptorPoolCreateInfoType);
        create.U64(0);
        create.U32(VkEnums.DescriptorPoolFreeable);
        create.U32(VulkanPipelines.MaxTiles);
        create.U32(1);
        create.U64((ulong)size);
        ulong pool;
        return NativeMethods.CreateDescriptorPool(context.Device, info, null, &pool) == VkConstants.Success
            ? pool
            : throw new InvalidOperationException("The GPU rejected the descriptor pool.");
    }

    /// <summary>Creates a sampler that clamps at the edges.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="filter">The filter, nearest or linear.</param>
    /// <returns>The sampler.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the sampler.</exception>
    internal static ulong CreateSampler(VulkanContext context, uint filter)
    {
        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.SamplerCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(filter);
        create.U32(filter);
        create.U32(0);
        create.U32(VkEnums.AddressClampToEdge);
        create.U32(VkEnums.AddressClampToEdge);
        create.U32(VkEnums.AddressClampToEdge);
        create.F32(0);
        create.U32(0);
        create.F32(1);
        create.U32(0);
        create.U32(0);
        create.F32(0);
        create.F32(0);
        create.U32(0);
        create.U32(0);
        ulong sampler;
        return NativeMethods.CreateSampler(context.Device, info, null, &sampler) == VkConstants.Success
            ? sampler
            : throw new InvalidOperationException("The GPU rejected the sampler.");
    }

    /// <summary>Creates a shader module from SPIR-V.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="code">The SPIR-V bytes.</param>
    /// <returns>The module.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the shader.</exception>
    internal static ulong CreateShaderModule(VulkanContext context, ReadOnlySpan<byte> code)
    {
        // SPIR-V is read as 32 bit words, so the bytes are copied to memory that is aligned for them.
        var words = new uint[code.Length / WordSize];
        code.CopyTo(MemoryMarshal.AsBytes(words.AsSpan()));
        fixed (uint* pointer = words)
        {
            byte* info = stackalloc byte[InfoSize];
            var create = new VkWriter(new(info, InfoSize));
            create.U32(VkEnums.ShaderModuleCreateInfoType);
            create.U64(0);
            create.U32(0);
            create.U64((ulong)code.Length);
            create.U64((ulong)pointer);
            ulong module;
            return NativeMethods.CreateShaderModule(context.Device, info, null, &module) == VkConstants.Success
                ? module
                : throw new InvalidOperationException("The GPU rejected a shader.");
        }
    }

    /// <summary>Creates a graphics pipeline that draws a triangle strip of four vertices with premultiplied alpha blending.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="vertex">The vertex shader module.</param>
    /// <param name="fragment">The fragment shader module.</param>
    /// <param name="layout">The pipeline layout.</param>
    /// <param name="renderPass">The render pass.</param>
    /// <returns>The pipeline.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the pipeline.</exception>
    internal static ulong CreatePipeline(VulkanContext context, ulong vertex, ulong fragment, ulong layout, ulong renderPass)
    {
        fixed (byte* entry = "main\0"u8)
        {
            // The driver reads the stages as an array of VkPipelineShaderStageCreateInfo, so they sit one stage size apart.
            byte* stages = stackalloc byte[VulkanPipelineState.StageSize * (int)StageCount];
            VulkanPipelineState.Stage(stages, VkEnums.ShaderStageVertex, vertex, entry);
            VulkanPipelineState.Stage(stages + VulkanPipelineState.StageSize, VkEnums.ShaderStageFragment, fragment, entry);
            byte* states = stackalloc byte[PartSize * SlotCount];
            VulkanPipelineState.VertexInput(states + (PartSize * VertexInputSlot));
            VulkanPipelineState.InputAssembly(states + (PartSize * AssemblySlot));
            VulkanPipelineState.Viewport(states + (PartSize * ViewportSlot));
            VulkanPipelineState.Rasterization(states + (PartSize * RasterizationSlot));
            VulkanPipelineState.Multisample(states + (PartSize * MultisampleSlot));
            VulkanPipelineState.ColorBlend(states + (PartSize * BlendAttachmentSlot), states + (PartSize * BlendSlot));
            return CreateWithState(context, stages, states, layout, renderPass);
        }
    }

    /// <summary>Creates the pipeline from prepared stage and state structures.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <param name="stages">The two shader stage structures.</param>
    /// <param name="states">The seven state structures in the order vertex input, assembly, viewport, rasterization, multisample, blend attachment and blend.</param>
    /// <param name="layout">The pipeline layout.</param>
    /// <param name="renderPass">The render pass.</param>
    /// <returns>The pipeline.</returns>
    /// <exception cref="InvalidOperationException">The GPU rejected the pipeline.</exception>
    private static ulong CreateWithState(VulkanContext context, byte* stages, byte* states, ulong layout, ulong renderPass)
    {
        uint* dynamicStates = stackalloc uint[] { VkEnums.DynamicViewport, VkEnums.DynamicScissor };
        byte* dynamic = stackalloc byte[PartSize];
        VulkanPipelineState.Dynamic(dynamicStates, dynamic);
        byte* info = stackalloc byte[InfoSize];
        var create = new VkWriter(new(info, InfoSize));
        create.U32(VkEnums.GraphicsPipelineCreateInfoType);
        create.U64(0);
        create.U32(0);
        create.U32(StageCount);
        create.U64((ulong)stages);
        create.U64((ulong)(states + (PartSize * VertexInputSlot)));
        create.U64((ulong)(states + (PartSize * AssemblySlot)));
        create.U64(0);
        create.U64((ulong)(states + (PartSize * ViewportSlot)));
        create.U64((ulong)(states + (PartSize * RasterizationSlot)));
        create.U64((ulong)(states + (PartSize * MultisampleSlot)));
        create.U64(0);
        create.U64((ulong)(states + (PartSize * BlendSlot)));
        create.U64((ulong)dynamic);
        create.U64(layout);
        create.U64(renderPass);
        create.U32(0);
        create.U64(0);
        create.I32(NoBasePipeline);
        ulong pipeline;
        var result = NativeMethods.CreateGraphicsPipelines(context.Device, 0, 1, info, null, &pipeline);
        return result == VkConstants.Success
            ? pipeline
            : throw new InvalidOperationException($"The GPU rejected a pipeline (VkResult {result}).");
    }
}
