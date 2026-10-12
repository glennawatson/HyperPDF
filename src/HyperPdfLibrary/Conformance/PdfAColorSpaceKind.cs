// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What a colour space name resolves to, as far as the PDF/A content rules need to know.</summary>
internal enum PdfAColorSpaceKind
{
    /// <summary>The name does not give a usable colour space.</summary>
    Invalid = 0,

    /// <summary>A built-in device space: DeviceGray, DeviceRGB or DeviceCMYK.</summary>
    Device = 1,

    /// <summary>A pattern colour space, or a pattern.</summary>
    Pattern = 2,

    /// <summary>Any other colour space.</summary>
    Other = 3,
}
