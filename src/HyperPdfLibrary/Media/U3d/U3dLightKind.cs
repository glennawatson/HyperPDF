// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The kind of a light resource (ECMA-363 9.8.1.3).</summary>
public enum U3dLightKind
{
    /// <summary>Light that reaches every surface equally.</summary>
    Ambient = 0,

    /// <summary>Light that arrives from one direction.</summary>
    Directional = 1,

    /// <summary>Light that spreads from a point.</summary>
    Point = 2,

    /// <summary>Light from a point, limited to a cone.</summary>
    Spot = 3,
}
