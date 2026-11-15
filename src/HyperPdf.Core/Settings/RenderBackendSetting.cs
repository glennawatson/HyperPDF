// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>Which way the window draws, as the user chose it. A choice the system cannot use makes the window fall back to the next best.</summary>
public enum RenderBackendSetting
{
    /// <summary>The viewer picks: the best graphics interface the system has, then the processor.</summary>
    Automatic = 0,

    /// <summary>Direct3D 11, on Windows.</summary>
    Direct3D11 = 1,

    /// <summary>Direct3D 12, on Windows.</summary>
    Direct3D12 = 2,

    /// <summary>Vulkan, on Windows and Linux.</summary>
    Vulkan = 3,

    /// <summary>The processor, with no graphics interface.</summary>
    Processor = 4,
}
