// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests for <see cref="SrtCaptions"/>.</summary>
public sealed class SrtCaptionsTests
{
    /// <summary>The end of the first caption in the first test, in seconds.</summary>
    private const double FirstEnd = 1.5;

    /// <summary>The start of the second caption in the first test, in seconds.</summary>
    private const double SecondStart = 62.25;

    /// <summary>The start in seconds of the caption whose times use points.</summary>
    private const double PointStart = 0.5;

    /// <summary>The number of captions in the first test.</summary>
    private const int TwoCaptions = 2;

    /// <summary>The second at which the second caption of the moment test begins.</summary>
    private const int SecondMoment = 1;

    /// <summary>The second at which no caption of the moment test shows.</summary>
    private const int LastMoment = 2;

    /// <summary>Verifies captions, their times and their lines are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsCaptionsWithTheirTimes()
    {
        var cues = SrtCaptions.Parse("1\n00:00:00,000 --> 00:00:01,500\nHello\nthere\n\n2\n00:01:02,250 --> 00:01:03,000\nWorld\n");

        await Assert.That(cues.Count).IsEqualTo(TwoCaptions);
        await Assert.That(cues[0].Start).IsEqualTo(TimeSpan.Zero);
        await Assert.That(cues[0].End).IsEqualTo(TimeSpan.FromSeconds(FirstEnd));
        await Assert.That(cues[0].Text).IsEqualTo("Hello\nthere");
        await Assert.That(cues[1].Start).IsEqualTo(TimeSpan.FromSeconds(SecondStart));
        await Assert.That(cues[1].Text).IsEqualTo("World");
    }

    /// <summary>Verifies WebVTT headers, notes, identifiers, hour-less times, settings and entities are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsWebVttCaptions()
    {
        var cues = SrtCaptions.Parse(
            "﻿WEBVTT - title\n\nNOTE a comment\n\nintro\n00:00.500 --> 00:01.500 align:start\nTom &amp; Jerry &lt;3 &bogus;\n\n"
            + "00:01:02.250 --> 00:01:03.000\n<v Bob>World</v>\n");

        await Assert.That(cues.Count).IsEqualTo(TwoCaptions);
        await Assert.That(cues[0].Start).IsEqualTo(TimeSpan.FromSeconds(PointStart));
        await Assert.That(cues[0].End).IsEqualTo(TimeSpan.FromSeconds(FirstEnd));
        await Assert.That(cues[0].Text).IsEqualTo("Tom & Jerry <3 &bogus;");
        await Assert.That(cues[1].Start).IsEqualTo(TimeSpan.FromSeconds(SecondStart));
        await Assert.That(cues[1].Text).IsEqualTo("World");
    }

    /// <summary>Verifies the caption at a moment includes its start and leaves out its end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsTheCaptionAtAMoment()
    {
        var cues = SrtCaptions.Parse("1\n00:00:00,000 --> 00:00:01,000\nOne\n\n2\n00:00:01,000 --> 00:00:02,000\nTwo\n");

        await Assert.That(SrtCaptions.TextAt(cues, TimeSpan.Zero)).IsEqualTo("One");
        await Assert.That(SrtCaptions.TextAt(cues, TimeSpan.FromSeconds(SecondMoment))).IsEqualTo("Two");
        await Assert.That(SrtCaptions.TextAt(cues, TimeSpan.FromSeconds(LastMoment))).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies markup tags are removed and position settings after the end time are ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesTagsAndPositionSettings()
    {
        var cues = SrtCaptions.Parse("1\r\n00:00:00.500 --> 00:00:01.000 X1:100 Y1:200\r\n<i>Slanted</i> and <font color=\"red\">red</font>\r\n");

        await Assert.That(cues.Count).IsEqualTo(1);
        await Assert.That(cues[0].Start).IsEqualTo(TimeSpan.FromSeconds(PointStart));
        await Assert.That(cues[0].Text).IsEqualTo("Slanted and red");
    }

    /// <summary>Verifies damaged times are skipped, huge values cannot overflow, and the last caption is kept without a blank line after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsDamagedTimes()
    {
        var cues = SrtCaptions.Parse("1\n99999999:00:00,000 --> 00:00:01,000\nToo late\n\n2\nnot a time --> also not\nLost\n\n3\n00:00:03,000 --> 00:00:04,000\nKept");

        await Assert.That(cues.Count).IsEqualTo(1);
        await Assert.That(cues[0].Text).IsEqualTo("Kept");
    }

    /// <summary>Verifies empty text gives no captions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyTextGivesNoCaptions()
    {
        await Assert.That(SrtCaptions.Parse(string.Empty).Count).IsEqualTo(0);
        await Assert.That(SrtCaptions.TextAt([], TimeSpan.FromSeconds(1))).IsEqualTo(string.Empty);
    }
}
