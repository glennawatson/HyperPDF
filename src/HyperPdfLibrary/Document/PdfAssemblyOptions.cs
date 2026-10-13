// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What an assembled document takes from its source documents besides their pages.</summary>
[DebuggerDisplay("PdfAssemblyOptions: viewer preferences from {ViewerPreferencesSource}")]
public sealed record PdfAssemblyOptions
{
    /// <summary>Gets the options that take nothing but the pages (and what travels with them).</summary>
    public static PdfAssemblyOptions Default { get; } = new();

    /// <summary>
    /// Gets the position of the source document whose viewer preferences (<c>/ViewerPreferences</c>) the result takes, or -1
    /// for none. The first page's source usually decides how a reader opens the result.
    /// </summary>
    public int ViewerPreferencesSource { get; init; } = -1;
}
