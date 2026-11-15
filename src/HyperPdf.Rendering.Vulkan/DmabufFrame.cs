// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>One GPU image the backend draws finished frames into, shared with the window system as a dmabuf file descriptor.</summary>
/// <param name="Descriptor">The dmabuf file descriptor, which the receiver owns and closes.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Stride">The bytes from one line to the next.</param>
/// <param name="Offset">The bytes before the first line.</param>
[DebuggerDisplay("DmabufFrame: {Width}x{Height}, stride {Stride}")]
public readonly record struct DmabufFrame(int Descriptor, int Width, int Height, int Stride, int Offset);
