// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>Source generated entry points of the Vulkan loader, which exports every core function.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name, resolved through <see cref="Core.Platform.NativeLibraries"/> to the loader of each platform.</summary>
    internal const string Library = "vulkan";

    /// <summary>Native <c>vkCreateInstance</c>.</summary>
    /// <param name="createInfo">The creation info.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="instance">Receives the instance.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateInstance")]
    internal static partial int CreateInstance(VkInstanceCreateInfo* createInfo, void* allocator, ulong* instance);

    /// <summary>Native <c>vkDestroyInstance</c>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyInstance")]
    internal static partial void DestroyInstance(ulong instance, void* allocator);

    /// <summary>Native <c>vkEnumeratePhysicalDevices</c>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="count">The capacity of <paramref name="devices"/>, replaced by the number found.</param>
    /// <param name="devices">Receives the physical devices.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkEnumeratePhysicalDevices")]
    internal static partial int EnumeratePhysicalDevices(ulong instance, uint* count, ulong* devices);

    /// <summary>Native <c>vkGetPhysicalDeviceProperties</c>.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <param name="properties">Receives the VkPhysicalDeviceProperties.</param>
    [LibraryImport(Library, EntryPoint = "vkGetPhysicalDeviceProperties")]
    internal static partial void GetPhysicalDeviceProperties(ulong physicalDevice, byte* properties);

    /// <summary>Native <c>vkGetPhysicalDeviceQueueFamilyProperties</c>.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <param name="count">The capacity of <paramref name="properties"/>, replaced by the number found.</param>
    /// <param name="properties">Receives the VkQueueFamilyProperties.</param>
    [LibraryImport(Library, EntryPoint = "vkGetPhysicalDeviceQueueFamilyProperties")]
    internal static partial void GetPhysicalDeviceQueueFamilyProperties(ulong physicalDevice, uint* count, byte* properties);

    /// <summary>Native <c>vkEnumerateDeviceExtensionProperties</c>.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <param name="layerName">The layer, or null for the driver's own extensions.</param>
    /// <param name="count">The capacity of <paramref name="properties"/>, replaced by the number found.</param>
    /// <param name="properties">Receives the VkExtensionProperties.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkEnumerateDeviceExtensionProperties")]
    internal static partial int EnumerateDeviceExtensionProperties(ulong physicalDevice, byte* layerName, uint* count, byte* properties);

    /// <summary>Native <c>vkCreateDevice</c>.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <param name="createInfo">The creation info.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="device">Receives the device.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateDevice")]
    internal static partial int CreateDevice(ulong physicalDevice, VkDeviceCreateInfo* createInfo, void* allocator, ulong* device);

    /// <summary>Native <c>vkDestroyDevice</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyDevice")]
    internal static partial void DestroyDevice(ulong device, void* allocator);

    /// <summary>Native <c>vkGetDeviceQueue</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="queueFamily">The queue family.</param>
    /// <param name="queueIndex">The queue in the family.</param>
    /// <param name="queue">Receives the queue.</param>
    [LibraryImport(Library, EntryPoint = "vkGetDeviceQueue")]
    internal static partial void GetDeviceQueue(ulong device, uint queueFamily, uint queueIndex, ulong* queue);

    /// <summary>Native <c>vkDeviceWaitIdle</c>.</summary>
    /// <param name="device">The device.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkDeviceWaitIdle")]
    internal static partial int DeviceWaitIdle(ulong device);

    /// <summary>Native <c>vkGetPhysicalDeviceMemoryProperties</c>.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <param name="properties">Receives the VkPhysicalDeviceMemoryProperties, 520 bytes.</param>
    [LibraryImport(Library, EntryPoint = "vkGetPhysicalDeviceMemoryProperties")]
    internal static partial void GetPhysicalDeviceMemoryProperties(ulong physicalDevice, byte* properties);

    /// <summary>Native <c>vkAllocateMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkMemoryAllocateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="memory">Receives the memory.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkAllocateMemory")]
    internal static partial int AllocateMemory(ulong device, byte* info, void* allocator, ulong* memory);

    /// <summary>Native <c>vkFreeMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="memory">The memory.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkFreeMemory")]
    internal static partial void FreeMemory(ulong device, ulong memory, void* allocator);

    /// <summary>Native <c>vkMapMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="memory">The memory.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="size">The size.</param>
    /// <param name="flags">The flags, 0.</param>
    /// <param name="data">Receives the address.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkMapMemory")]
    internal static partial int MapMemory(ulong device, ulong memory, ulong offset, ulong size, uint flags, void** data);

    /// <summary>Native <c>vkUnmapMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="memory">The memory.</param>
    [LibraryImport(Library, EntryPoint = "vkUnmapMemory")]
    internal static partial void UnmapMemory(ulong device, ulong memory);

    /// <summary>Native <c>vkCreateBuffer</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkBufferCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateBuffer")]
    internal static partial int CreateBuffer(ulong device, byte* info, void* allocator, ulong* buffer);

    /// <summary>Native <c>vkDestroyBuffer</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyBuffer")]
    internal static partial void DestroyBuffer(ulong device, ulong buffer, void* allocator);

    /// <summary>Native <c>vkGetBufferMemoryRequirements</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="requirements">Receives the VkMemoryRequirements, 24 bytes.</param>
    [LibraryImport(Library, EntryPoint = "vkGetBufferMemoryRequirements")]
    internal static partial void GetBufferMemoryRequirements(ulong device, ulong buffer, byte* requirements);

    /// <summary>Native <c>vkBindBufferMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="memory">The memory.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkBindBufferMemory")]
    internal static partial int BindBufferMemory(ulong device, ulong buffer, ulong memory, ulong offset);

    /// <summary>Native <c>vkCreateImage</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkImageCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="image">Receives the image.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateImage")]
    internal static partial int CreateImage(ulong device, byte* info, void* allocator, ulong* image);

    /// <summary>Native <c>vkDestroyImage</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="image">The image.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyImage")]
    internal static partial void DestroyImage(ulong device, ulong image, void* allocator);

    /// <summary>Native <c>vkGetImageMemoryRequirements</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="image">The image.</param>
    /// <param name="requirements">Receives the VkMemoryRequirements, 24 bytes.</param>
    [LibraryImport(Library, EntryPoint = "vkGetImageMemoryRequirements")]
    internal static partial void GetImageMemoryRequirements(ulong device, ulong image, byte* requirements);

    /// <summary>Native <c>vkBindImageMemory</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="image">The image.</param>
    /// <param name="memory">The memory.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkBindImageMemory")]
    internal static partial int BindImageMemory(ulong device, ulong image, ulong memory, ulong offset);

    /// <summary>Native <c>vkCreateImageView</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkImageViewCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="view">Receives the view.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateImageView")]
    internal static partial int CreateImageView(ulong device, byte* info, void* allocator, ulong* view);

    /// <summary>Native <c>vkDestroyImageView</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="view">The view.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyImageView")]
    internal static partial void DestroyImageView(ulong device, ulong view, void* allocator);

    /// <summary>Native <c>vkCreateSampler</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkSamplerCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="sampler">Receives the sampler.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateSampler")]
    internal static partial int CreateSampler(ulong device, byte* info, void* allocator, ulong* sampler);

    /// <summary>Native <c>vkDestroySampler</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="sampler">The sampler.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroySampler")]
    internal static partial void DestroySampler(ulong device, ulong sampler, void* allocator);

    /// <summary>Native <c>vkCreateRenderPass</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkRenderPassCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="renderPass">Receives the render pass.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateRenderPass")]
    internal static partial int CreateRenderPass(ulong device, byte* info, void* allocator, ulong* renderPass);

    /// <summary>Native <c>vkDestroyRenderPass</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="renderPass">The render pass.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyRenderPass")]
    internal static partial void DestroyRenderPass(ulong device, ulong renderPass, void* allocator);

    /// <summary>Native <c>vkCreateFramebuffer</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkFramebufferCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="framebuffer">Receives the framebuffer.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateFramebuffer")]
    internal static partial int CreateFramebuffer(ulong device, byte* info, void* allocator, ulong* framebuffer);

    /// <summary>Native <c>vkDestroyFramebuffer</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="framebuffer">The framebuffer.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyFramebuffer")]
    internal static partial void DestroyFramebuffer(ulong device, ulong framebuffer, void* allocator);

    /// <summary>Native <c>vkCreateShaderModule</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkShaderModuleCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="module">Receives the module.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateShaderModule")]
    internal static partial int CreateShaderModule(ulong device, byte* info, void* allocator, ulong* module);

    /// <summary>Native <c>vkDestroyShaderModule</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="module">The module.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyShaderModule")]
    internal static partial void DestroyShaderModule(ulong device, ulong module, void* allocator);

    /// <summary>Native <c>vkCreateDescriptorSetLayout</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkDescriptorSetLayoutCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="layout">Receives the layout.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateDescriptorSetLayout")]
    internal static partial int CreateDescriptorSetLayout(ulong device, byte* info, void* allocator, ulong* layout);

    /// <summary>Native <c>vkDestroyDescriptorSetLayout</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyDescriptorSetLayout")]
    internal static partial void DestroyDescriptorSetLayout(ulong device, ulong layout, void* allocator);

    /// <summary>Native <c>vkCreatePipelineLayout</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkPipelineLayoutCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="layout">Receives the layout.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreatePipelineLayout")]
    internal static partial int CreatePipelineLayout(ulong device, byte* info, void* allocator, ulong* layout);

    /// <summary>Native <c>vkDestroyPipelineLayout</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyPipelineLayout")]
    internal static partial void DestroyPipelineLayout(ulong device, ulong layout, void* allocator);

    /// <summary>Native <c>vkCreateDescriptorPool</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkDescriptorPoolCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="pool">Receives the pool.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateDescriptorPool")]
    internal static partial int CreateDescriptorPool(ulong device, byte* info, void* allocator, ulong* pool);

    /// <summary>Native <c>vkDestroyDescriptorPool</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyDescriptorPool")]
    internal static partial void DestroyDescriptorPool(ulong device, ulong pool, void* allocator);

    /// <summary>Native <c>vkAllocateDescriptorSets</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkDescriptorSetAllocateInfo.</param>
    /// <param name="sets">Receives the sets.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkAllocateDescriptorSets")]
    internal static partial int AllocateDescriptorSets(ulong device, byte* info, ulong* sets);

    /// <summary>Native <c>vkFreeDescriptorSets</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="count">The number of sets.</param>
    /// <param name="sets">The sets.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkFreeDescriptorSets")]
    internal static partial int FreeDescriptorSets(ulong device, ulong pool, uint count, ulong* sets);

    /// <summary>Native <c>vkUpdateDescriptorSets</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="writeCount">The number of writes.</param>
    /// <param name="writes">The VkWriteDescriptorSet array.</param>
    /// <param name="copyCount">The number of copies, 0.</param>
    /// <param name="copies">The copies, null.</param>
    [LibraryImport(Library, EntryPoint = "vkUpdateDescriptorSets")]
    internal static partial void UpdateDescriptorSets(ulong device, uint writeCount, byte* writes, uint copyCount, byte* copies);

    /// <summary>Native <c>vkCreateGraphicsPipelines</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="cache">The pipeline cache, or zero.</param>
    /// <param name="count">The number of pipelines.</param>
    /// <param name="infos">The VkGraphicsPipelineCreateInfo array.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="pipelines">Receives the pipelines.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateGraphicsPipelines")]
    internal static partial int CreateGraphicsPipelines(ulong device, ulong cache, uint count, byte* infos, void* allocator, ulong* pipelines);

    /// <summary>Native <c>vkDestroyPipeline</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyPipeline")]
    internal static partial void DestroyPipeline(ulong device, ulong pipeline, void* allocator);

    /// <summary>Native <c>vkCreateCommandPool</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkCommandPoolCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="pool">Receives the pool.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateCommandPool")]
    internal static partial int CreateCommandPool(ulong device, byte* info, void* allocator, ulong* pool);

    /// <summary>Native <c>vkDestroyCommandPool</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyCommandPool")]
    internal static partial void DestroyCommandPool(ulong device, ulong pool, void* allocator);

    /// <summary>Native <c>vkAllocateCommandBuffers</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkCommandBufferAllocateInfo.</param>
    /// <param name="buffers">Receives the command buffers.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkAllocateCommandBuffers")]
    internal static partial int AllocateCommandBuffers(ulong device, byte* info, ulong* buffers);

    /// <summary>Native <c>vkResetCommandPool</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="pool">The pool.</param>
    /// <param name="flags">The flags, 0.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkResetCommandPool")]
    internal static partial int ResetCommandPool(ulong device, ulong pool, uint flags);

    /// <summary>Native <c>vkBeginCommandBuffer</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="info">The VkCommandBufferBeginInfo.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkBeginCommandBuffer")]
    internal static partial int BeginCommandBuffer(ulong commandBuffer, byte* info);

    /// <summary>Native <c>vkEndCommandBuffer</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkEndCommandBuffer")]
    internal static partial int EndCommandBuffer(ulong commandBuffer);

    /// <summary>Native <c>vkCmdPipelineBarrier</c> with image barriers only.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="sourceStage">The source stage mask.</param>
    /// <param name="destinationStage">The destination stage mask.</param>
    /// <param name="dependencyFlags">The dependency flags, 0.</param>
    /// <param name="memoryBarrierCount">The memory barrier count, 0.</param>
    /// <param name="memoryBarriers">The memory barriers, null.</param>
    /// <param name="bufferBarrierCount">The buffer barrier count, 0.</param>
    /// <param name="bufferBarriers">The buffer barriers, null.</param>
    /// <param name="imageBarrierCount">The image barrier count.</param>
    /// <param name="imageBarriers">The VkImageMemoryBarrier array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdPipelineBarrier")]
    internal static partial void CmdPipelineBarrier(
        ulong commandBuffer,
        uint sourceStage,
        uint destinationStage,
        uint dependencyFlags,
        uint memoryBarrierCount,
        byte* memoryBarriers,
        uint bufferBarrierCount,
        byte* bufferBarriers,
        uint imageBarrierCount,
        byte* imageBarriers);

    /// <summary>Native <c>vkCmdCopyBufferToImage</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="image">The destination image.</param>
    /// <param name="imageLayout">The image layout.</param>
    /// <param name="regionCount">The number of regions.</param>
    /// <param name="regions">The VkBufferImageCopy array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdCopyBufferToImage")]
    internal static partial void CmdCopyBufferToImage(ulong commandBuffer, ulong buffer, ulong image, uint imageLayout, uint regionCount, byte* regions);

    /// <summary>Native <c>vkCmdCopyImageToBuffer</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="image">The source image.</param>
    /// <param name="imageLayout">The image layout.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="regionCount">The number of regions.</param>
    /// <param name="regions">The VkBufferImageCopy array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdCopyImageToBuffer")]
    internal static partial void CmdCopyImageToBuffer(ulong commandBuffer, ulong image, uint imageLayout, ulong buffer, uint regionCount, byte* regions);

    /// <summary>Native <c>vkCmdBeginRenderPass</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="info">The VkRenderPassBeginInfo.</param>
    /// <param name="contents">The subpass contents, 0 for inline.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdBeginRenderPass")]
    internal static partial void CmdBeginRenderPass(ulong commandBuffer, byte* info, uint contents);

    /// <summary>Native <c>vkCmdEndRenderPass</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdEndRenderPass")]
    internal static partial void CmdEndRenderPass(ulong commandBuffer);

    /// <summary>Native <c>vkCmdBindPipeline</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="bindPoint">The bind point, 0 for graphics.</param>
    /// <param name="pipeline">The pipeline.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdBindPipeline")]
    internal static partial void CmdBindPipeline(ulong commandBuffer, uint bindPoint, ulong pipeline);

    /// <summary>Native <c>vkCmdBindDescriptorSets</c> with no dynamic offsets.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="bindPoint">The bind point, 0 for graphics.</param>
    /// <param name="layout">The pipeline layout.</param>
    /// <param name="firstSet">The first set number.</param>
    /// <param name="setCount">The number of sets.</param>
    /// <param name="sets">The sets.</param>
    /// <param name="dynamicOffsetCount">The dynamic offset count, 0.</param>
    /// <param name="dynamicOffsets">The dynamic offsets, null.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdBindDescriptorSets")]
    internal static partial void CmdBindDescriptorSets(
        ulong commandBuffer,
        uint bindPoint,
        ulong layout,
        uint firstSet,
        uint setCount,
        ulong* sets,
        uint dynamicOffsetCount,
        uint* dynamicOffsets);

    /// <summary>Native <c>vkCmdPushConstants</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="layout">The pipeline layout.</param>
    /// <param name="stages">The stages that read the constants.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="size">The size.</param>
    /// <param name="values">The values.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdPushConstants")]
    internal static partial void CmdPushConstants(ulong commandBuffer, ulong layout, uint stages, uint offset, uint size, byte* values);

    /// <summary>Native <c>vkCmdSetViewport</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="first">The first viewport.</param>
    /// <param name="count">The number of viewports.</param>
    /// <param name="viewports">The VkViewport array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdSetViewport")]
    internal static partial void CmdSetViewport(ulong commandBuffer, uint first, uint count, byte* viewports);

    /// <summary>Native <c>vkCmdSetScissor</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="first">The first scissor.</param>
    /// <param name="count">The number of scissors.</param>
    /// <param name="scissors">The VkRect2D array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdSetScissor")]
    internal static partial void CmdSetScissor(ulong commandBuffer, uint first, uint count, byte* scissors);

    /// <summary>Native <c>vkCmdDraw</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="vertexCount">The vertex count.</param>
    /// <param name="instanceCount">The instance count.</param>
    /// <param name="firstVertex">The first vertex.</param>
    /// <param name="firstInstance">The first instance.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdDraw")]
    internal static partial void CmdDraw(ulong commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    /// <summary>Native <c>vkCmdClearAttachments</c>.</summary>
    /// <param name="commandBuffer">The command buffer.</param>
    /// <param name="attachmentCount">The number of attachments.</param>
    /// <param name="attachments">The VkClearAttachment array.</param>
    /// <param name="rectCount">The number of rectangles.</param>
    /// <param name="rects">The VkClearRect array.</param>
    [LibraryImport(Library, EntryPoint = "vkCmdClearAttachments")]
    internal static partial void CmdClearAttachments(ulong commandBuffer, uint attachmentCount, byte* attachments, uint rectCount, byte* rects);

    /// <summary>Native <c>vkQueueSubmit</c>.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="count">The number of submits.</param>
    /// <param name="submits">The VkSubmitInfo array.</param>
    /// <param name="fence">The fence signalled when the work finishes.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkQueueSubmit")]
    internal static partial int QueueSubmit(ulong queue, uint count, byte* submits, ulong fence);

    /// <summary>Native <c>vkCreateFence</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="info">The VkFenceCreateInfo.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    /// <param name="fence">Receives the fence.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkCreateFence")]
    internal static partial int CreateFence(ulong device, byte* info, void* allocator, ulong* fence);

    /// <summary>Native <c>vkDestroyFence</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="fence">The fence.</param>
    /// <param name="allocator">The allocation callbacks, or null.</param>
    [LibraryImport(Library, EntryPoint = "vkDestroyFence")]
    internal static partial void DestroyFence(ulong device, ulong fence, void* allocator);

    /// <summary>Native <c>vkWaitForFences</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="count">The number of fences.</param>
    /// <param name="fences">The fences.</param>
    /// <param name="waitAll">1 to wait for all of them.</param>
    /// <param name="timeout">The timeout in nanoseconds.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkWaitForFences")]
    internal static partial int WaitForFences(ulong device, uint count, ulong* fences, uint waitAll, ulong timeout);

    /// <summary>Native <c>vkResetFences</c>.</summary>
    /// <param name="device">The device.</param>
    /// <param name="count">The number of fences.</param>
    /// <param name="fences">The fences.</param>
    /// <returns>A VkResult.</returns>
    [LibraryImport(Library, EntryPoint = "vkResetFences")]
    internal static partial int ResetFences(ulong device, uint count, ulong* fences);
}
