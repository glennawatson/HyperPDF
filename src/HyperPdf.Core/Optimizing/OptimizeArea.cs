// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Optimizing;

/// <summary>The kind of change an optimisation makes, used to say what was left alone.</summary>
public enum OptimizeArea
{
    /// <summary>How the file is laid out.</summary>
    Layout = 0,

    /// <summary>Pictures scaled down or compressed.</summary>
    Images = 1,

    /// <summary>Stream compression.</summary>
    Streams = 2,

    /// <summary>Identical objects merged.</summary>
    Duplicates = 3,

    /// <summary>Objects nothing uses.</summary>
    UnusedObjects = 4,

    /// <summary>Embedded fonts.</summary>
    Fonts = 5,

    /// <summary>Thumbnails, private data and unused resources.</summary>
    Cleanup = 6,

    /// <summary>Language, title and tagging.</summary>
    Accessibility = 7,

    /// <summary>Text layers added to scanned pages.</summary>
    TextLayer = 8,

    /// <summary>Signature, encryption and conformance limits.</summary>
    Safety = 9,
}
