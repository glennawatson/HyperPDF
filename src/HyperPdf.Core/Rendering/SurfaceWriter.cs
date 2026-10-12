// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Rendering;

/// <summary>Writes pixels into a locked surface.</summary>
/// <typeparam name="TState">The state passed through to the writer, avoiding closure allocations.</typeparam>
/// <param name="target">The locked pixel buffer.</param>
/// <param name="state">The caller supplied state.</param>
/// <returns><see langword="true"/> when the surface now holds valid content.</returns>
public delegate bool SurfaceWriter<TState>(RenderTarget target, in TState state);
