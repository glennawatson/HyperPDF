// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>Native <c>pa_buffer_attr</c>: how much audio the server buffers. <see cref="uint.MaxValue"/> keeps a default.</summary>
/// <param name="MaxLength">The most bytes buffered.</param>
/// <param name="TargetLength">The bytes the server aims to keep buffered, which sets the latency.</param>
/// <param name="Prebuffer">The bytes buffered before playing starts.</param>
/// <param name="MinimumRequest">The fewest bytes requested at a time.</param>
/// <param name="FragmentSize">The fragment size, for recording only.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PulseBufferAttributes(uint MaxLength, uint TargetLength, uint Prebuffer, uint MinimumRequest, uint FragmentSize);
