// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests;

/// <summary>
/// Prepares the external font data of non-embedded CJK fonts once per test run, before any test starts. The synchronous text,
/// hit-test, render and extraction methods only read data that is already in the font data folder, so without this step a
/// test passes only when an earlier test happened to fill the folder. The data is the same data the asynchronous font loading
/// prepares on demand: every predefined CMap and each character collection's Unicode table. It is downloaded from the pinned
/// upstream revisions the library uses and skipped when the folder already holds it. The substitute faces are embedded in the
/// library and need no preparation.
/// </summary>
internal static class FontDataWarmUp
{
    /// <summary>The Adobe character collections, in <c>CjkScript</c> order from Japanese.</summary>
    private static readonly CjkScript[] Scripts = [CjkScript.Japanese, CjkScript.SimplifiedChinese, CjkScript.TraditionalChinese, CjkScript.Korean];

    /// <summary>Prepares every predefined CMap and Unicode table.</summary>
    /// <param name="cancellationToken">Cancels the preparation when the test session ends.</param>
    /// <returns>A task completing when the data is in the font data folder.</returns>
    [Before(TestSession)]
    public static async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var resources = new PdfFontResources();
        try
        {
            await PrepareCMapsAsync(resources, cancellationToken);
        }
        finally
        {
            resources.CloseDownloadClient();
        }
    }

    /// <summary>Prepares the predefined CMaps and the Unicode table of each collection.</summary>
    /// <param name="resources">The font resources.</param>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>A task completing when the tables are cached.</returns>
    private static async Task PrepareCMapsAsync(PdfFontResources resources, CancellationToken cancellationToken)
    {
        foreach (var collection in Collections.All)
        {
            foreach (var name in collection.CMaps)
            {
                await PredefinedCMaps.EnsureAsync(resources, name, cancellationToken);
            }
        }

        foreach (var script in Scripts)
        {
            await CidToUnicodeTable.EnsureAsync(resources, script, cancellationToken);
        }
    }
}
