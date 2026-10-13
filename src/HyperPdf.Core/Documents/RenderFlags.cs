// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Options controlling page rasterisation.</summary>
[Flags]
public enum RenderFlags
{
    /// <summary>Default rendering.</summary>
    None = 0,

    /// <summary>Draw annotations and form widgets.</summary>
    Annotations = 1 << 0,

    /// <summary>Render in grayscale.</summary>
    Grayscale = 1 << 1,

    /// <summary>Optimise for printing.</summary>
    Printing = 1 << 3,
}
