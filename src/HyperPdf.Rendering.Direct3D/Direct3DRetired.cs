// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>The GPU objects of a disposed tile, kept until the render thread releases them.</summary>
/// <param name="Texture">The ID3D11Texture2D.</param>
/// <param name="View">The ID3D11ShaderResourceView.</param>
[DebuggerDisplay("Direct3DRetired {Texture}")]
internal readonly record struct Direct3DRetired(ulong Texture, ulong View);
