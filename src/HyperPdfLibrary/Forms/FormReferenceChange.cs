// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>The change to field names that actions referring to fields by name must follow: a renamed field and everything below it, or removed fields.</summary>
[DebuggerDisplay("FormReferenceChange: {OldName} to {NewName}")]
internal sealed class FormReferenceChange
{
    /// <summary>The fully qualified names of the removed fields.</summary>
    private readonly List<string> _removed = [];

    /// <summary>Gets the old name of a renamed field; empty when the change is a removal.</summary>
    internal string OldName { get; init; } = string.Empty;

    /// <summary>Gets the new name of a renamed field.</summary>
    internal string NewName { get; init; } = string.Empty;

    /// <summary>Gets the object numbers of the removed fields and widgets.</summary>
    internal HashSet<int> RemovedObjects { get; } = [];

    /// <summary>Gets a value indicating whether the change renames a field.</summary>
    internal bool IsRename => OldName.Length > 0;

    /// <summary>Adds a removed field name; the fields below it go with it.</summary>
    /// <param name="name">The fully qualified name.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddRemoved(string name) => _removed.Add(name);

    /// <summary>Gives the name a reference should now use.</summary>
    /// <param name="name">The name the reference holds.</param>
    /// <returns>The new name; <see langword="null"/> when the reference is not affected by a rename.</returns>
    internal string? MapName(string name)
    {
        if (!IsRename)
        {
            return null;
        }

        if (string.Equals(name, OldName, StringComparison.Ordinal))
        {
            return NewName;
        }

        return FormFieldNames.IsBelow(name, OldName) ? string.Concat(NewName, name.AsSpan(OldName.Length)) : null;
    }

    /// <summary>Determines whether a name belongs to a removed field.</summary>
    /// <param name="name">The name a reference holds.</param>
    /// <returns><see langword="true"/> when the field it names is gone.</returns>
    internal bool IsRemoved(string name)
    {
        foreach (var removed in _removed)
        {
            if (string.Equals(name, removed, StringComparison.Ordinal) || FormFieldNames.IsBelow(name, removed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a script mentions a name the change affects, in quotes as scripts refer to fields.</summary>
    /// <param name="script">The script text.</param>
    /// <returns>The affected name found in the script; <see langword="null"/> when there is none.</returns>
    internal string? FindMention(string script)
    {
        if (IsRename && Mentions(script, OldName))
        {
            return OldName;
        }

        foreach (var removed in _removed)
        {
            if (Mentions(script, removed))
            {
                return removed;
            }
        }

        return null;
    }

    /// <summary>Determines whether a script holds a name right after a quotation mark.</summary>
    /// <param name="script">The script text.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when <c>"name</c> or <c>'name</c> is in the script.</returns>
    private static bool Mentions(string script, string name) =>
        script.Contains($"\"{name}", StringComparison.Ordinal) || script.Contains($"'{name}", StringComparison.Ordinal);
}
