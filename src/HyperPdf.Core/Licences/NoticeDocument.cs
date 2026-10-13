// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace HyperPdf.Core.Licences;

/// <summary>
/// The third-party notices file read into groups of components. The file is Markdown written by
/// <c>scripts/GenerateThirdPartyNotices.cs</c>: a <c>## </c> heading per licence, a <c>### </c> heading per component
/// with <c>- Name: value</c> lines, then the licence text in a four-backtick <c>text</c> fence.
/// </summary>
/// <param name="Groups">The groups, this application's licence first.</param>
[DebuggerDisplay("NoticeDocument: {Groups.Count} licences")]
public sealed record NoticeDocument(IReadOnlyList<NoticeGroup> Groups)
{
    /// <summary>Gets an empty document.</summary>
    public static NoticeDocument Empty { get; } = new([]);

    /// <summary>Reads a notices file.</summary>
    /// <param name="utf8">The file's bytes.</param>
    /// <returns>The document.</returns>
    public static NoticeDocument Parse(ReadOnlySpan<byte> utf8)
    {
        // The whole file is decoded into a rented buffer, not a string; only the licence texts become strings.
        var chars = ArrayPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(utf8.Length));
        try
        {
            var length = Encoding.UTF8.GetChars(utf8, chars);
            return Parse(chars.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    /// <summary>Reads a notices file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The document.</returns>
    public static NoticeDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text.AsSpan());
    }

    /// <summary>Gets every component of every group.</summary>
    /// <returns>The components.</returns>
    public List<NoticeEntry> AllEntries()
    {
        var all = new List<NoticeEntry>();
        foreach (var group in Groups)
        {
            all.AddRange(group.Entries);
        }

        return all;
    }

    /// <summary>Reads a notices file from its characters.</summary>
    /// <param name="text">The file's characters.</param>
    /// <returns>The document.</returns>
    private static NoticeDocument Parse(ReadOnlySpan<char> text)
    {
        var reader = default(NoticeReader);
        foreach (var line in text.EnumerateLines())
        {
            reader.Add(line);
        }

        return new(reader.Finish());
    }
}
