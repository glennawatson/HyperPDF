// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Layers;

/// <summary>Which layers of the order a layer panel lists (ISO 32000-2, 8.11.4.3, Table 99 /ListMode).</summary>
public enum PdfLayerListMode
{
    /// <summary>List every layer of the order.</summary>
    AllPages = 0,

    /// <summary>List only the layers that visible pages use.</summary>
    VisiblePages = 1,
}
