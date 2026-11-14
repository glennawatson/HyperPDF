// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The GPU objects of a disposed tile, kept until the frame that may still use them has finished.</summary>
/// <param name="Image">The tile image, or <see langword="null"/> when it never reached the GPU.</param>
/// <param name="NearestSet">The descriptor set with the nearest sampler, or zero.</param>
/// <param name="LinearSet">The descriptor set with the linear sampler, or zero.</param>
[DebuggerDisplay("VulkanRetired {Image}")]
internal readonly record struct VulkanRetired(VulkanImage? Image, ulong NearestSet, ulong LinearSet);
