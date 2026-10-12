// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Cups;

/// <summary>Source generated entry points of libcups (<c>cups/cups.h</c>), Apache-2.0.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name, resolved through <see cref="Core.Platform.NativeLibraries"/>.</summary>
    internal const string Library = "cups";

    /// <summary>Native <c>cupsGetDests2</c>.</summary>
    /// <param name="http">The connection, or 0 for the default server.</param>
    /// <param name="destinations">Receives the destinations, freed with <see cref="CupsFreeDests"/>.</param>
    /// <returns>The number of destinations.</returns>
    [LibraryImport(Library, EntryPoint = "cupsGetDests2")]
    internal static partial int CupsGetDests2(nint http, out nint destinations);

    /// <summary>Native <c>cupsFreeDests</c>.</summary>
    /// <param name="count">The number of destinations.</param>
    /// <param name="destinations">The destinations.</param>
    [LibraryImport(Library, EntryPoint = "cupsFreeDests")]
    internal static partial void CupsFreeDests(int count, nint destinations);

    /// <summary>Native <c>cupsPrintFile2</c>.</summary>
    /// <param name="http">The connection, or 0 for the default server.</param>
    /// <param name="name">The UTF-8 queue name.</param>
    /// <param name="fileName">The UTF-8 file path.</param>
    /// <param name="title">The UTF-8 job title.</param>
    /// <param name="optionCount">The number of options.</param>
    /// <param name="options">The options.</param>
    /// <returns>The job id, or 0 on failure.</returns>
    [LibraryImport(Library, EntryPoint = "cupsPrintFile2")]
    internal static partial int CupsPrintFile2(nint http, byte* name, byte* fileName, byte* title, int optionCount, CupsOption* options);

    /// <summary>Native <c>cupsLastErrorString</c>.</summary>
    /// <returns>The UTF-8 message of the last error.</returns>
    [LibraryImport(Library, EntryPoint = "cupsLastErrorString")]
    internal static partial nint CupsLastErrorString();
}
