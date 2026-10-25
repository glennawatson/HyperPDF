// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests;

/// <summary>Reads a downloaded reference PDF from <c>src/tests/reference-pdfs/manifest.json</c> by id, or skips the test when it is not there.</summary>
internal static class ReferencePdfFile
{
    /// <summary>The environment variable naming the folder of downloaded reference PDFs.</summary>
    internal const string FolderVariable = "HYPERPDF_REFERENCE_PDF_DIR";

    /// <summary>The environment variable that turns a missing reference PDF from a skip into a failure when set to <c>1</c>.</summary>
    internal const string RequireVariable = "HYPERPDF_REQUIRE_REFERENCE_PDFS";

    /// <summary>Reads a reference PDF.</summary>
    /// <param name="id">The manifest id, which is also the file name without its extension.</param>
    /// <returns>The file bytes; the test is skipped, or fails when the files are required, if the file is not available.</returns>
    internal static byte[] Read(string id) => File.ReadAllBytes(RequirePaths(id)[0]);

    /// <summary>Finds the paths of reference PDFs.</summary>
    /// <param name="ids">The manifest ids, which are also the file names without their extensions.</param>
    /// <returns>The paths, in the order of the ids; the test is skipped, or fails when the files are required, if any file is not available.</returns>
    /// <exception cref="FileNotFoundException">A file is missing and <see cref="RequireVariable"/> is <c>1</c>.</exception>
    internal static string[] RequirePaths(params ReadOnlySpan<string> ids)
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var paths = new string[ids.Length];
        var missing = new List<string>();
        for (var i = 0; i < ids.Length; i++)
        {
            paths[i] = folder is { Length: > 0 } ? Path.Combine(folder, $"{ids[i]}.pdf") : string.Empty;
            if (!File.Exists(paths[i]))
            {
                missing.Add(ids[i]);
            }
        }

        if (missing.Count == 0)
        {
            return paths;
        }

        var message = $"The reference PDFs '{string.Join("', '", missing)}' are not available. "
            + $"Fetch them with src/tools/FetchReferencePdfs.cs from src/tests/reference-pdfs/manifest.json and set {FolderVariable}.";
        if (Environment.GetEnvironmentVariable(RequireVariable) == "1")
        {
            throw new FileNotFoundException(message);
        }

        Skip.Test(message);
        return [];
    }
}
