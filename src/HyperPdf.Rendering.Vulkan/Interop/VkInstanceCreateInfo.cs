// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>The VkInstanceCreateInfo structure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct VkInstanceCreateInfo
{
    /// <summary>Gets the structure type.</summary>
    internal uint SType { get; init; }

    /// <summary>Gets the extension chain.</summary>
    internal ulong PNext { get; init; }

    /// <summary>Gets the creation flags.</summary>
    internal uint Flags { get; init; }

    /// <summary>Gets the address of the application info.</summary>
    internal ulong ApplicationInfo { get; init; }

    /// <summary>Gets the number of layers.</summary>
    internal uint EnabledLayerCount { get; init; }

    /// <summary>Gets the address of the layer names.</summary>
    internal ulong EnabledLayerNames { get; init; }

    /// <summary>Gets the number of extensions.</summary>
    internal uint EnabledExtensionCount { get; init; }

    /// <summary>Gets the address of the extension names.</summary>
    internal ulong EnabledExtensionNames { get; init; }
}
