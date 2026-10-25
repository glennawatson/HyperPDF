// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperCodecs;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>Reads the timed text of a file once, when it opens, so the player can show the right words at any position.</summary>
internal static class ManagedCaptions
{
    /// <summary>The most captions kept, so a damaged or hostile file cannot fill memory.</summary>
    private const int MaxCues = 50_000;

    /// <summary>The longest a caption shows when the file does not say, in seconds.</summary>
    private const double DefaultSeconds = 4;

    /// <summary>Reads the captions of one text track.</summary>
    /// <param name="container">The file, positioned anywhere; it is read from the start.</param>
    /// <param name="track">The number of the text track.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>The captions in the order the file lists them; empty when there are none.</returns>
    internal static List<CaptionCue> Read(MediaContainer container, int track, CancellationToken cancellationToken)
    {
        var cues = new List<CaptionCue>();
        if (!container.Seek(TimeSpan.Zero))
        {
            return cues;
        }

        var packet = new MediaPacket();
        while (cues.Count < MaxCues && !cancellationToken.IsCancellationRequested && container.ReadPacket(packet))
        {
            if (packet.Track == track && packet.Length > 0 && Cue(packet) is { } cue)
            {
                cues.Add(cue);
            }
        }

        return cues;
    }

    /// <summary>Makes a caption from a packet of text.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The caption, or <see langword="null"/> when the packet holds no words.</returns>
    private static CaptionCue? Cue(MediaPacket packet)
    {
        var words = SrtCaptions.PlainText(Encoding.UTF8.GetString(packet.Data), false);
        if (words.Length == 0)
        {
            return null;
        }

        var length = packet.Duration > 0 ? packet.Duration : DefaultSeconds;
        return new(TimeSpan.FromSeconds(packet.Time), TimeSpan.FromSeconds(packet.Time + length), words);
    }
}
