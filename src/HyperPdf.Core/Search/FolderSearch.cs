// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Search;

/// <summary>
/// Searches every PDF in a folder: each file is opened on its own, searched page
/// by page and closed again, without disturbing the documents open in tabs.
/// </summary>
public static class FolderSearch
{
    /// <summary>The characters of context kept on each side of a match.</summary>
    private const int ContextLength = 50;

    /// <summary>The most files searched in one go.</summary>
    private const int MaxFiles = 10_000;

    /// <summary>Finds the PDF files in a folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="includeSubfolders">Whether to look in subfolders too.</param>
    /// <returns>The files, sorted by path.</returns>
    public static List<string> FindFiles(string folder, bool includeSubfolders)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.pdf", options))
        {
            files.Add(file);
            if (files.Count == MaxFiles)
            {
                break;
            }
        }

        files.Sort(StringComparer.CurrentCultureIgnoreCase);
        return files;
    }

    /// <summary>Searches one file.</summary>
    /// <param name="engine">The engine that opens it.</param>
    /// <param name="path">The file.</param>
    /// <param name="query">The words to find.</param>
    /// <param name="options">Match case and whole words.</param>
    /// <param name="maxMatches">The most matches kept for the file.</param>
    /// <param name="cancellationToken">Stops between pages.</param>
    /// <returns>What was found.</returns>
    public static FolderSearchFile SearchFile(IDocumentEngine engine, string path, string query, SearchOptions options, int maxMatches, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrEmpty(query);
        IDocument document;
        try
        {
            document = engine.Open(path, null);
        }
        catch (DocumentOpenException ex)
        {
            return new(path, [], ex.Error == DocumentOpenError.Password ? "needs a password" : "could not be opened", false);
        }

        using (document)
        {
            var found = new List<FolderSearchMatch>();
            var hits = new List<TextMatch>();
            for (var page = 0; page < document.PageCount; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hits.Clear();
                document.Find(page, query, options, hits);
                foreach (var hit in hits)
                {
                    if (found.Count == maxMatches)
                    {
                        return new(path, found, null, true);
                    }

                    found.Add(Snippet(document, hit));
                }
            }

            return new(path, found, null, false);
        }
    }

    /// <summary>Searches a file after preparing each requested page's font data.</summary>
    /// <param name="engine">The engine that opens it.</param>
    /// <param name="path">The file.</param>
    /// <param name="query">The words to find.</param>
    /// <param name="options">Match case and whole words.</param>
    /// <param name="maxMatches">The most matches kept for the file.</param>
    /// <param name="cancellationToken">Cancels preparation and search.</param>
    /// <returns>What was found.</returns>
    public static async Task<FolderSearchFile> SearchFileAsync(IDocumentEngine engine, string path, string query, SearchOptions options, int maxMatches, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrEmpty(query);
        IDocument document;
        try
        {
            document = await Task.Run(() => engine.Open(path, null), cancellationToken).ConfigureAwait(false);
        }
        catch (DocumentOpenException exception)
        {
            return new(path, [], exception.Error == DocumentOpenError.Password ? "needs a password" : "could not be opened", false);
        }

        using (document)
        {
            var found = new List<FolderSearchMatch>();
            await foreach (var page in DocumentSearch.SearchAsync(document, query, options, 0, cancellationToken).ConfigureAwait(false))
            {
                foreach (var hit in page.Hits)
                {
                    if (found.Count == maxMatches)
                    {
                        return new(path, found, null, true);
                    }

                    found.Add(Snippet(document, hit.Match));
                }
            }

            return new(path, found, null, false);
        }
    }

    /// <summary>Makes a match's snippet: the words around it on one line, with the match's place in it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="hit">Where the words were found.</param>
    /// <returns>The match with its snippet.</returns>
    private static FolderSearchMatch Snippet(IDocument document, TextMatch hit)
    {
        var start = Math.Max(0, hit.Start - ContextLength);
        var end = Math.Min(document.GetCharacterCount(hit.PageIndex), hit.Start + hit.Length + ContextLength);
        var raw = document.GetText(hit.PageIndex, start, end - start);
        var builder = new StringBuilder(raw.Length);
        var matchStart = -1;
        var matchEnd = -1;
        for (var i = 0; i < raw.Length; i++)
        {
            matchStart = i == hit.Start - start ? builder.Length : matchStart;
            matchEnd = i == hit.Start - start + hit.Length ? builder.Length : matchEnd;
            var c = char.IsWhiteSpace(raw[i]) ? ' ' : raw[i];
            if (c != ' ' || (builder.Length > 0 && builder[^1] != ' '))
            {
                _ = builder.Append(c);
            }
        }

        matchStart = Math.Max(0, matchStart);
        matchEnd = matchEnd < 0 ? builder.Length : matchEnd;
        return new(hit.PageIndex, builder.ToString().Trim(), matchStart, Math.Max(0, matchEnd - matchStart));
    }
}
