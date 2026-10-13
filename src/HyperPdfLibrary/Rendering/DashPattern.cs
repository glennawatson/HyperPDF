// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace HyperPdfLibrary.Rendering;

/// <summary>The dash lengths of one dash array, kept so a graphics state that sets it again reuses them.</summary>
/// <param name="Lengths">The lengths; empty for a solid line.</param>
internal sealed record DashPattern(ImmutableArray<float> Lengths);
