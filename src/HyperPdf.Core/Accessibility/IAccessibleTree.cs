// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// The application's elements as a platform bridge reads them. Reads are safe from any thread, because they work on an immutable
/// snapshot that the UI thread replaces; actions are carried out on the UI thread.
/// </summary>
public interface IAccessibleTree
{
    /// <summary>Gets the number of the application element, the root.</summary>
    int RootId { get; }

    /// <summary>Gets the number of the element that has the keyboard focus, or -1 when none has.</summary>
    int FocusedId { get; }

    /// <summary>Gets the changes as they are found, which a bridge passes to the screen reader.</summary>
    IObservable<AccessibleChange> Changes { get; }

    /// <summary>Gets an element.</summary>
    /// <param name="id">The element's number.</param>
    /// <returns>The element, or <see langword="null"/> when it is gone.</returns>
    AccessibleNode? Get(int id);

    /// <summary>Finds the deepest element at a point.</summary>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>The element's number, or -1 when nothing is there.</returns>
    int HitTest(int x, int y);

    /// <summary>Carries out one of an element's actions.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="action">The index in <see cref="AccessibleNode.Actions"/>.</param>
    /// <returns>Whether the action was started.</returns>
    bool DoAction(int id, int action);

    /// <summary>Moves the keyboard focus to an element.</summary>
    /// <param name="id">The element's number.</param>
    /// <returns>Whether the focus was asked to move.</returns>
    bool Focus(int id);

    /// <summary>Moves the caret of an element that has text.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="offset">The new offset.</param>
    /// <returns>Whether the caret was asked to move.</returns>
    bool SetCaret(int id, int offset);
}
