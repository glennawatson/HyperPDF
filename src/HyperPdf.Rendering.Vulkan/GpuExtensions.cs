// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The optional device extensions a GPU has.</summary>
/// <param name="Dmabuf">Whether it can export memory as a dmabuf.</param>
/// <param name="Swapchain">Whether it can present images to a window.</param>
[DebuggerDisplay("GpuExtensions dmabuf {Dmabuf}, swapchain {Swapchain}")]
internal readonly record struct GpuExtensions(bool Dmabuf, bool Swapchain);
