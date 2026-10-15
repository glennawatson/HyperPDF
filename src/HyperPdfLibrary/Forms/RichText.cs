// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Rich text read into pooled arrays: every character in one array, the runs as ranges of it and the paragraphs as ranges of
/// the runs. Dispose gives the arrays back.
/// </summary>
[DebuggerDisplay("RichText: {ParagraphCount} paragraphs, {RunCount} runs")]
internal ref struct RichText
{
    /// <summary>The factor by which a full buffer grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The characters of every run; a pooled array.</summary>
    private char[]? _chars;

    /// <summary>The runs; a pooled array.</summary>
    private RichRun[]? _runs;

    /// <summary>The paragraphs; a pooled array.</summary>
    private RichParagraph[]? _paragraphs;

    /// <summary>Gets the number of characters.</summary>
    internal int CharCount { get; private set; }

    /// <summary>Gets the number of runs.</summary>
    internal int RunCount { get; private set; }

    /// <summary>Gets the number of paragraphs.</summary>
    internal int ParagraphCount { get; private set; }

    /// <summary>Gets a run.</summary>
    /// <param name="index">The run's place.</param>
    /// <returns>The run.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly RichRun GetRun(int index) => _runs![index];

    /// <summary>Gets a paragraph.</summary>
    /// <param name="index">The paragraph's place.</param>
    /// <returns>The paragraph.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly RichParagraph GetParagraph(int index) => _paragraphs![index];

    /// <summary>Gets the characters of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<char> GetText(in RichRun run) => _chars.AsSpan(run.Start, run.Length);

    /// <summary>Gets room to write characters after the last run.</summary>
    /// <param name="count">The most characters to write.</param>
    /// <returns>The space; call <see cref="CommitChars"/> with the number written.</returns>
    internal Span<char> GetCharSpace(int count)
    {
        if (_chars is null || _chars.Length - CharCount < count)
        {
            var larger = System.Buffers.ArrayPool<char>.Shared.Rent(Math.Max(count + CharCount, (_chars?.Length ?? 0) * GrowthFactor));
            _chars.AsSpan(0, CharCount).CopyTo(larger);
            if (_chars is not null)
            {
                System.Buffers.ArrayPool<char>.Shared.Return(_chars);
            }

            _chars = larger;
        }

        return _chars.AsSpan(CharCount, count);
    }

    /// <summary>Keeps characters written into the space <see cref="GetCharSpace"/> gave.</summary>
    /// <param name="count">The number of characters written.</param>
    internal void CommitChars(int count) => CharCount += count;

    /// <summary>Adds a run.</summary>
    /// <param name="run">The run, whose characters end at the last character committed.</param>
    internal void AddRun(in RichRun run)
    {
        PooledArrays.EnsureRoom(ref _runs, RunCount, clear: false)[RunCount] = run;
        RunCount++;
    }

    /// <summary>Replaces a run.</summary>
    /// <param name="index">The run's place.</param>
    /// <param name="run">The new run.</param>
    internal readonly void SetRun(int index, in RichRun run) => _runs![index] = run;

    /// <summary>Removes the last run and its characters.</summary>
    internal void RemoveLastRun()
    {
        RunCount--;
        CharCount = _runs![RunCount].Start;
    }

    /// <summary>Drops characters from the end of the last run.</summary>
    /// <param name="count">The number of characters to drop.</param>
    internal void TrimLastRun(int count)
    {
        var last = _runs![RunCount - 1];
        _runs[RunCount - 1] = last with { Length = last.Length - count };
        CharCount -= count;
    }

    /// <summary>Adds a paragraph.</summary>
    /// <param name="paragraph">The paragraph.</param>
    internal void AddParagraph(RichParagraph paragraph)
    {
        PooledArrays.EnsureRoom(ref _paragraphs, ParagraphCount, clear: false)[ParagraphCount] = paragraph;
        ParagraphCount++;
    }

    /// <summary>Joins the text of the paragraphs, one line per paragraph.</summary>
    /// <returns>The plain text.</returns>
    internal readonly string ToPlainText()
    {
        var length = Math.Max(0, ParagraphCount - 1);
        for (var i = 0; i < RunCount; i++)
        {
            length += _runs![i].Length;
        }

        return string.Create(length, this, static (destination, text) => text.CopyPlainText(destination));
    }

    /// <summary>Gives the arrays back.</summary>
    internal void Dispose()
    {
        PooledArrays.Return(ref _runs, RunCount, clear: false);
        PooledArrays.Return(ref _paragraphs, ParagraphCount, clear: false);
        if (_chars is not null)
        {
            System.Buffers.ArrayPool<char>.Shared.Return(_chars);
            _chars = null;
        }

        RunCount = 0;
        ParagraphCount = 0;
        CharCount = 0;
    }

    /// <summary>Writes the plain text.</summary>
    /// <param name="destination">The space for it.</param>
    private readonly void CopyPlainText(Span<char> destination)
    {
        var written = 0;
        for (var p = 0; p < ParagraphCount; p++)
        {
            if (p > 0)
            {
                destination[written] = '\n';
                written++;
            }

            var paragraph = _paragraphs![p];
            for (var r = paragraph.FirstRun; r < paragraph.FirstRun + paragraph.RunCount; r++)
            {
                GetText(_runs![r]).CopyTo(destination[written..]);
                written += _runs[r].Length;
            }
        }
    }
}
