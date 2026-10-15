// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Licences;

/// <summary>One component in the third-party notices. The licence text is cut from the file the first time it is read.</summary>
[DebuggerDisplay("NoticeEntry: {Name} {Version} ({Licence})")]
public sealed class NoticeEntry
{
    /// <summary>The notices text.</summary>
    private readonly string _source;

    /// <summary>Where the licence text sits in <see cref="_source"/>.</summary>
    private readonly TextRange _text;

    /// <summary>The licence text once cut, or <see langword="null"/>.</summary>
    private string? _licenceText;

    /// <summary>Initializes a new instance of the <see cref="NoticeEntry"/> class.</summary>
    /// <param name="source">The notices text.</param>
    /// <param name="row">Where the component's parts sit in the text.</param>
    /// <param name="licence">The licence shared by the component's group.</param>
    internal NoticeEntry(string source, in NoticeRow row, string licence)
    {
        _source = source;
        _text = row.Text;
        Name = row.Name.Read(source);
        Licence = licence;
        Version = row.Version.Read(source);
        Origin = row.Origin.Read(source);
        Copyright = row.Copyright.Read(source);
        Link = row.Link.Read(source);
    }

    /// <summary>Gets the component's name.</summary>
    public string Name { get; }

    /// <summary>Gets the SPDX licence expression, such as <c>MIT</c>.</summary>
    public string Licence { get; }

    /// <summary>Gets the version, or empty.</summary>
    public string Version { get; }

    /// <summary>Gets where it comes from, such as "NuGet package" or "Downloaded when you turn this on".</summary>
    public string Origin { get; }

    /// <summary>Gets the copyright notice, or empty.</summary>
    public string Copyright { get; }

    /// <summary>Gets a link to the project, or empty.</summary>
    public string Link { get; }

    /// <summary>Gets the full licence text.</summary>
    public string Text => Volatile.Read(ref _licenceText) ?? ReadText();

    /// <summary>Cuts the licence text from the file and keeps it.</summary>
    /// <returns>The text.</returns>
    private string ReadText()
    {
        var text = _source.AsSpan(_text.Start, _text.Length).TrimEnd();
        var lines = !text.Contains('\r') ? text.ToString() : string.Create(text.Length - text.Count("\r\n".AsSpan()), text, static (output, input) =>
        {
            var written = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (input[i] == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                {
                    continue;
                }

                output[written] = input[i] == '\r' ? '\n' : input[i];
                written++;
            }
        });
        return Interlocked.CompareExchange(ref _licenceText, lines, null) ?? lines;
    }
}
