// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>A PAdES baseline signature level, from ETSI EN 319 142-1. Each level includes the one before it.</summary>
public enum PdfPadesLevel
{
    /// <summary>No baseline level: the signature does not meet the basic level.</summary>
    None = 0,

    /// <summary>The basic level: a CAdES signature with the required attributes.</summary>
    BaselineB = 1,

    /// <summary>The basic level plus a trusted time stamp over the signature.</summary>
    BaselineT = 2,

    /// <summary>The time stamp level plus validation data stored in the file for long-term checking.</summary>
    BaselineLt = 3,

    /// <summary>The long-term level plus a later document time stamp that protects the validation data.</summary>
    BaselineLta = 4,
}
