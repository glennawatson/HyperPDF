// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>How the smooth sampler walks along one source row.</summary>
/// <param name="Start">The 16.16 source column position of the first pixel.</param>
/// <param name="Step">The 16.16 source step per destination pixel.</param>
[DebuggerDisplay("SampleWalk {Start} + {Step}")]
internal readonly record struct SampleWalk(int Start, int Step);
