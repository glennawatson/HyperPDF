// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>The CxtVLC rows of T.814 Annex C, read from the data file the test assembly embeds.</summary>
internal static class JpxTestHtRows
{
    /// <summary>The name of the embedded data file.</summary>
    private const string ResourceName = "T814CxtVlc.txt";

    /// <summary>Reads the rows of both tables.</summary>
    /// <returns>The rows of CxtVLC_table_0 and CxtVLC_table_1.</returns>
    /// <exception cref="InvalidOperationException">The assembly does not embed the data file.</exception>
    internal static JpxHtTableBuilder.Row[][] Load()
    {
        using var stream = typeof(JpxTestHtRows).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The T.814 CxtVLC data file is not embedded.");
        using var reader = new StreamReader(stream);
        return JpxHtTableBuilder.ParseRows(reader.ReadToEnd());
    }
}
