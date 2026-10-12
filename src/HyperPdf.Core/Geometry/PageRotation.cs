// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Geometry;

/// <summary>Clockwise page rotation in quarter turns.</summary>
public enum PageRotation
{
    /// <summary>No rotation.</summary>
    None = 0,

    /// <summary>Rotated 90 degrees clockwise.</summary>
    Rotate90 = 1,

    /// <summary>Rotated 180 degrees.</summary>
    Rotate180 = 2,

    /// <summary>Rotated 270 degrees clockwise.</summary>
    Rotate270 = 3,
}
