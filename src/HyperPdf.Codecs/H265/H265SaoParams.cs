// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Names how the offset parameters of a coding tree block are laid out in a flat array: per colour component the type, the band position or edge class, and four offsets.</summary>
internal static class H265SaoParams
{
    /// <summary>The colour components that have parameters.</summary>
    internal const int Components = 3;

    /// <summary>The offset type of a component that has no offset.</summary>
    internal const int None = 0;

    /// <summary>The offset type that adds an offset by band.</summary>
    internal const int Band = 1;

    /// <summary>The offset type that adds an offset by edge shape.</summary>
    internal const int Edge = 2;

    /// <summary>The first of the offsets that edge offset makes negative.</summary>
    internal const int FirstNegativeEdgeOffset = 2;

    /// <summary>The values kept for one colour component.</summary>
    internal const int IntsPerComponent = 6;

    /// <summary>The values kept for one coding tree block.</summary>
    internal const int IntsPerCtb = IntsPerComponent * Components;

    /// <summary>The offset in a component's values of the band position or edge class.</summary>
    internal const int ClassOffset = 1;

    /// <summary>The offset in a component's values of the first of four offsets.</summary>
    internal const int OffsetsOffset = 2;

    /// <summary>The offsets a component has.</summary>
    internal const int OffsetCount = 4;
}
