// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The tables a font subset plan fills and the writer reads: the fonts using each embedded program, the glyphs each font
/// shows and the glyphs to keep in each program. They are kept with the lists and bit sets they grew between runs, so a
/// plan does not allocate them again.
/// </summary>
[DebuggerDisplay("FontPlanState: {Programs.Count} programs")]
internal sealed class FontPlanState : IDisposable
{
    /// <summary>The most programs an idle state keeps room for.</summary>
    private const int MaxKeptPrograms = 1024;

    /// <summary>Idle states.</summary>
    private readonly ReusableSlots<FontPlanState> _idle;

    /// <summary>The emptied user lists of earlier plans.</summary>
    private readonly Stack<List<int>> _spareLists = [];

    /// <summary>The emptied bit sets of earlier plans.</summary>
    private readonly Stack<CodeBitSet> _spareSets = [];

    /// <summary>1 once the state has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="FontPlanState"/> class.</summary>
    /// <param name="idle">The owning service's font-plan pool.</param>
    private FontPlanState(ReusableSlots<FontPlanState> idle) => _idle = idle;

    /// <summary>Gets the object numbers of the fonts using each embedded program, by program number.</summary>
    internal Dictionary<int, List<int>> Programs { get; } = [];

    /// <summary>Gets the glyphs each font shows, by font number.</summary>
    internal Dictionary<int, CodeBitSet> Shown { get; } = [];

    /// <summary>
    /// Gets the object numbers of the only fonts whose programs may be cut down when <see cref="LimitToOnlyFonts"/> is set.
    /// Redaction fills it with the fonts that lost text, so no other font program is touched.
    /// </summary>
    internal HashSet<int> OnlyFonts { get; } = [];

    /// <summary>Gets or sets a value indicating whether only the programs of <see cref="OnlyFonts"/> may be cut down.</summary>
    internal bool LimitToOnlyFonts { get; set; }

    /// <summary>Gets the glyphs to keep in each embedded program, by the program's number.</summary>
    internal Dictionary<int, CodeBitSet> Kept { get; } = [];

    /// <summary>Gets the embedded program streams the plan may cut down, by the program's number.</summary>
    internal Dictionary<int, PdfStream> Streams { get; } = [];

    /// <summary>Gets what each font that uses an embedded program names, by the font's object number, for adding subset tags.</summary>
    internal Dictionary<int, FontUser> Users { get; } = [];

    /// <summary>Gets the cut-down program stream of each program that was cut down, by the program's number.</summary>
    internal Dictionary<int, PdfStream> Subsets { get; } = [];

    /// <summary>Gets the subset tag change of each font object that needs one, by object number.</summary>
    internal Dictionary<int, FontRename> Renames { get; } = [];

    /// <summary>Empties the state and gives it back to the pool.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        var grew = Programs.Count > MaxKeptPrograms || Kept.Count > MaxKeptPrograms;
        foreach (var users in Programs.Values)
        {
            users.Clear();
            _spareLists.Push(users);
        }

        Programs.Clear();
        OnlyFonts.Clear();
        LimitToOnlyFonts = false;
        Streams.Clear();
        Users.Clear();
        Subsets.Clear();
        Renames.Clear();
        Recycle(Shown);
        Recycle(Kept);
        if (!grew)
        {
            _ = _idle.Offer(this);
        }
    }

    /// <summary>Takes an idle state, or makes one.</summary>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>An empty state, which the caller disposes to hand it back.</returns>
    internal static FontPlanState Rent(OptimizerResources resources)
    {
        var idle = resources.Fonts;
        var state = idle.Take() ?? new(idle);
        _ = Interlocked.Exchange(ref state._returned, 0);
        return state;
    }

    /// <summary>Takes an empty list for a program's users.</summary>
    /// <returns>The list.</returns>
    internal List<int> RentList() => _spareLists.TryPop(out var list) ? list : [];

    /// <summary>Takes an empty bit set.</summary>
    /// <returns>The set; the state takes it back when it is disposed, once it is in <see cref="Shown"/> or <see cref="Kept"/>.</returns>
    internal CodeBitSet RentBits() => _spareSets.TryPop(out var set) ? set : new(true);

    /// <summary>Takes back a bit set that was removed from <see cref="Kept"/> or <see cref="Shown"/>.</summary>
    /// <param name="set">The set.</param>
    internal void Release(CodeBitSet set)
    {
        set.Dispose();
        _spareSets.Push(set);
    }

    /// <summary>Empties a table of bit sets, keeping the sets for the next plan.</summary>
    /// <param name="table">The table.</param>
    private void Recycle(Dictionary<int, CodeBitSet> table)
    {
        foreach (var set in table.Values)
        {
            set.Dispose();
            _spareSets.Push(set);
        }

        table.Clear();
    }
}
