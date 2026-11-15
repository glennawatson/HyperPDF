// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>What a new page object is.</summary>
public enum PageObjectDraftKind
{
    /// <summary>A rectangle.</summary>
    Rectangle = 0,

    /// <summary>An ellipse that fills its box.</summary>
    Ellipse = 1,

    /// <summary>A straight line from the top left to the bottom right of its box.</summary>
    Line = 2,

    /// <summary>A line of text.</summary>
    Text = 3,
}
