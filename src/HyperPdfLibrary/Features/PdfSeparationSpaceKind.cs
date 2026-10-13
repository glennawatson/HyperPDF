// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Features;

/// <summary>The kind of colour space a separation dictionary's <c>/ColorSpace</c> describes (ISO 32000-2 §14.11.4, Table 400).</summary>
public enum PdfSeparationSpaceKind
{
    /// <summary>The dictionary has no usable colour space.</summary>
    None = 0,

    /// <summary>A <c>/Separation</c> colour space with one colourant.</summary>
    Separation = 1,

    /// <summary>A <c>/DeviceN</c> colour space with one or more colourants.</summary>
    DeviceN = 2,
}
