// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The GPU the context picked.</summary>
/// <param name="PhysicalDevice">The physical device handle.</param>
/// <param name="Name">The driver's name for the GPU.</param>
/// <param name="QueueFamily">The first queue family that can draw.</param>
/// <param name="Score">3 for a discrete GPU, 2 for an integrated one and 1 for anything else.</param>
/// <param name="CanExportDmabuf">Whether the GPU has the extensions that export memory as a dmabuf.</param>
[DebuggerDisplay("GpuSelection {Name}, score {Score}")]
internal readonly record struct GpuSelection(ulong PhysicalDevice, string Name, uint QueueFamily, int Score, bool CanExportDmabuf);
