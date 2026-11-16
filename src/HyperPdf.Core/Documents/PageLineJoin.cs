// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>How the corners of a line of a page object are drawn.</summary>
public enum PageLineJoin
{
    /// <summary>Pointed, as a sharp corner.</summary>
    Miter = 0,

    /// <summary>Rounded, as a circle.</summary>
    Round = 1,

    /// <summary>Cut off square across the corner.</summary>
    Bevel = 2,
}
