// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Pdf20;

/// <summary>What writing properties to the catalog's XMP packet did.</summary>
internal enum XmpWriteResult
{
    /// <summary>The packet already held every property, so it was left as it was.</summary>
    Unchanged = 0,

    /// <summary>The existing packet was edited.</summary>
    Edited = 1,

    /// <summary>The document had no packet, so a new one was created.</summary>
    Created = 2,

    /// <summary>The packet could not be edited safely, so nothing was written.</summary>
    Failed = 3,
}
