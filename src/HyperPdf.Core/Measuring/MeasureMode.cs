// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Measuring;

/// <summary>What the measuring tool measures.</summary>
public enum MeasureMode
{
    /// <summary>The straight distance between two points.</summary>
    Distance = 0,

    /// <summary>The length of a path through several points.</summary>
    Perimeter = 1,

    /// <summary>The area enclosed by several points.</summary>
    Area = 2,
}
