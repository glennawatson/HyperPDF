// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Which layers of the tree a layer panel lists.</summary>
public enum DocumentLayerListMode
{
    /// <summary>List every layer.</summary>
    AllPages = 0,

    /// <summary>List only the layers that the visible pages use.</summary>
    VisiblePages = 1,
}
