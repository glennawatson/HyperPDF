// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>Updates the store's edit layer and object generations.</summary>
public static class StoreEditing
{
    /// <summary>The highest generation; an object that reaches it is never reused.</summary>
    internal const int MaxGeneration = 65_535;

    /// <summary>Gets a value indicating whether any object was changed, added or deleted.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>True when the store contains edits.</returns>
    public static bool HasEdits(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            return self.EditsState.Count > 0 || self.DeletedState.Count > 0;
        }
    }

    /// <summary>Adds a new indirect object.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "value">The value.</param>
    /// <returns>The new object's id.</returns>
    /// <exception cref = "ArgumentOutOfRangeException">The document has no object numbers left.</exception>
    public static PdfObjectId Add(PdfObjectStore self, PdfValue value)
    {
        lock (self.Gate)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(self.NextNumberState, PdfLimits.MaxObjectNumber);
            var id = new PdfObjectId(self.NextNumberState, 0);
            self.NextNumberState++;
            StoreTransactions.RecordEdit(self, id.Number);
            self.EditsState[id.Number] = value;
            if ((uint)id.Number < (uint)self.CacheState.Length)
            {
                Volatile.Write(ref self.CacheState[id.Number], null);
            }

            return id;
        }
    }

    /// <summary>Replaces an indirect object, reusing the number of a deleted one.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The object id.</param>
    /// <param name = "value">The new value.</param>
    /// <exception cref = "ArgumentOutOfRangeException">The object number is zero, negative or above the limit.</exception>
    public static void Replace(PdfObjectStore self, PdfObjectId id, PdfValue value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id.Number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id.Number, PdfLimits.MaxObjectNumber);
        lock (self.Gate)
        {
            StoreTransactions.RecordEdit(self, id.Number);
            self.EditsState[id.Number] = value;
            _ = self.DeletedState.Remove(id.Number);
            self.NextNumberState = Math.Max(self.NextNumberState, id.Number + 1);

            // A cached copy would be answered before the edit; drop it.
            if ((uint)id.Number < (uint)self.CacheState.Length)
            {
                Volatile.Write(ref self.CacheState[id.Number], null);
            }
        }
    }

    /// <summary>
    /// Deletes an indirect object. Saving incrementally records a free entry whose generation is one higher, so the number
    /// can be reused.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The object id; the generation is ignored.</param>
    /// <exception cref = "ArgumentOutOfRangeException">The object number is zero, negative or above the limit.</exception>
    public static void Delete(PdfObjectStore self, PdfObjectId id)
    {
        var number = id.Number;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, PdfLimits.MaxObjectNumber);
        lock (self.Gate)
        {
            var existed = self.XrefState.GetType(number) != XrefEntryType.Free || self.FreedGenerationsState.ContainsKey(number);
            var generation = StoreEditing.GetGenerationLocked(self, number);
            StoreTransactions.RecordEdit(self, number);
            _ = self.EditsState.Remove(number);
            if ((uint)number < (uint)self.CacheState.Length)
            {
                Volatile.Write(ref self.CacheState[number], null);
            }

            // An object that never reached a file, or is already free, has nothing to record.
            if (existed && !self.DeletedState.Contains(number))
            {
                self.FreedGenerationsState[number] = Math.Min(generation + 1, StoreEditing.MaxGeneration);
                _ = self.DeletedState.Add(number);
            }
        }
    }

    /// <summary>Gets the numbers of the changed, added and deleted objects, in ascending order.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The object numbers.</returns>
    public static int[] GetEditedNumbers(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            var numbers = new int[self.EditsState.Count + self.DeletedState.Count];
            self.EditsState.Keys.CopyTo(numbers, 0);
            self.DeletedState.CopyTo(numbers, self.EditsState.Count);
            Array.Sort(numbers);
            return numbers;
        }
    }

    /// <summary>
    /// Takes the changed, added and deleted objects with their generations in one step, so an edit made while a save runs
    /// cannot leave the saved update inconsistent.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "size">One more than the highest object number at that moment.</param>
    /// <returns>The objects, in ascending number order.</returns>
    public static PdfEditedObject[] GetEditedObjects(PdfObjectStore self, out int size)
    {
        lock (self.Gate)
        {
            var objects = new PdfEditedObject[self.EditsState.Count + self.DeletedState.Count];
            var keys = new int[objects.Length];
            var count = 0;
            foreach (var (number, value) in self.EditsState)
            {
                keys[count] = number;
                objects[count] = new(number, value, false, StoreEditing.GetGenerationLocked(self, number));
                count++;
            }

            foreach (var number in self.DeletedState)
            {
                keys[count] = number;
                objects[count] = new(number, default, true, StoreEditing.GetGenerationLocked(self, number));
                count++;
            }

            Array.Sort(keys, objects);
            size = self.Size;
            return objects;
        }
    }

    /// <summary>
    /// Gets the generation an object was read with, for writing it back under the same id. A deleted number that is
    /// reused takes the generation of its free entry.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The generation, or zero for new objects.</returns>
    public static int GetGeneration(PdfObjectStore self, int number)
    {
        lock (self.Gate)
        {
            return StoreEditing.GetGenerationLocked(self, number);
        }
    }

    /// <summary>Determines whether a save has anything to write: an object or trailer entry was changed, added or deleted.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>True when the store differs from its file.</returns>
    internal static bool HasChanges(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            return self.EditsState.Count > 0 || self.DeletedState.Count > 0 || self.TrailerEditedState;
        }
    }

    /// <summary>Forgets every change, after the document has been saved and reopened.</summary>
    /// <param name = "self">The owned object-store state.</param>
    internal static void ClearEdits(PdfObjectStore self)
    {
        lock (self.Gate)
        {
            self.EditsState.Clear();
            self.DeletedState.Clear();
            self.FreedGenerationsState.Clear();
            self.TrailerEditedState = false;
        }
    }

    /// <summary>Gets a changed object, while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <param name = "value">The changed value.</param>
    /// <returns><see langword="true"/> when the object was changed or deleted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetEdited(PdfObjectStore self, int number, out PdfValue value)
    {
        if (self.EditsState.TryGetValue(number, out value))
        {
            return true;
        }

        value = default;
        return self.DeletedState.Contains(number);
    }

    /// <summary>Gets an object's generation while holding the lock.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The generation.</returns>
    internal static int GetGenerationLocked(PdfObjectStore self, int number)
    {
        if (self.FreedGenerationsState.TryGetValue(number, out var freed))
        {
            return freed;
        }

        return self.XrefState.GetType(number) == XrefEntryType.InFile ? self.XrefState.GetDetail(number) : self.XrefState.GetFreeGeneration(number);
    }
}
