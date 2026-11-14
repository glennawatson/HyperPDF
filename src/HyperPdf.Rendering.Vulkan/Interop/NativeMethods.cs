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
}
