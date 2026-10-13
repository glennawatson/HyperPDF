// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>PDFium's <c>PDFACTION_*</c> action types, plus a value for a link that has no action.</summary>
internal enum PdfiumActionType
{
    /// <summary>The link has no <c>/A</c> action; it may still have a <c>/Dest</c>.</summary>
    NoAction = -1,

    /// <summary>An action type PDFium does not classify, such as JavaScript or Named.</summary>
    Unsupported = 0,

    /// <summary>A GoTo action within this document.</summary>
    GoTo = 1,

    /// <summary>A GoToR action into another file.</summary>
    RemoteGoTo = 2,

    /// <summary>A URI action.</summary>
    Uri = 3,

    /// <summary>A Launch action.</summary>
    Launch = 4,

    /// <summary>A GoToE action into an embedded file.</summary>
    EmbeddedGoTo = 5,
}
