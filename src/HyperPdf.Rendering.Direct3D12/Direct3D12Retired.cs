// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>The GPU texture of a disposed tile, kept until the render thread releases it.</summary>
/// <param name="Texture">The ID3D12Resource.</param>
[DebuggerDisplay("Direct3D12Retired {Texture}")]
internal readonly record struct Direct3D12Retired(ulong Texture);
