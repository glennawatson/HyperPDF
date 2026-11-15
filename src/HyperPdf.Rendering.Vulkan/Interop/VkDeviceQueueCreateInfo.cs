// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>The VkDeviceQueueCreateInfo structure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct VkDeviceQueueCreateInfo
{
    /// <summary>Gets the structure type.</summary>
    internal uint SType { get; init; }

    /// <summary>Gets the extension chain.</summary>
    internal ulong PNext { get; init; }

    /// <summary>Gets the creation flags.</summary>
    internal uint Flags { get; init; }

    /// <summary>Gets the queue family index.</summary>
    internal uint QueueFamilyIndex { get; init; }

    /// <summary>Gets the number of queues.</summary>
    internal uint QueueCount { get; init; }

    /// <summary>Gets the address of the queue priorities.</summary>
    internal ulong QueuePriorities { get; init; }
}
