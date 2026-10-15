// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Whether strokes are fitted to whole pixels (automatic stroke adjustment, ISO 32000-2 10.7.5).</summary>
public enum PdfStrokeAdjustment
{
    /// <summary>Lines are never fitted to whole pixels.</summary>
    Off = 0,

    /// <summary>Lines are fitted where the page turns stroke adjustment on with <c>/SA</c>.</summary>
    Document = 1,

    /// <summary>Lines are fitted whatever the page asks.</summary>
    Always = 2,
}
