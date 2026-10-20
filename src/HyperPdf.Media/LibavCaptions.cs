// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen.Abstractions;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>Reads the text captions of a file once, when it opens, so the player can show the right words at any position.</summary>
internal static unsafe class LibavCaptions
{
    /// <summary>The most captions kept, so a damaged or hostile file cannot fill memory.</summary>
    private const int MaxCues = 50_000;

    /// <summary>The longest caption kept, in characters.</summary>
    private const int MaxTextLength = 2_000;

    /// <summary>The number of commas before the words in an ASS dialogue line made by FFmpeg.</summary>
    private const int AssFieldsBeforeText = 8;

    /// <summary>The milliseconds in a second.</summary>
    private const double MillisecondsPerSecond = 1_000.0;

    /// <summary>The longest a caption shows when the file does not say, in seconds.</summary>
    private const double DefaultSeconds = 4;

    /// <summary>Reads the captions of the best caption stream.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>The captions in the order the file lists them; empty when there are none or they cannot be read.</returns>
    internal static List<CaptionCue> Read(string path, CancellationToken cancellationToken)
    {
        var cues = new List<CaptionCue>();
        using var source = LibavSource.Open(path);
        if (source is null || source.CaptionIndex < 0)
        {
            return cues;
        }

        using var codec = LibavCodec.Open(source.Stream(source.CaptionIndex));
        if (codec is null)
        {
            return cues;
        }

        using var packet = new LibavPacket();
        ReadPackets(source, codec, packet, cues, cancellationToken);
        return cues;
    }

    /// <summary>Decodes every caption packet.</summary>
    /// <param name="source">The file.</param>
    /// <param name="codec">The caption decoder.</param>
    /// <param name="packet">A packet to read into.</param>
    /// <param name="cues">Receives the captions.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    private static void ReadPackets(LibavSource source, LibavCodec codec, LibavPacket packet, List<CaptionCue> cues, CancellationToken cancellationToken)
    {
        var timeBase = source.TimeBase(source.CaptionIndex);
        while (cues.Count < MaxCues && !cancellationToken.IsCancellationRequested && source.Read(packet))
        {
            if (packet.StreamIndex == source.CaptionIndex)
            {
                Decode(codec, packet, timeBase, source.Origin, cues);
            }

            ffmpeg.av_packet_unref(packet.Pointer);
        }
    }

    /// <summary>Decodes one packet into a caption.</summary>
    /// <param name="codec">The caption decoder.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="timeBase">The seconds in one tick of the packet's time stamps.</param>
    /// <param name="origin">The time stamp in seconds at which the file starts.</param>
    /// <param name="cues">Receives the caption.</param>
    private static void Decode(LibavCodec codec, LibavPacket packet, double timeBase, double origin, List<CaptionCue> cues)
    {
        var subtitle = default(AVSubtitle);
        var decoded = stackalloc int[1];
        if (ffmpeg.avcodec_decode_subtitle2(codec.Pointer, &subtitle, decoded, packet.Pointer) < 0 || decoded[0] == 0)
        {
            return;
        }

        try
        {
            var text = TextOf(&subtitle);
            if (text.Length > 0 && packet.Stamp != long.MinValue)
            {
                var start = (packet.Stamp * timeBase) - origin + (subtitle.start_display_time / MillisecondsPerSecond);
                var length = subtitle.end_display_time != 0 ? (subtitle.end_display_time - subtitle.start_display_time) / MillisecondsPerSecond : packet.Length * timeBase;
                cues.Add(new(TimeSpan.FromSeconds(Math.Max(0, start)), TimeSpan.FromSeconds(Math.Max(0, start) + (length > 0 ? length : DefaultSeconds)), text));
            }
        }
        finally
        {
            ffmpeg.avsubtitle_free(&subtitle);
        }
    }

    /// <summary>Gathers the words of a caption's rectangles.</summary>
    /// <param name="subtitle">The decoded caption.</param>
    /// <returns>The words, one rectangle to a line.</returns>
    private static string TextOf(AVSubtitle* subtitle)
    {
        var text = new StringBuilder();
        for (var i = 0U; i < subtitle->num_rects; i++)
        {
            var rectangle = subtitle->rects[i];
            var words = rectangle->type switch
            {
                AVSubtitleType.SUBTITLE_ASS => AssWords(Marshal.PtrToStringUTF8((nint)rectangle->ass)),
                AVSubtitleType.SUBTITLE_TEXT => Marshal.PtrToStringUTF8((nint)rectangle->text),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(words))
            {
                _ = (text.Length > 0 ? text.Append('\n') : text).Append(words.Trim());
            }
        }

        return text.Length > MaxTextLength ? text.ToString(0, MaxTextLength) : text.ToString();
    }

    /// <summary>Takes the words out of an ASS dialogue line: after the style fields, without override codes.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The words, or <see langword="null"/> when the line is empty.</returns>
    private static string? AssWords(string? line)
    {
        var at = line is null ? -1 : WordsStart(line);
        if (line is null || at < 0)
        {
            return null;
        }

        var words = new StringBuilder();
        var rest = line.AsSpan(at);
        while (!rest.IsEmpty)
        {
            // Override codes sit between braces; everything outside them is the words.
            var open = rest.IndexOf('{');
            _ = words.Append(open < 0 ? rest : rest[..open]);
            var close = open < 0 ? -1 : rest[open..].IndexOf('}');
            rest = close < 0 ? default : rest[(open + close + 1)..];
        }

        return words.Replace("\\N", "\n").Replace("\\n", "\n").ToString();
    }

    /// <summary>Finds where the words start in an ASS dialogue line: after the style fields.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The index of the first character of the words, or -1 when the line has too few fields.</returns>
    private static int WordsStart(string line)
    {
        var at = 0;
        for (var commas = 0; commas < AssFieldsBeforeText; commas++)
        {
            at = line.IndexOf(',', at) + 1;
            if (at == 0)
            {
                return -1;
            }
        }

        return at;
    }
}
