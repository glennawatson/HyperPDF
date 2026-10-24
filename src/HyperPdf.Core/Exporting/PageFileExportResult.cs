// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Exporting;

/// <summary>What exporting some pages to one file did.</summary>
/// <param name="Written">Whether the file was written.</param>
/// <param name="Skipped">How many things the format cannot hold that were left out.</param>
/// <param name="Approximated">How many things were drawn differently because the format cannot say them the way the page does.</param>
/// <param name="Problem">Why nothing was written, in plain words, or <see langword="null"/>.</param>
[DebuggerDisplay("PageFileExportResult: written {Written}, skipped {Skipped}, approximated {Approximated}")]
public sealed record PageFileExportResult(bool Written, int Skipped, int Approximated, string? Problem)
{
    /// <summary>Gets a value indicating whether the file was written and matches the page exactly.</summary>
    public bool IsExact => Written && Skipped == 0 && Approximated == 0;

    /// <summary>Makes the result of a file that was written exactly.</summary>
    /// <returns>The result.</returns>
    public static PageFileExportResult Exact() => new(true, 0, 0, null);

    /// <summary>Makes the result of a file that was not written.</summary>
    /// <param name="problem">Why, in plain words.</param>
    /// <returns>The result.</returns>
    public static PageFileExportResult Failed(string problem) => new(false, 0, 0, problem);
}
