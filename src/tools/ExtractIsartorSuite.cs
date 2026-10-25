#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace HyperPdf.Scripts;

/// <summary>
/// Extracts the Isartor PDF/A-1b test suite from the PDF Association (a zip you obtained under its terms of use) into a
/// folder outside the repository, for the test that records the PDF/A validator's results. It downloads nothing.
/// </summary>
internal static class ExtractIsartorSuite
{
    /// <summary>Extracts the zip into an empty or missing folder.</summary>
    /// <param name="args">The zip file and the destination folder.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">The arguments are not a zip and a folder.</exception>
    /// <exception cref="FileNotFoundException">The zip does not exist.</exception>
    /// <exception cref="IOException">The folder already holds files.</exception>
    internal static async Task Main(string[] args)
    {
        if (args is not [var zipArgument, var destinationArgument])
        {
            throw new ArgumentException("Provide the Isartor zip file and the folder to extract it into.");
        }

        var zip = Path.GetFullPath(zipArgument);
        var folder = Path.GetFullPath(destinationArgument);
        if (!File.Exists(zip))
        {
            throw new FileNotFoundException("The Isartor zip file does not exist.", zip);
        }

        if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length > 0)
        {
            throw new IOException($"The folder must be empty or missing; remove it or choose another: {folder}");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await ZipFile.ExtractToDirectoryAsync(zip, folder, timeout.Token);
        Console.WriteLine($"Extracted the Isartor test suite: {folder}");
        Console.WriteLine("Set HYPERPDF_ISARTOR_FILES to that folder, and optionally HYPERPDF_PDFA_OUTPUT to a folder for the results list.");
        Console.WriteLine("Keep the files outside the repository. The suite's terms of use do not allow redistributing them.");
    }
}
