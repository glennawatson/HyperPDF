// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Well-formed PDF graphics cases with independently derived interior pixels.</summary>
internal enum StandardsRenderCase
{
    /// <summary>Same-direction nested paths use nonzero winding.</summary>
    NonzeroFill = 0,

    /// <summary>Even-odd filling leaves the nested rectangle empty.</summary>
    EvenOddFill = 1,

    /// <summary>Clipping limits painting and restoring graphics state removes that clip.</summary>
    Clipping = 2,

    /// <summary>The current transformation matrix translates paths and Q restores it.</summary>
    Transform = 3,

    /// <summary>Normal blending composes half-opacity red over blue.</summary>
    NormalOpacity = 4,

    /// <summary>Multiply blending composes opaque blue over red into black.</summary>
    Multiply = 5,

    /// <summary>A form's bounding box clips its painted paths.</summary>
    FormBoundingBox = 6,

    /// <summary>A non-isolated Multiply group blends against the page backdrop.</summary>
    NonIsolatedMultiply = 7,
}
