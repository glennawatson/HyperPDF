// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NoticeDocument Parse(ReadOnlySpan<byte> utf8) => Parse(Encoding.UTF8.GetString(utf8));

    /// <summary>Reads a notices file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The document.</returns>
    public static NoticeDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var reader = new NoticeReader();
        foreach (var line in text.AsSpan().EnumerateLines())
        {
            reader.Add(line);
        }

        return new(reader.Finish());
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
}
