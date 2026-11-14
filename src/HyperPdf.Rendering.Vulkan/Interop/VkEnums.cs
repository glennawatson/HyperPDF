// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>The Vulkan structure types, enumerants and flag bits the compositor uses.</summary>
internal static class VkEnums
{
    /// <summary>The structure type of VkSubmitInfo.</summary>
    internal const uint SubmitInfoType = 4;

    /// <summary>The structure type of VkMemoryAllocateInfo.</summary>
    internal const uint MemoryAllocateInfoType = 5;

    /// <summary>The structure type of VkFenceCreateInfo.</summary>
    internal const uint FenceCreateInfoType = 8;

    /// <summary>The structure type of VkBufferCreateInfo.</summary>
    internal const uint BufferCreateInfoType = 12;

    /// <summary>The structure type of VkImageCreateInfo.</summary>
    internal const uint ImageCreateInfoType = 14;

    /// <summary>The structure type of VkImageViewCreateInfo.</summary>
    internal const uint ImageViewCreateInfoType = 15;

    /// <summary>The structure type of VkShaderModuleCreateInfo.</summary>
    internal const uint ShaderModuleCreateInfoType = 16;

    /// <summary>The structure type of VkPipelineShaderStageCreateInfo.</summary>
    internal const uint PipelineShaderStageType = 18;

    /// <summary>The structure type of VkPipelineVertexInputStateCreateInfo.</summary>
    internal const uint PipelineVertexInputType = 19;

    /// <summary>The structure type of VkPipelineInputAssemblyStateCreateInfo.</summary>
    internal const uint PipelineInputAssemblyType = 20;

    /// <summary>The structure type of VkPipelineViewportStateCreateInfo.</summary>
    internal const uint PipelineViewportType = 22;

    /// <summary>The structure type of VkPipelineRasterizationStateCreateInfo.</summary>
    internal const uint PipelineRasterizationType = 23;

    /// <summary>The structure type of VkPipelineMultisampleStateCreateInfo.</summary>
    internal const uint PipelineMultisampleType = 24;

    /// <summary>The structure type of VkPipelineColorBlendStateCreateInfo.</summary>
    internal const uint PipelineColorBlendType = 26;

    /// <summary>The structure type of VkPipelineDynamicStateCreateInfo.</summary>
    internal const uint PipelineDynamicStateType = 27;

    /// <summary>The structure type of VkGraphicsPipelineCreateInfo.</summary>
    internal const uint GraphicsPipelineCreateInfoType = 28;

    /// <summary>The structure type of VkPipelineLayoutCreateInfo.</summary>
    internal const uint PipelineLayoutCreateInfoType = 30;

    /// <summary>The structure type of VkSamplerCreateInfo.</summary>
    internal const uint SamplerCreateInfoType = 31;

    /// <summary>The structure type of VkDescriptorSetLayoutCreateInfo.</summary>
    internal const uint DescriptorSetLayoutCreateInfoType = 32;

    /// <summary>The structure type of VkDescriptorPoolCreateInfo.</summary>
    internal const uint DescriptorPoolCreateInfoType = 33;

    /// <summary>The structure type of VkDescriptorSetAllocateInfo.</summary>
    internal const uint DescriptorSetAllocateInfoType = 34;

    /// <summary>The structure type of VkWriteDescriptorSet.</summary>
    internal const uint WriteDescriptorSetType = 35;

    /// <summary>The structure type of VkFramebufferCreateInfo.</summary>
    internal const uint FramebufferCreateInfoType = 37;

    /// <summary>The structure type of VkRenderPassCreateInfo.</summary>
    internal const uint RenderPassCreateInfoType = 38;

    /// <summary>The structure type of VkCommandPoolCreateInfo.</summary>
    internal const uint CommandPoolCreateInfoType = 39;

    /// <summary>The structure type of VkCommandBufferAllocateInfo.</summary>
    internal const uint CommandBufferAllocateInfoType = 40;

    /// <summary>The structure type of VkCommandBufferBeginInfo.</summary>
    internal const uint CommandBufferBeginInfoType = 42;

    /// <summary>The structure type of VkRenderPassBeginInfo.</summary>
    internal const uint RenderPassBeginInfoType = 43;

    /// <summary>The structure type of VkImageMemoryBarrier.</summary>
    internal const uint ImageMemoryBarrierType = 45;

    /// <summary>The format B8G8R8A8_UNORM, which matches the BGRA bytes of tiles.</summary>
    internal const uint FormatB8G8R8A8Unorm = 44;

    /// <summary>The image layout UNDEFINED.</summary>
    internal const uint LayoutUndefined = 0;

    /// <summary>The image layout COLOR_ATTACHMENT_OPTIMAL.</summary>
    internal const uint LayoutColorAttachment = 2;

    /// <summary>The image layout SHADER_READ_ONLY_OPTIMAL.</summary>
    internal const uint LayoutShaderReadOnly = 5;

    /// <summary>The image layout TRANSFER_SRC_OPTIMAL.</summary>
    internal const uint LayoutTransferSource = 6;

    /// <summary>The image layout TRANSFER_DST_OPTIMAL.</summary>
    internal const uint LayoutTransferDestination = 7;

    /// <summary>The image usage bit for being a copy source.</summary>
    internal const uint ImageUsageTransferSource = 1;

    /// <summary>The image usage bit for being a copy destination.</summary>
    internal const uint ImageUsageTransferDestination = 2;

    /// <summary>The image usage bit for being sampled by a shader.</summary>
    internal const uint ImageUsageSampled = 4;

    /// <summary>The image usage bit for being a colour attachment.</summary>
    internal const uint ImageUsageColorAttachment = 16;

    /// <summary>The buffer usage bit for being a copy source.</summary>
    internal const uint BufferUsageTransferSource = 1;

    /// <summary>The buffer usage bit for being a copy destination.</summary>
    internal const uint BufferUsageTransferDestination = 2;

    /// <summary>The memory property bit for device local memory.</summary>
    internal const uint MemoryDeviceLocal = 1;

    /// <summary>The memory property bit for memory the CPU can map.</summary>
    internal const uint MemoryHostVisible = 2;

    /// <summary>The memory property bit for mapped memory that needs no flushes.</summary>
    internal const uint MemoryHostCoherent = 4;

    /// <summary>The image aspect bit for colour.</summary>
    internal const uint AspectColor = 1;

    /// <summary>The pipeline stage bit for the top of the pipe.</summary>
    internal const uint StageTopOfPipe = 1;

    /// <summary>The pipeline stage bit for fragment shaders.</summary>
    internal const uint StageFragmentShader = 128;

    /// <summary>The pipeline stage bit for colour attachment output.</summary>
    internal const uint StageColorAttachmentOutput = 1024;

    /// <summary>The pipeline stage bit for transfers.</summary>
    internal const uint StageTransfer = 4096;

    /// <summary>The access bit for shader reads.</summary>
    internal const uint AccessShaderRead = 32;

    /// <summary>The access bit for colour attachment writes.</summary>
    internal const uint AccessColorAttachmentWrite = 256;

    /// <summary>The access bit for transfer reads.</summary>
    internal const uint AccessTransferRead = 2048;

    /// <summary>The access bit for transfer writes.</summary>
    internal const uint AccessTransferWrite = 4096;

    /// <summary>The shader stage bit for vertex shaders.</summary>
    internal const uint ShaderStageVertex = 1;

    /// <summary>The shader stage bit for fragment shaders.</summary>
    internal const uint ShaderStageFragment = 16;

    /// <summary>The descriptor type COMBINED_IMAGE_SAMPLER.</summary>
    internal const uint DescriptorCombinedImageSampler = 1;

    /// <summary>The command pool flag that lets one command buffer be reset on its own.</summary>
    internal const uint CommandPoolResettable = 2;

    /// <summary>The descriptor pool flag that lets single sets be freed.</summary>
    internal const uint DescriptorPoolFreeable = 1;

    /// <summary>The fence flag that makes a new fence start signalled.</summary>
    internal const uint FenceSignaled = 1;

    /// <summary>The wait timeout used for frame fences, in nanoseconds.</summary>
    internal const ulong FenceTimeout = 5_000_000_000;

    /// <summary>The filter NEAREST.</summary>
    internal const uint FilterNearest = 0;

    /// <summary>The filter LINEAR.</summary>
    internal const uint FilterLinear = 1;

    /// <summary>The address mode CLAMP_TO_EDGE.</summary>
    internal const uint AddressClampToEdge = 2;

    /// <summary>The primitive topology TRIANGLE_STRIP.</summary>
    internal const uint TopologyTriangleStrip = 4;

    /// <summary>The blend factor ONE.</summary>
    internal const uint BlendOne = 1;

    /// <summary>The blend factor ONE_MINUS_SRC_ALPHA.</summary>
    internal const uint BlendOneMinusSourceAlpha = 7;

    /// <summary>The colour write mask for all four channels.</summary>
    internal const uint ColorWriteAll = 15;

    /// <summary>The dynamic state VIEWPORT.</summary>
    internal const uint DynamicViewport = 0;

    /// <summary>The dynamic state SCISSOR.</summary>
    internal const uint DynamicScissor = 1;

    /// <summary>The attachment load operation LOAD, which keeps what the frame image held.</summary>
    internal const uint LoadOpLoad = 0;

    /// <summary>The attachment load operation DONT_CARE.</summary>
    internal const uint LoadOpDontCare = 2;

    /// <summary>The attachment store operation STORE.</summary>
    internal const uint StoreOpStore = 0;

    /// <summary>The attachment store operation DONT_CARE.</summary>
    internal const uint StoreOpDontCare = 1;

    /// <summary>The number of descriptor sets a tile owns: one per sampler filter.</summary>
    internal const uint SetsPerTile = 2;

    /// <summary>The command buffer level PRIMARY.</summary>
    internal const uint CommandBufferPrimary = 0;

    /// <summary>The command buffer usage flag ONE_TIME_SUBMIT.</summary>
    internal const uint CommandBufferOneTimeSubmit = 1;

    /// <summary>The subpass contents INLINE.</summary>
    internal const uint SubpassInline = 0;

    /// <summary>The graphics pipeline bind point.</summary>
    internal const uint BindPointGraphics = 0;

    /// <summary>The number of vertices in the quad's triangle strip.</summary>
    internal const uint QuadVertexCount = 4;

    /// <summary>The queue family ignored value.</summary>
    internal const uint QueueFamilyIgnored = 0xFFFF_FFFF;

    /// <summary>The size of the push constants in bytes: a rectangle, the viewport, padding and a colour.</summary>
    internal const uint PushConstantSize = 48;

    /// <summary>The shader stages that read the push constants.</summary>
    internal const uint PushConstantStages = ShaderStageVertex | ShaderStageFragment;

    /// <summary>The offset of the memory type list in VkPhysicalDeviceMemoryProperties.</summary>
    internal const int MemoryTypesOffset = 4;

    /// <summary>The size of one VkMemoryType in bytes.</summary>
    internal const int MemoryTypeSize = 8;

    /// <summary>The size of VkPhysicalDeviceMemoryProperties in bytes.</summary>
    internal const int MemoryPropertiesSize = 520;

    /// <summary>The size of VkMemoryRequirements in bytes.</summary>
    internal const int MemoryRequirementsSize = 24;

    /// <summary>The offset of the memory type bits in VkMemoryRequirements.</summary>
    internal const int MemoryTypeBitsOffset = 16;

    /// <summary>The offset of the alignment in VkMemoryRequirements.</summary>
    internal const int AlignmentOffset = 8;

    /// <summary>The structure type of VkSwapchainCreateInfoKHR.</summary>
    internal const uint SwapchainCreateInfoType = 1_000_001_000;

    /// <summary>The structure type of VkPresentInfoKHR.</summary>
    internal const uint PresentInfoType = 1_000_001_001;

    /// <summary>The structure type of VkXlibSurfaceCreateInfoKHR.</summary>
    internal const uint XlibSurfaceCreateInfoType = 1_000_004_000;

    /// <summary>The image layout a swapchain image must be in to be presented.</summary>
    internal const uint LayoutPresentSource = 1_000_001_002;

    /// <summary>The VkResult of a swapchain that still works but no longer matches the window exactly.</summary>
    internal const int Suboptimal = 1_000_001_003;

    /// <summary>The VkResult of a swapchain that no longer matches the window and must be made again.</summary>
    internal const int OutOfDate = -1_000_001_004;

    /// <summary>The present mode that waits for the display's refresh, which every driver has.</summary>
    internal const uint PresentModeFifo = 2;

    /// <summary>The composite alpha bit of an opaque window.</summary>
    internal const uint CompositeAlphaOpaque = 1;
}
