// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>What a piece of page content is, as far as someone editing the page is concerned.</summary>
public enum PageObjectKind
{
    /// <summary>A run of text.</summary>
    Text = 0,

    /// <summary>A picture.</summary>
    Image = 1,

    /// <summary>A line, shape or other drawing.</summary>
    Drawing = 2,

    /// <summary>A gradient fill or an embedded group of content.</summary>
    Other = 3,
}
