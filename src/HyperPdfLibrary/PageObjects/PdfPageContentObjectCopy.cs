// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// Paints another copy of a page object on a page. The copy is a form that holds the page's own content with every other object removed, so the object keeps
/// the colours, line style, clipping and transparency it has where it stands, whatever operators set them.
/// </summary>
public static class PdfPageContentObjectCopy
{
    /// <summary>The prefix of the resource name the copy's form gets.</summary>
    private const string FormPrefix = "EdCopy";

    /// <summary>The share of an object's larger side added to every edge of its form's box.</summary>
    private const float PadShare = 0.02F;

    /// <summary>The least added to every edge of the form's box, in points.</summary>
    private const float PadMinimum = 1F;

    /// <summary>The passes sharing resources makes: one to look for a clash and one to add.</summary>
    private const int SharePasses = 2;

    /// <summary>The most objects whose deleted state is kept on the stack while one object is copied.</summary>
    private const int StackObjects = 512;

    /// <summary>The initial number of entries in the form's dictionary.</summary>
    private const int FormEntries = 5;

    /// <summary>Paints a copy of an object on top of its own page, moved, resized or turned by a change.</summary>
    /// <param name="content">The page content that holds the object. Apply it afterwards to write the page.</param>
    /// <param name="item">The object to copy.</param>
    /// <param name="change">The change in user space that carries the copy from where the object is; the identity puts it exactly on the original.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The content belongs to a form, not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DrawOnTop(PdfPageContent content, PdfPageObject item, Matrix3x2 change) => Draw(content, CreateForm(content, item), change);

    /// <summary>
    /// Moves objects to one end of their page: their own content is written again at the end of the page, in front of everything, or at its start, behind everything, in the
    /// order they have on the page, and the originals are removed. The objects stay text, pictures and drawings when the page is read.
    /// The rest of each object's innermost marked-content sequence moves with it, so a tagged paragraph keeps its structure link and the link is written once.
    /// A wider sequence that also holds objects that stay is written as an artifact around the moved objects.
    /// </summary>
    /// <param name="content">The page content that holds the objects. Apply it afterwards to write the page.</param>
    /// <param name="items">The objects to move.</param>
    /// <param name="behind">True to move them behind everything else, false to move them in front of everything else.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void MoveInline(PdfPageContent content, PdfPageObject[] items, bool behind)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(items);
        items = WholeSequences(content, items);
        SplitMarks(content, items, out var shared, out var owned);
        SetArtifact(shared, true);
        var bytes = RegenerateOnly(content, items);
        SetArtifact(shared, false);

        // What the moved objects owned stays behind as empty artifacts, so the structure link is written once.
        SetArtifact(owned, true);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.WriteRaw(bytes);
            builder.RestoreState();
            if (behind)
            {
                PdfPageContentEditing.Prepend(content, builder.WrittenSpan);
            }
            else
            {
                PdfPageContentEditing.Append(content, builder.WrittenSpan);
            }
        }
        finally
        {
            builder.Dispose();
        }

        foreach (var item in items)
        {
            item.Delete();
        }
    }

    /// <summary>
    /// Adds a copy of an object's own content at the end of its page, moved, resized or turned by a change. The copy is still text, a picture or a drawing when the page is read,
    /// so it can be edited like the original. It has no place in the structure tree, so its marked content is written as artifacts.
    /// </summary>
    /// <param name="content">The page content that holds the object. Apply it afterwards to write the page.</param>
    /// <param name="item">The object to copy.</param>
    /// <param name="change">The change in user space that carries the copy from where the object is.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void CopyInline(PdfPageContent content, PdfPageObject item, Matrix3x2 change)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(item);
        var bytes = RegenerateAsArtifacts(content, item);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(change.M11, change.M12, change.M21, change.M22, change.M31, change.M32);
            builder.WriteRaw(bytes);
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Adds a copy of an object's own content at the end of another page of the same document, so the copy is text, a picture or a drawing there and can be edited like the original.
    /// The fonts, pictures and graphics states the content names are shared with the page the object is on. The copy has no place in the structure tree, so its marked content is
    /// written as artifacts.
    /// </summary>
    /// <param name="source">The page content that holds the object.</param>
    /// <param name="target">The page content the copy goes on. Apply it afterwards to write the page.</param>
    /// <param name="item">The object to copy.</param>
    /// <param name="change">The change in the target page's user space that carries the copy from where the object is.</param>
    /// <returns><see langword="false"/> when a name means something else on the target page; nothing was added then; copy it with <see cref="CreateForm"/> and <see cref="Draw"/> instead.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool TryCopyInline(PdfPageContent source, PdfPageContent target, PdfPageObject item, Matrix3x2 change)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(item);
        var bytes = RegenerateAsArtifacts(source, item);
        if (!PdfPageResourceSharing.TryShare(source, target, bytes))
        {
            return false;
        }

        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(change.M11, change.M12, change.M21, change.M22, change.M31, change.M32);
            builder.WriteRaw(bytes);
            builder.RestoreState();
            PdfPageContentEditing.Append(target, builder.WrittenSpan);
            return true;
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Makes a form that paints one object of a page and nothing else.</summary>
    /// <param name="content">The page content that holds the object.</param>
    /// <param name="item">The object to copy.</param>
    /// <returns>The form, which a page can paint with <see cref="Draw"/>, on the page it came from or on any other page of the same document.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The content belongs to a form, not a page.</exception>
    public static PdfStream CreateForm(PdfPageContent content, PdfPageObject item)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(item);
        if (content.Page is null)
        {
            throw new InvalidOperationException("Only a page's content can be copied from.");
        }

        var bytes = RegenerateAsArtifacts(content, item);
        var store = content.Document.Objects;
        var form = new PdfDictionary(store, FormEntries);
        form.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        form.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        var box = Pad(item);
        form.Set(KnownName.BBox, PdfValue.FromArray(PdfArray.FromNumbers(store, [box.Left, box.Bottom, box.Right, box.Top])));
        form.Set(KnownName.Resources, PdfValue.FromDictionary(content.Resources?.Clone() ?? new PdfDictionary(store)));
        return PdfPageContentApplication.CreateStream(form, bytes);
    }

    /// <summary>Paints a form from <see cref="CreateForm"/> on top of a page's content.</summary>
    /// <param name="content">The page content to add to. Apply it afterwards to write the page.</param>
    /// <param name="form">The form.</param>
    /// <param name="change">The change in the page's user space that carries the form from where it was made.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Draw(PdfPageContent content, PdfStream form, Matrix3x2 change)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(form);
        var name = PdfPageContentEditing.AllocateName(content, KnownName.XObject, FormPrefix);
        PdfPageContentEditing.AddResource(content, KnownName.XObject, name, PdfValue.FromStream(form));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(change.M11, change.M12, change.M21, change.M22, change.M31, change.M32);
            builder.DrawXObject(content.Document.Objects.Names.GetSpelling(name));
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Adds to some objects the others that sit in the same innermost marked-content sequence, so a tagged paragraph moves as a whole and keeps its structure link.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="items">The objects to move.</param>
    /// <returns>The objects to move, with the rest of their sequences, in the order they have on the page.</returns>
    private static PdfPageObject[] WholeSequences(PdfPageContent content, PdfPageObject[] items)
    {
        var innermost = new HashSet<PdfMark>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            if (item.Marks.Count > 0)
            {
                _ = innermost.Add(item.Marks[^1]);
            }
        }

        if (innermost.Count == 0)
        {
            return items;
        }

        var whole = new List<PdfPageObject>(items.Length);
        foreach (var other in content.Objects)
        {
            if (Array.IndexOf(items, other) >= 0 || (other.Marks.Count > 0 && innermost.Contains(other.Marks[^1])))
            {
                whole.Add(other);
            }
        }

        return whole.ToArray();
    }

    /// <summary>Sorts the marked-content sequences the moved objects sit in into those that hold only moved objects and those that hold others too.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="items">The moved objects.</param>
    /// <param name="shared">Receives the sequences that also hold objects that stay.</param>
    /// <param name="owned">Receives the sequences that hold only moved objects.</param>
    private static void SplitMarks(PdfPageContent content, PdfPageObject[] items, out List<PdfMark> shared, out List<PdfMark> owned)
    {
        shared = [];
        owned = [];
        var touched = new HashSet<PdfMark>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            foreach (var mark in item.Marks)
            {
                _ = touched.Add(mark);
            }
        }

        var kept = new HashSet<PdfMark>(ReferenceEqualityComparer.Instance);
        foreach (var other in content.Objects)
        {
            if (Array.IndexOf(items, other) >= 0)
            {
                continue;
            }

            foreach (var mark in other.Marks)
            {
                _ = kept.Add(mark);
            }
        }

        foreach (var mark in touched)
        {
            (kept.Contains(mark) ? shared : owned).Add(mark);
        }
    }

    /// <summary>Makes the regenerated content write some marked-content sequences as artifacts, or as they were.</summary>
    /// <param name="marks">The sequences.</param>
    /// <param name="on">Whether to write them as artifacts.</param>
    private static void SetArtifact(List<PdfMark> marks, bool on)
    {
        foreach (var mark in marks)
        {
            mark.SetArtifact(on);
        }
    }

    /// <summary>Gets the box of the copy's form: the object's own box with room for the width of its lines and for ink that lies a little past its metrics.</summary>
    /// <param name="item">The object.</param>
    /// <returns>The box in user space.</returns>
    private static PdfRectangle Pad(PdfPageObject item)
    {
        var box = item.Bounds;
        var pad = MathF.Max(item.LineWidth, 1F) + (MathF.Max(box.Width, box.Height) * PadShare) + PadMinimum;
        return new(box.Left - pad, box.Bottom - pad, box.Right + pad, box.Top + pad);
    }

    /// <summary>
    /// Writes the content of a copy: the page's content with every object but one taken out, and every marked-content sequence written as an artifact, because the
    /// copy has no place in the structure tree and must not repeat the marked-content numbers the original has.
    /// </summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object to keep.</param>
    /// <returns>The content stream data, not compressed.</returns>
    private static byte[] RegenerateAsArtifacts(PdfPageContent content, PdfPageObject item)
    {
        var marks = content.Begins;
        for (var i = 0; i < marks.Count; i++)
        {
            marks[i].SetArtifact(true);
        }

        try
        {
            return RegenerateOnly(content, item);
        }
        finally
        {
            for (var i = 0; i < marks.Count; i++)
            {
                marks[i].SetArtifact(false);
            }
        }
    }

    /// <summary>Writes the page's content with every object but one taken out, leaving the operators that set the state the object is drawn in.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object to keep.</param>
    /// <returns>The content stream data, not compressed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] RegenerateOnly(PdfPageContent content, PdfPageObject item) => RegenerateOnly(content, [item]);

    /// <summary>Writes the page's content with every object but some taken out, leaving the operators that set the state the kept objects are drawn in.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="keep">The objects to keep.</param>
    /// <returns>The content stream data, not compressed. The kept objects come in the order they have on the page.</returns>
    private static byte[] RegenerateOnly(PdfPageContent content, ReadOnlySpan<PdfPageObject> keep)
    {
        var objects = content.Objects;

        // Most pages hold fewer objects than the stack space; a larger page gets an array.
        Span<bool> storage = stackalloc bool[StackObjects];
        var wasDeleted = objects.Count <= StackObjects ? storage[..objects.Count] : new bool[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            wasDeleted[i] = objects[i].IsDeleted;
            if (!IsKept(keep, objects[i]))
            {
                objects[i].Delete();
            }
        }

        try
        {
            return PdfPageContentWriter.Regenerate(content);
        }
        finally
        {
            for (var i = 0; i < objects.Count; i++)
            {
                if (!IsKept(keep, objects[i]) && !wasDeleted[i])
                {
                    objects[i].Undelete();
                }
            }
        }
    }

    /// <summary>Tells whether an object is one of those kept.</summary>
    /// <param name="keep">The objects to keep.</param>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> when it is one of them.</returns>
    private static bool IsKept(ReadOnlySpan<PdfPageObject> keep, PdfPageObject item)
    {
        for (var i = 0; i < keep.Length; i++)
        {
            if (ReferenceEquals(keep[i], item))
            {
                return true;
            }
        }

        return false;
    }
}
