// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>A growing bounding box in PDF user space, built while points are converted.</summary>
[DebuggerDisplay("UserBounds: {_min} – {_max}")]
internal record struct UserBounds
{
    /// <summary>The lower-left corner.</summary>
    private Vector2 _min;

    /// <summary>The upper-right corner.</summary>
    private Vector2 _max;

    /// <summary>Gets a value indicating whether a point has been added.</summary>
    internal bool IsSet { get; private set; }

    /// <summary>Adds a point.</summary>
    /// <param name="point">The point.</param>
    internal void Add(Vector2 point)
    {
        _min = IsSet ? Vector2.Min(_min, point) : point;
        _max = IsSet ? Vector2.Max(_max, point) : point;
        IsSet = true;
    }

    /// <summary>Adds a rectangle's corners.</summary>
    /// <param name="rectangle">The rectangle.</param>
    internal void AddRectangle(PdfRectangle rectangle)
    {
        Add(new(rectangle.Left, rectangle.Bottom));
        Add(new(rectangle.Right, rectangle.Top));
    }

    /// <summary>Gets the box grown on every side by a margin.</summary>
    /// <param name="margin">The margin, for example a line width.</param>
    /// <returns>The rectangle.</returns>
    internal readonly PdfRectangle ToRectangle(float margin) => new(_min.X - margin, _min.Y - margin, _max.X + margin, _max.Y + margin);
}
