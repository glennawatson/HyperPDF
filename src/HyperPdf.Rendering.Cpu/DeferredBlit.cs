// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>A surface whose drawing is held back until the fill under it has been dealt with.</summary>
/// <param name="Surface">The surface.</param>
/// <param name="Request">Where it goes and how.</param>
[DebuggerDisplay("DeferredBlit: {Request}")]
internal readonly record struct DeferredBlit(CpuPixelSurface Surface, BlitRequest Request);
