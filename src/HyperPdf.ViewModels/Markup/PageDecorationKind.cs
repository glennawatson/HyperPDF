// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Markup;

/// <summary>What is added around the content of the pages of a document.</summary>
public enum PageDecorationKind
{
    /// <summary>Large text over or under the page, such as CONFIDENTIAL or DRAFT.</summary>
    Watermark = 0,

    /// <summary>Text along the top edge of each page.</summary>
    Header = 1,

    /// <summary>Text along the bottom edge of each page.</summary>
    Footer = 2,

    /// <summary>The number of each page, written along the bottom edge.</summary>
    PageNumbers = 3,

    /// <summary>A running number with a prefix on each page, as legal documents are numbered.</summary>
    Bates = 4,

    /// <summary>A colour filled behind the page.</summary>
    Background = 5,

    /// <summary>Taking margins off the pages, so less of each page is shown.</summary>
    Crop = 6,
}
