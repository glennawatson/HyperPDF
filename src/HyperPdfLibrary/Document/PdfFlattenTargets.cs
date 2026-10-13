// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Which kinds of annotation flattening works on.</summary>
public enum PdfFlattenTargets
{
    /// <summary>Every annotation, form field widgets and links included.</summary>
    All = 0,

    /// <summary>Only form field widgets, so the page keeps its notes, markup and links.</summary>
    WidgetsOnly = 1,

    /// <summary>Only markup annotations such as highlights, stamps and notes, so the page keeps its form and links.</summary>
    MarkupOnly = 2,

    /// <summary>Every annotation except links and popups, so the page keeps its links.</summary>
    WithoutLinks = 3,
}
