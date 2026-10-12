// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>Names the places PDF/A failures are found, for the report's context.</summary>
internal static class PdfAContexts
{
    /// <summary>The trailer.</summary>
    internal const string Trailer = "trailer";

    /// <summary>The document catalog.</summary>
    internal const string Catalog = "catalog";

    /// <summary>The catalog's metadata stream.</summary>
    internal const string CatalogMetadata = "catalog metadata";

    /// <summary>The document information dictionary.</summary>
    internal const string Info = "document information";

    /// <summary>The interactive form dictionary.</summary>
    internal const string Form = "interactive form";

    /// <summary>Names a page.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>For example "page 3".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Page(int index) => string.Create(CultureInfo.InvariantCulture, $"page {index + 1}");

    /// <summary>Names an indirect object.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>For example "object 12".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Object(int number) => string.Create(CultureInfo.InvariantCulture, $"object {number}");
}
