// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Navigation;

/// <summary>Back and forward history for jumps within a document (links, outline, search results, go to page).</summary>
[DebuggerDisplay("NavigationHistory: Back {BackCount}, Forward {ForwardCount}")]
public sealed class NavigationHistory
{
    /// <summary>The maximum number of remembered positions in each direction.</summary>
    private const int MaxEntries = 100;

    /// <summary>Positions to go back to, most recent last.</summary>
    private readonly List<DocumentPosition> _back = [];

    /// <summary>Positions to go forward to, most recent last.</summary>
    private readonly List<DocumentPosition> _forward = [];

    /// <summary>Gets the number of back entries.</summary>
    public int BackCount => _back.Count;

    /// <summary>Gets the number of forward entries.</summary>
    public int ForwardCount => _forward.Count;

    /// <summary>Gets a value indicating whether there is somewhere to go back to.</summary>
    public bool CanGoBack => _back.Count > 0;

    /// <summary>Gets a value indicating whether there is somewhere to go forward to.</summary>
    public bool CanGoForward => _forward.Count > 0;

    /// <summary>Records the position being left before a jump, clearing the forward history.</summary>
    /// <param name="current">The position being left.</param>
    public void Push(in DocumentPosition current)
    {
        if (_back.Count > 0 && _back[^1].PageIndex == current.PageIndex)
        {
            _back[^1] = current;
        }
        else
        {
            Add(_back, current);
        }

        _forward.Clear();
    }

    /// <summary>Moves back.</summary>
    /// <param name="current">The current position, remembered for going forward.</param>
    /// <param name="target">The position to go to.</param>
    /// <returns><see langword="true"/> when there was history.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGoBack(in DocumentPosition current, out DocumentPosition target) => TryMove(_back, _forward, current, out target);

    /// <summary>Moves forward.</summary>
    /// <param name="current">The current position, remembered for going back.</param>
    /// <param name="target">The position to go to.</param>
    /// <returns><see langword="true"/> when there was history.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGoForward(in DocumentPosition current, out DocumentPosition target) => TryMove(_forward, _back, current, out target);

    /// <summary>Clears all history.</summary>
    public void Clear()
    {
        _back.Clear();
        _forward.Clear();
    }

    /// <summary>Moves from one stack to the other.</summary>
    /// <param name="from">The stack to pop.</param>
    /// <param name="to">The stack to push the current position onto.</param>
    /// <param name="current">The current position.</param>
    /// <param name="target">The popped position.</param>
    /// <returns><see langword="true"/> when an entry was popped.</returns>
    private static bool TryMove(List<DocumentPosition> from, List<DocumentPosition> to, in DocumentPosition current, out DocumentPosition target)
    {
        if (from.Count == 0)
        {
            target = current;
            return false;
        }

        target = from[^1];
        from.RemoveAt(from.Count - 1);
        Add(to, current);
        return true;
    }

    /// <summary>Appends an entry, discarding the oldest when full.</summary>
    /// <param name="list">The stack.</param>
    /// <param name="position">The position.</param>
    private static void Add(List<DocumentPosition> list, in DocumentPosition position)
    {
        if (list.Count >= MaxEntries)
        {
            list.RemoveAt(0);
        }

        list.Add(position);
    }
}
