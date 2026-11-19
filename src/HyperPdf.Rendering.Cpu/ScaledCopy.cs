// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>What is kept about one smooth scaling of a surface.</summary>
/// <param name="Pixels">The scaled pixels from the pool, or <see langword="null"/> before the scaling is made.</param>
/// <param name="Version">The version of the surface's pixels the scaling was made at.</param>
/// <param name="Frame">The frame the scaling was last drawn in.</param>
/// <param name="Confirmed">Whether more than one frame drew the scaling, so it is kept for a while when it goes off screen.</param>
[DebuggerDisplay("ScaledCopy: version {Version}, frame {Frame}")]
internal readonly record struct ScaledCopy(byte[]? Pixels, int Version, int Frame, bool Confirmed);
