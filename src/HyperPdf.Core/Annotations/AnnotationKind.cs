// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>The kinds of annotation the viewer shows and creates.</summary>
public enum AnnotationKind
{
    /// <summary>Another kind the viewer shows but does not edit, for example a shape from another program.</summary>
    Other = 0,

    /// <summary>A highlight over text.</summary>
    Highlight = 1,

    /// <summary>A line under text.</summary>
    Underline = 2,

    /// <summary>A line through text.</summary>
    StrikeOut = 3,

    /// <summary>A wavy line under text.</summary>
    Squiggly = 4,

    /// <summary>A freehand drawing.</summary>
    Ink = 5,

    /// <summary>A sticky note.</summary>
    Note = 6,

    /// <summary>Text written on the page.</summary>
    TextBox = 7,

    /// <summary>A drawn or typed signature.</summary>
    Signature = 8,

    /// <summary>A rectangle drawn on the page.</summary>
    Rectangle = 9,

    /// <summary>An ellipse drawn on the page.</summary>
    Ellipse = 10,

    /// <summary>An arrow drawn on the page, pointing at its end.</summary>
    Arrow = 11,

    /// <summary>A straight line drawn on the page.</summary>
    Line = 12,

    /// <summary>A stamp such as "Approved" or "Draft", the person's own words, or a picture.</summary>
    Stamp = 13,

    /// <summary>A text box with a line pointing at something on the page.</summary>
    Callout = 14,

    /// <summary>A closed shape of straight sides drawn point by point.</summary>
    Polygon = 15,

    /// <summary>A polygon with a scalloped, cloud-like edge, used to ring an area for review.</summary>
    Cloud = 16,

    /// <summary>An open run of straight lines drawn point by point.</summary>
    PolyLine = 17,

    /// <summary>An area marked for redaction: it is removed for good when the redactions are applied.</summary>
    Redaction = 18,
}
