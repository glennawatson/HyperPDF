// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>Which pages the export window writes.</summary>
public enum ExportPagesScopeKind
{
    /// <summary>The page on screen.</summary>
    CurrentPage = 0,

    /// <summary>Every page.</summary>
    AllPages = 1,

    /// <summary>The pages typed in the range box.</summary>
    Range = 2,
}
