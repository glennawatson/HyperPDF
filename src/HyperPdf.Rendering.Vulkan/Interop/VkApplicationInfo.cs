// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>The VkApplicationInfo structure. Pointers are held as 64 bit addresses because the head targets 64 bit systems.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct VkApplicationInfo
{
    /// <summary>Gets the structure type.</summary>
    internal uint SType { get; init; }

    /// <summary>Gets the extension chain.</summary>
    internal ulong PNext { get; init; }

    /// <summary>Gets the UTF-8 application name.</summary>
    internal ulong ApplicationName { get; init; }

    /// <summary>Gets the application version.</summary>
    internal uint ApplicationVersion { get; init; }

    /// <summary>Gets the UTF-8 engine name.</summary>
    internal ulong EngineName { get; init; }

    /// <summary>Gets the engine version.</summary>
    internal uint EngineVersion { get; init; }

    /// <summary>Gets the Vulkan version the application targets.</summary>
    internal uint ApiVersion { get; init; }
}
