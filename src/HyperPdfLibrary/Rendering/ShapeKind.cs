// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The shapes a <see cref="ShapePlan"/> draws.</summary>
internal enum ShapeKind
{
    /// <summary>Not a shape subtype.</summary>
    None = 0,

    /// <summary>A rectangle (<c>/Square</c>).</summary>
    Square = 1,

    /// <summary>An ellipse (<c>/Circle</c>).</summary>
    Circle = 2,

    /// <summary>A closed path through <c>/Vertices</c> (<c>/Polygon</c>).</summary>
    Polygon = 3,

    /// <summary>An open path through <c>/Vertices</c> (<c>/PolyLine</c>).</summary>
    PolyLine = 4,

    /// <summary>A line between two points (<c>/Line</c>).</summary>
    Line = 5,
}
