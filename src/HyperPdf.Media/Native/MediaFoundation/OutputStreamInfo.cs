// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>What a transform needs to give a picture (<c>MFT_OUTPUT_STREAM_INFO</c>). The transform writes it through the pointer it is given.</summary>
/// <param name="Flags">Flags about the output, such as whether the transform supplies its own memory.</param>
/// <param name="Size">The size of the memory the transform needs for a picture, in bytes.</param>
/// <param name="Alignment">The alignment the memory needs, in bytes.</param>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("OutputStreamInfo: {Size} bytes")]
internal readonly record struct OutputStreamInfo(uint Flags, uint Size, uint Alignment);
