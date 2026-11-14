// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Rendering;

/// <summary>What the user or settings ask the head to use for drawing.</summary>
public enum RenderBackendPreference
{
    /// <summary>Use the GPU when the head can start one, and the CPU otherwise. This is the default.</summary>
    Gpu = 0,

    /// <summary>Always use the CPU backend.</summary>
    Cpu = 1,
}
