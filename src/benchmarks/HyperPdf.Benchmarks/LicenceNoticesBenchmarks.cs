// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Licences;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading the third-party notices file that the Licences window shows.</summary>
public class LicenceNoticesBenchmarks
{
    /// <summary>The components in the sample, about as many as the app lists.</summary>
    private const int Components = 70;

    /// <summary>The lines of licence text per component.</summary>
    private const int TextLines = 20;

    /// <summary>The sample file.</summary>
    private string _text = string.Empty;

    /// <summary>The sample file as UTF-8.</summary>
    private byte[] _utf8 = [];

    /// <summary>Builds a sample in the format the generator writes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var text = new StringBuilder("# Third-party notices\n\nIntro.\n\n## MIT\n");
        for (var i = 0; i < Components; i++)
        {
            _ = text.Append("\n### Package").Append(i).Append("\n- Version: 1.0.").Append(i).Append("\n- Origin: NuGet package\n- Copyright: Copyright (c) 2026 Someone\n\n````text\n");
            for (var line = 0; line < TextLines; line++)
            {
                _ = text.Append("Permission is hereby granted, free of charge, to any person obtaining a copy of this software.\n");
            }

            _ = text.Append("````\n");
        }

        _text = text.ToString();
        _utf8 = Encoding.UTF8.GetBytes(_text);
    }

    /// <summary>Reads the file from its text.</summary>
    /// <returns>The document.</returns>
    [Benchmark]
    public NoticeDocument ParseText() => NoticeDocument.Parse(_text);

    /// <summary>Reads the file from its UTF-8 bytes, as the app does after loading the embedded resource.</summary>
    /// <returns>The document.</returns>
    [Benchmark]
    public NoticeDocument ParseUtf8() => NoticeDocument.Parse(_utf8);

    /// <summary>Reads the file and every component's licence text.</summary>
    /// <returns>The total licence text length.</returns>
    [Benchmark]
    public int ReadLicenceTexts()
    {
        var length = 0;
        var groups = NoticeDocument.Parse(_utf8).Groups;
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var entries = groups[groupIndex].Entries;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                length += entries[entryIndex].Text.Length;
            }
        }

        return length;
    }
}
