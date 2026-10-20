// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Finds the sound and video in a document and writes it out as files. Nothing is played.</summary>
internal static class HyperPdfDocumentMediaItems
{
    /// <summary>The deepest chain of follow-on actions read.</summary>
    private const int MaxActionDepth = 8;

    /// <summary>Lists the document's media.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The media in page order.</returns>
    internal static IReadOnlyList<DocumentMedia> GetMedia(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed ? [] : Cached(self).Media;
    }

    /// <summary>Writes one piece of media as a file.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="index">The media's index.</param>
    /// <param name="destination">Receives the file.</param>
    /// <returns><see langword="true"/> when the whole file was written.</returns>
    internal static bool SaveMedia(HyperPdfDocument self, int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return false;
        }

        var entries = Cached(self).Entries;
        if ((uint)index >= (uint)entries.Length)
        {
            return false;
        }

        var entry = entries[index];
        var output = default(PooledBuffer);
        try
        {
            _ = entry.Data.Decode(ref output);
            if (output.Length == 0)
            {
                return false;
            }

            // The decoded bytes are a pooled copy that belongs to this call, so a sound's samples can be converted where they are.
            Write(entry, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(output.WrittenSpan), output.Length), destination);
            return true;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Writes decoded media: a sound as a WAVE file, anything else as it is.</summary>
    /// <param name="entry">The media.</param>
    /// <param name="bytes">The decoded bytes, a private copy that can be changed.</param>
    /// <param name="destination">Receives the file.</param>
    private static void Write(MediaEntry entry, Span<byte> bytes, Stream destination)
    {
        if (entry.Sound is { } sound)
        {
            HyperPdfSoundWave.Write(sound, bytes, destination);
            return;
        }

        destination.Write(bytes);
    }

    /// <summary>Gets the media of the document, found again only when the library gives a new snapshot of the annotations after an edit.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The media.</returns>
    private static MediaCache Cached(HyperPdfDocument self)
    {
        var snapshot = PdfDocumentMedia.GetMultimediaAnnotations(self.Document);
        if (Volatile.Read(ref self.Media) is { } cached && ReferenceEquals(cached.Source, snapshot))
        {
            return cached;
        }

        var entries = Collect(snapshot);
        var media = new DocumentMedia[entries.Count];
        for (var i = 0; i < media.Length; i++)
        {
            media[i] = entries[i].Info;
        }

        var made = new MediaCache(snapshot, [.. entries], media);
        Volatile.Write(ref self.Media, made);
        return made;
    }

    /// <summary>Gathers the media of every multimedia annotation.</summary>
    /// <param name="annotations">The multimedia annotations.</param>
    /// <returns>The entries, numbered in order.</returns>
    private static List<MediaEntry> Collect(PdfMultimediaAnnotation[] annotations)
    {
        var entries = new List<MediaEntry>();
        var seen = new HashSet<int>();
        foreach (var annotation in annotations)
        {
            switch (annotation)
            {
                case { Sound: { } sound }:
                {
                    AddSound(entries, annotation.PageIndex, sound);
                    break;
                }

                case { Screen: { } screen }:
                {
                    AddScreen(entries, seen, annotation.PageIndex, screen);
                    break;
                }

                case { RichMedia: { } rich }:
                {
                    AddRichMedia(entries, seen, annotation.PageIndex, rich);
                    break;
                }
            }
        }

        return entries;
    }

    /// <summary>Adds the sound of a sound annotation.</summary>
    /// <param name="entries">The entries so far.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="sound">The sound.</param>
    private static void AddSound(List<MediaEntry> entries, int page, PdfSound sound)
    {
        var wave = HyperPdfSoundWave.CanWrite(sound);
        var number = entries.Count + 1;
        var info = new DocumentMedia(
            entries.Count,
            DocumentMediaKind.Sound,
            page,
            string.Create(CultureInfo.InvariantCulture, $"Sound on page {page + 1}"),
            string.Create(CultureInfo.InvariantCulture, $"sound-{number}{(wave ? ".wav" : ".raw")}"),
            wave ? "audio/wav" : HyperPdfMediaTypes.Unknown,
            HyperPdfAttachments.AttachmentSize(sound.Data));
        entries.Add(new(info, sound.Data, wave ? sound : null));
    }

    /// <summary>Adds the media clips of the renditions a screen annotation's actions would play.</summary>
    /// <param name="entries">The entries so far.</param>
    /// <param name="seen">The object numbers of the streams already added, so a clip used twice is listed once.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="screen">The screen annotation.</param>
    private static void AddScreen(List<MediaEntry> entries, HashSet<int> seen, int page, PdfScreenAnnotation screen)
    {
        var renditions = new List<PdfRendition>();
        FindRenditions(screen.Action, renditions, 0);
        foreach (var trigger in screen.Triggers)
        {
            FindRenditions(trigger.Action, renditions, 0);
        }

        foreach (var rendition in renditions)
        {
            AddClip(entries, seen, page, Playable(rendition.Clip));
        }
    }

    /// <summary>Adds a media clip that holds its data in the document.</summary>
    /// <param name="entries">The entries so far.</param>
    /// <param name="seen">The object numbers of the streams already added.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="clip">The clip, or <see langword="null"/>.</param>
    private static void AddClip(List<MediaEntry> entries, HashSet<int> seen, int page, PdfMediaClip? clip)
    {
        if (clip?.Data is not { } data || (data.Id.Number != 0 && !seen.Add(data.Id.Number)))
        {
            return;
        }

        var name = FileNameOf(clip.FileName ?? clip.Name, clip.ContentType, entries.Count + 1);
        entries.Add(Make(entries.Count, DocumentMediaKind.Screen, page, name, clip.ContentType, data));
    }

    /// <summary>Adds the audio and video assets of a rich media annotation.</summary>
    /// <param name="entries">The entries so far.</param>
    /// <param name="seen">The object numbers of the streams already added.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="rich">The rich media annotation.</param>
    private static void AddRichMedia(List<MediaEntry> entries, HashSet<int> seen, int page, PdfRichMediaAnnotation rich)
    {
        foreach (var asset in rich.Assets)
        {
            var type = HyperPdfMediaTypes.FromFileName(asset.Name);
            if (asset.Data is { } data && type is not null && (data.Id.Number == 0 || seen.Add(data.Id.Number)))
            {
                entries.Add(Make(entries.Count, DocumentMediaKind.RichMedia, page, FileNameOf(asset.Name, type, entries.Count + 1), type, data));
            }
        }
    }

    /// <summary>Makes an entry for a media file.</summary>
    /// <param name="index">The entry's index.</param>
    /// <param name="kind">The kind of annotation.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="data">The data.</param>
    /// <returns>The entry.</returns>
    private static MediaEntry Make(int index, DocumentMediaKind kind, int page, string fileName, string? contentType, PdfStream data)
    {
        var type = contentType ?? HyperPdfMediaTypes.FromFileName(fileName) ?? HyperPdfMediaTypes.Unknown;
        var what = type.StartsWith("audio/", StringComparison.Ordinal) ? "audio" : "video";
        var name = string.Create(CultureInfo.InvariantCulture, $"{fileName} ({what} on page {page + 1})");
        return new(new(index, kind, page, name, fileName, type, HyperPdfAttachments.AttachmentSize(data)), data, null);
    }

    /// <summary>Follows a section clip to the clip that holds the data.</summary>
    /// <param name="clip">The clip, or <see langword="null"/>.</param>
    /// <returns>The clip with data, or <see langword="null"/> when the media is not embedded.</returns>
    private static PdfMediaClip? Playable(PdfMediaClip? clip)
    {
        var current = clip;
        for (var depth = 0; current is { Data: null, Section: { } parent } && depth < MaxActionDepth; depth++)
        {
            current = parent;
        }

        return current;
    }

    /// <summary>Makes a safe file name for saving, adding an extension from the media type when the name has none.</summary>
    /// <param name="name">The name the document gives, or <see langword="null"/>.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="number">The number used when the document gives no name.</param>
    /// <returns>The file name.</returns>
    private static string FileNameOf(string? name, string? contentType, int number)
    {
        var plain = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            plain = plain.Replace(bad, '_');
        }

        plain = plain.Length > 0 ? plain : string.Create(CultureInfo.InvariantCulture, $"media-{number}");
        return Path.HasExtension(plain) ? plain : plain + HyperPdfMediaTypes.ExtensionOf(contentType);
    }

    /// <summary>Gathers the renditions of an action and the actions that follow it.</summary>
    /// <param name="node">The action, or <see langword="null"/>.</param>
    /// <param name="found">Receives the renditions.</param>
    /// <param name="depth">How far the chain has been followed.</param>
    private static void FindRenditions(PdfActionNode? node, List<PdfRendition> found, int depth)
    {
        if (node is null || depth > MaxActionDepth)
        {
            return;
        }

        if (node.Action is RenditionAction { Rendition: { } rendition })
        {
            found.Add(rendition);
        }

        foreach (var next in node.Next)
        {
            FindRenditions(next, found, depth + 1);
        }
    }
}
