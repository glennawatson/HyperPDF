// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>The format a copy of an open document can be saved in.</summary>
public enum SaveCopyFormat
{
    /// <summary>A standard PDF copy that keeps annotations, form entries and page changes.</summary>
    Standard = 0,

    /// <summary>A smaller, cleaned-up or more accessible copy.</summary>
    Optimized = 1,

    /// <summary>A PDF/A copy made to keep its look for many years.</summary>
    Archive = 2,

    /// <summary>A PDF 2.0 copy that moves retired features when possible.</summary>
    Pdf20 = 3,

    /// <summary>A copy in which form fields, comments and other marks become part of the page.</summary>
    Flattened = 4,
}
