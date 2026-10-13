// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The vector width <see cref="CmykConverter"/> uses for a row.</summary>
internal enum CmykLaneWidth
{
    /// <summary>One pixel at a time.</summary>
    Scalar = 0,

    /// <summary>Four pixels per 128-bit vector, then the scalar tail.</summary>
    Vector128 = 1,

    /// <summary>Eight pixels per 256-bit vector, then the scalar tail.</summary>
    Vector256 = 2,

    /// <summary>Sixteen pixels per 512-bit vector, then the scalar tail.</summary>
    Vector512 = 3,

    /// <summary>The widest vector the hardware accelerates, then narrower vectors, then the scalar tail.</summary>
    Widest = 4,
}
