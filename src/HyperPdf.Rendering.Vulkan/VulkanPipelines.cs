// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The fixed objects every frame uses: the render pass, the layouts, the two pipelines, the samplers and the pool of descriptor sets.</summary>
[DebuggerDisplay("VulkanPipelines")]
internal sealed class VulkanPipelines : IDisposable
{
    /// <summary>The most descriptor sets the pool holds, which is the most tiles that can be on the GPU at once.</summary>
    internal const uint MaxTiles = 8192;

    /// <summary>A size that holds any structure built here.</summary>
    private const int InfoSize = 160;

    /// <summary>The context.</summary>
    private readonly VulkanContext _context;

    /// <summary>Initializes a new instance of the <see cref="VulkanPipelines"/> class.</summary>
    /// <param name="context">The Vulkan context.</param>
    /// <exception cref="InvalidOperationException">The GPU rejected an object.</exception>
    internal VulkanPipelines(VulkanContext context)
    {
        _context = context;
        RenderPass = VulkanPipelineParts.CreateRenderPass(context);
        SetLayout = VulkanPipelineParts.CreateSetLayout(context);
        PipelineLayout = VulkanPipelineParts.CreatePipelineLayout(context, SetLayout);
        DescriptorPool = VulkanPipelineParts.CreateDescriptorPool(context);
        NearestSampler = VulkanPipelineParts.CreateSampler(context, VkEnums.FilterNearest);
        LinearSampler = VulkanPipelineParts.CreateSampler(context, VkEnums.FilterLinear);
        var vertex = VulkanPipelineParts.CreateShaderModule(context, VulkanShaderCode.Vertex);
        var tileFragment = VulkanPipelineParts.CreateShaderModule(context, VulkanShaderCode.TileFragment);
        var solidFragment = VulkanPipelineParts.CreateShaderModule(context, VulkanShaderCode.SolidFragment);
        TilePipeline = VulkanPipelineParts.CreatePipeline(context, vertex, tileFragment, PipelineLayout, RenderPass);
        SolidPipeline = VulkanPipelineParts.CreatePipeline(context, vertex, solidFragment, PipelineLayout, RenderPass);
        VulkanPipelineParts.DestroyShaderModules(context, vertex, tileFragment, solidFragment);
    }

    /// <summary>Gets the render pass that draws onto the frame image.</summary>
    internal ulong RenderPass { get; }

    /// <summary>Gets the layout of the descriptor set that holds one tile.</summary>
    internal ulong SetLayout { get; }

    /// <summary>Gets the layout shared by both pipelines.</summary>
    internal ulong PipelineLayout { get; }

    /// <summary>Gets the pool the tile descriptor sets come from.</summary>
    internal ulong DescriptorPool { get; }

    /// <summary>Gets the sampler that picks the nearest texel.</summary>
    internal ulong NearestSampler { get; }

    /// <summary>Gets the sampler that blends the four nearest texels.</summary>
    internal ulong LinearSampler { get; }

    /// <summary>Gets the pipeline that draws a tile.</summary>
    internal ulong TilePipeline { get; }

    /// <summary>Gets the pipeline that fills a rectangle.</summary>
    internal ulong SolidPipeline { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => VulkanPipelineParts.Destroy(_context, this);
}
