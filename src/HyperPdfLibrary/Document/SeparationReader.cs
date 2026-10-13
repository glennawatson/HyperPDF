// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads the separation dictionaries (<c>/SeparationInfo</c>) of a preseparated document and groups the pages by printed
/// page (ISO 32000-1 and ISO 32000-2 §14.11.4; deprecated in PDF 2.0). The page order is never changed.
/// </summary>
internal static class SeparationReader
{
    /// <summary>The most pages with a separation dictionary read.</summary>
    internal const int MaxSeparationPages = 10_000;

    /// <summary>The most entries read from one <c>/Pages</c> array or DeviceN colourant array.</summary>
    internal const int MaxListEntries = 1_024;

    /// <summary>How many pages are read between cancellation checks.</summary>
    private const int CheckInterval = 256;

    /// <summary>The length of a Separation colour space array: family, colourant, alternate space and tint transform.</summary>
    private const int SeparationLength = 4;

    /// <summary>The length of a DeviceN colour space array without its attributes dictionary.</summary>
    private const int DeviceNLength = 4;

    /// <summary>The length of a DeviceN colour space array with its attributes dictionary.</summary>
    private const int DeviceNAttributesLength = 5;

    /// <summary>Gets the page dictionary key of the separation dictionary.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The <c>/SeparationInfo</c> name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfName InfoKey(PdfNameTable names) => names.Intern("SeparationInfo"u8);

    /// <summary>Gets the separation dictionary key of the colourant.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The <c>/DeviceColorant</c> name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfName ColorantKey(PdfNameTable names) => names.Intern("DeviceColorant"u8);

    /// <summary>Reads and groups the separation pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups and findings.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfSeparationInfo Read(PdfDocument document, CancellationToken cancellationToken)
    {
        var pages = PdfDocumentPages.GetPageSet(document).Pages;
        var key = SeparationReader.InfoKey(document.Objects.Names);
        SeparationReadState? state = null;
        for (var i = 0; i < pages.Length; i++)
        {
            if (i % CheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (pages[i].Dictionary.GetDictionary(key) is not { } info)
            {
                continue;
            }

            state ??= new(document, SeparationReader.ColorantKey(document.Objects.Names));
            if (state.Entries.Count == MaxSeparationPages)
            {
                state.Report(i, PdfSeparationProblem.TooManyEntries);
                break;
            }

            SeparationReader.ReadEntry(state, i, info);
        }

        return state is null ? PdfSeparationInfo.Empty : SeparationReader.Group(state, cancellationToken);
    }

    /// <summary>Gets the text of a colourant given as a name or a string.</summary>
    /// <param name="objects">The objects that spell names.</param>
    /// <param name="value">The <c>/DeviceColorant</c> value.</param>
    /// <returns>The text, or <see langword="null"/> for another kind or an empty value.</returns>
    internal static string? ColorantText(PdfObjectStore objects, PdfValue value)
    {
        var text = value.Kind switch
        {
            PdfKind.Name => objects.Names.GetString(value.AsName()),
            PdfKind.String => PdfText.Decode(value.AsStringBytes()),
            _ => null,
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Reads one page's separation dictionary.</summary>
    /// <param name="state">The read.</param>
    /// <param name="index">The page index.</param>
    /// <param name="info">The separation dictionary.</param>
    private static void ReadEntry(SeparationReadState state, int index, PdfDictionary info)
    {
        var colorant = SeparationReader.ColorantText(state.Document.Objects, info.Get(state.ColorantKey));
        if (colorant is null)
        {
            state.Report(index, PdfSeparationProblem.MissingColorant);
        }

        var space = SeparationReader.ReadSpace(state, index, info.Get(KnownName.ColorSpace));
        var spaceColorants = space.Colorants ?? [];
        if (colorant is not null && space.Kind != PdfSeparationSpaceKind.None && Array.IndexOf(spaceColorants, colorant) < 0)
        {
            state.Report(index, PdfSeparationProblem.ColorantMismatch);
        }

        var pages = info.GetArray(KnownName.Pages);
        var entry = new SeparationEntry(new(index, colorant ?? string.Empty, space.Kind, spaceColorants), pages, SeparationReader.ReadMembers(state, index, pages));
        state.Entries.Add(entry);
        state.ByPage[index] = entry;
    }

    /// <summary>Reads the colour space of a separation dictionary.</summary>
    /// <param name="state">The read.</param>
    /// <param name="index">The page index.</param>
    /// <param name="space">The <c>/ColorSpace</c> value.</param>
    /// <returns>The space; <see cref="PdfSeparationSpaceKind.None"/> when missing or not a Separation or DeviceN array.</returns>
    private static SeparationSpace ReadSpace(SeparationReadState state, int index, PdfValue space)
    {
        if (space.IsNull)
        {
            return new(PdfSeparationSpaceKind.None, []);
        }

        var read = space.AsArray() is { } array ? SeparationReader.ReadSpaceArray(state.Document.Objects, array) : default;
        if (read.Colorants is null)
        {
            state.Report(index, PdfSeparationProblem.InvalidColorSpace);
            return new(PdfSeparationSpaceKind.None, []);
        }

        return read;
    }

    /// <summary>Reads a Separation or DeviceN colour space array.</summary>
    /// <param name="objects">The objects that spell names.</param>
    /// <param name="array">The colour space array.</param>
    /// <returns>The space; its colourants are <see langword="null"/> when it is neither or is malformed.</returns>
    private static SeparationSpace ReadSpaceArray(PdfObjectStore objects, PdfArray array)
    {
        var family = array.GetName(0);
        if (family.Is(KnownName.Separation) && array.Count == SeparationLength)
        {
            return array.Get(1).SpellName(objects) is { } name ? new(PdfSeparationSpaceKind.Separation, [name]) : default;
        }

        return family.Is(KnownName.DeviceN) && array.Count is DeviceNLength or DeviceNAttributesLength
            ? new(PdfSeparationSpaceKind.DeviceN, SeparationReader.ReadNames(objects, array.GetArray(1)))
            : default;
    }

    /// <summary>Reads the colourant names of a DeviceN colour space.</summary>
    /// <param name="objects">The objects that spell names.</param>
    /// <param name="names">The names array.</param>
    /// <returns>The names, or <see langword="null"/> when the array is missing, empty, too long or holds something else.</returns>
    private static string[]? ReadNames(PdfObjectStore objects, PdfArray? names)
    {
        if (names is not { Count: > 0 and <= MaxListEntries })
        {
            return null;
        }

        var result = new string[names.Count];
        for (var i = 0; i < result.Length; i++)
        {
            if (names.Get(i).SpellName(objects) is not { } name)
            {
                return null;
            }

            result[i] = name;
        }

        return result;
    }

    /// <summary>Reads the pages a <c>/Pages</c> array lists.</summary>
    /// <param name="state">The read.</param>
    /// <param name="index">The page whose array it is.</param>
    /// <param name="pages">The array, or <see langword="null"/>.</param>
    /// <returns>The page indexes in order and each once, or <see langword="null"/> when the array is missing or does not list the page.</returns>
    private static int[]? ReadMembers(SeparationReadState state, int index, PdfArray? pages)
    {
        if (pages is null)
        {
            state.Report(index, PdfSeparationProblem.MissingPages);
            return null;
        }

        var listed = state.Listed;
        listed.Clear();
        var members = state.Members;
        members.Clear();
        var stray = false;
        for (var i = 0; i < pages.Count; i++)
        {
            if (i == MaxListEntries)
            {
                state.Report(index, PdfSeparationProblem.TooManyEntries);
                break;
            }

            // Only the reference is looked at, never followed, so a looping or self-referencing entry ends here.
            var raw = pages.GetRaw(i);
            var page = raw.IsReference ? PdfDocumentPages.GetPageIndex(state.Document, raw.AsReference()) : -1;
            if (page < 0)
            {
                stray = true;
            }
            else if (listed.Add(page))
            {
                members.Add(page);
            }
        }

        if (stray)
        {
            state.Report(index, PdfSeparationProblem.NotAPage);
        }

        if (!listed.Contains(index))
        {
            state.Report(index, PdfSeparationProblem.MissingPages);
            return null;
        }

        return [.. members];
    }

    /// <summary>Groups the pages read: each group is started by the first page whose array is usable.</summary>
    /// <param name="state">The read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups and findings.</returns>
    private static PdfSeparationInfo Group(SeparationReadState state, CancellationToken cancellationToken)
    {
        var builds = new List<SeparationGroupBuild>();
        for (var i = 0; i < state.Entries.Count; i++)
        {
            if (i % CheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = state.Entries[i];
            if (entry.Group >= 0)
            {
                SeparationReader.CheckSameArray(state, entry, builds[entry.Group].Creator);
            }
            else if (entry.Members is { } members)
            {
                var build = new SeparationGroupBuild(entry);
                foreach (var member in members)
                {
                    SeparationReader.Join(state, build, builds.Count, member);
                }

                builds.Add(build);
            }
        }

        return SeparationReader.Finish(state, builds);
    }

    /// <summary>Adds a listed page to a group being formed.</summary>
    /// <param name="state">The read.</param>
    /// <param name="build">The group.</param>
    /// <param name="number">The group's position.</param>
    /// <param name="member">The listed page index.</param>
    private static void Join(SeparationReadState state, SeparationGroupBuild build, int number, int member)
    {
        if (!state.ByPage.TryGetValue(member, out var entry))
        {
            state.Report(member, PdfSeparationProblem.MemberWithoutSeparationInfo);
            return;
        }

        if (entry.Group >= 0)
        {
            state.Report(member, PdfSeparationProblem.PageInTwoGroups);
            return;
        }

        entry.Group = number;
        build.Members.Add(entry);
    }

    /// <summary>Reports a member whose own <c>/Pages</c> array differs from the one that formed its group.</summary>
    /// <param name="state">The read.</param>
    /// <param name="entry">The member.</param>
    /// <param name="creator">The page whose array formed the group.</param>
    private static void CheckSameArray(SeparationReadState state, SeparationEntry entry, SeparationEntry creator)
    {
        // A member without a usable array was reported when it was read.
        if (ReferenceEquals(entry, creator) || ReferenceEquals(entry.PagesArray, creator.PagesArray) || entry.Members is not { } members)
        {
            return;
        }

        if (!members.AsSpan().SequenceEqual(creator.Members))
        {
            state.Report(entry.Page.PageIndex, PdfSeparationProblem.PagesMismatch);
        }
    }

    /// <summary>Makes the result: groups in order of their first page, findings in page order.</summary>
    /// <param name="state">The read.</param>
    /// <param name="builds">The groups formed.</param>
    /// <returns>The result.</returns>
    private static PdfSeparationInfo Finish(SeparationReadState state, List<SeparationGroupBuild> builds)
    {
        builds.Sort(static (a, b) => SeparationReader.FirstPage(a).CompareTo(SeparationReader.FirstPage(b)));
        var groups = new PdfSeparationGroup[builds.Count];
        for (var i = 0; i < groups.Length; i++)
        {
            var members = builds[i].Members;
            var separations = new PdfSeparationPage[members.Count];
            for (var j = 0; j < separations.Length; j++)
            {
                separations[j] = members[j].Page;
            }

            groups[i] = new(i, separations);
        }

        state.Findings.Sort(static (a, b) => a.PageIndex != b.PageIndex ? a.PageIndex.CompareTo(b.PageIndex) : a.Problem.CompareTo(b.Problem));
        return new(groups, [.. state.Findings]);
    }

    /// <summary>Gets the lowest page index in a group.</summary>
    /// <param name="build">The group.</param>
    /// <returns>The index.</returns>
    private static int FirstPage(SeparationGroupBuild build)
    {
        var first = int.MaxValue;
        foreach (var member in build.Members)
        {
            first = Math.Min(first, member.Page.PageIndex);
        }

        return first;
    }
}
