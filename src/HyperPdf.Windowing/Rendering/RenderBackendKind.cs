// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Rendering;

/// <summary>The ways a platform head can put page tiles on screen.</summary>
public enum RenderBackendKind
{
    /// <summary>Managed pixel buffers copied to the window by the operating system. Works everywhere.</summary>
    Cpu = 0,

    /// <summary>Direct3D 12 on Windows.</summary>
    Direct3D12 = 1,

    /// <summary>Metal on macOS.</summary>
    Metal = 2,

    /// <summary>Vulkan on Linux and Windows.</summary>
    Vulkan = 3,

    /// <summary>OpenGL, the older route on Linux when Vulkan is missing.</summary>
    OpenGL = 4,
}
