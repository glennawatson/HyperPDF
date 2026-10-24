// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>
/// Calls the methods of the COM objects Media Foundation hands out through their function tables, which keeps the interfaces
/// out of the trimmer's way. Each call names the slot of the method in its interface; <c>IMFAttributes</c> comes first in
/// the media type, the sample and the activation object, so their own methods follow its 33 slots.
/// </summary>
internal static unsafe class Com
{
    /// <summary>The <c>IUnknown::Release</c> slot.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The <c>IMFAttributes::GetUINT32</c> slot.</summary>
    private const int GetUInt32Slot = 7;

    /// <summary>The <c>IMFAttributes::GetUINT64</c> slot.</summary>
    private const int GetUInt64Slot = 8;

    /// <summary>The <c>IMFAttributes::GetGUID</c> slot.</summary>
    private const int GetGuidSlot = 10;

    /// <summary>The <c>IMFAttributes::SetUINT32</c> slot.</summary>
    private const int SetUInt32Slot = 21;

    /// <summary>The <c>IMFAttributes::SetUINT64</c> slot.</summary>
    private const int SetUInt64Slot = 22;

    /// <summary>The <c>IMFAttributes::SetGUID</c> slot.</summary>
    private const int SetGuidSlot = 24;

    /// <summary>The <c>IMFSample::GetSampleTime</c> slot.</summary>
    private const int GetSampleTimeSlot = 35;

    /// <summary>The <c>IMFSample::SetSampleTime</c> slot.</summary>
    private const int SetSampleTimeSlot = 36;

    /// <summary>The <c>IMFSample::SetSampleDuration</c> slot.</summary>
    private const int SetSampleDurationSlot = 38;

    /// <summary>The <c>IMFSample::ConvertToContiguousBuffer</c> slot.</summary>
    private const int ConvertToContiguousSlot = 41;

    /// <summary>The <c>IMFSample::AddBuffer</c> slot.</summary>
    private const int AddBufferSlot = 42;

    /// <summary>The <c>IMFMediaBuffer::Lock</c> slot.</summary>
    private const int LockSlot = 3;

    /// <summary>The <c>IMFMediaBuffer::Unlock</c> slot.</summary>
    private const int UnlockSlot = 4;

    /// <summary>The <c>IMFMediaBuffer::GetCurrentLength</c> slot.</summary>
    private const int GetCurrentLengthSlot = 5;

    /// <summary>The <c>IMFMediaBuffer::SetCurrentLength</c> slot.</summary>
    private const int SetCurrentLengthSlot = 6;

    /// <summary>Gets the address of a method in an object's function table.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="slot">The method's place in its interface, counting <c>IUnknown</c>'s three.</param>
    /// <returns>The method.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];

    /// <summary>Lets go of an object, if there is one.</summary>
    /// <param name="instance">The object, or null.</param>
    internal static void Release(void* instance)
    {
        if (instance is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(instance, ReleaseSlot))(instance);
        }
    }

    /// <summary>Sets a GUID attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SetGuid(void* attributes, Guid key, Guid value) =>
        ((delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, int>)Slot(attributes, SetGuidSlot))(attributes, &key, &value);

    /// <summary>Sets a 32 bit attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SetUInt32(void* attributes, Guid key, uint value) =>
        ((delegate* unmanaged[Stdcall]<void*, Guid*, uint, int>)Slot(attributes, SetUInt32Slot))(attributes, &key, value);

    /// <summary>Sets a 64 bit attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SetUInt64(void* attributes, Guid key, ulong value) =>
        ((delegate* unmanaged[Stdcall]<void*, Guid*, ulong, int>)Slot(attributes, SetUInt64Slot))(attributes, &key, value);

    /// <summary>Reads a 32 bit attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the attribute exists.</returns>
    internal static bool TryGetUInt32(void* attributes, Guid key, out uint value)
    {
        uint result;
        var hr = ((delegate* unmanaged[Stdcall]<void*, Guid*, uint*, int>)Slot(attributes, GetUInt32Slot))(attributes, &key, &result);
        value = hr >= 0 ? result : 0;
        return hr >= 0;
    }

    /// <summary>Reads a 64 bit attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the attribute exists.</returns>
    internal static bool TryGetUInt64(void* attributes, Guid key, out ulong value)
    {
        ulong result;
        var hr = ((delegate* unmanaged[Stdcall]<void*, Guid*, ulong*, int>)Slot(attributes, GetUInt64Slot))(attributes, &key, &result);
        value = hr >= 0 ? result : 0;
        return hr >= 0;
    }

    /// <summary>Reads a GUID attribute.</summary>
    /// <param name="attributes">The object holding attributes.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the attribute exists.</returns>
    internal static bool TryGetGuid(void* attributes, Guid key, out Guid value)
    {
        Guid result;
        var hr = ((delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, int>)Slot(attributes, GetGuidSlot))(attributes, &key, &result);
        value = hr >= 0 ? result : default;
        return hr >= 0;
    }

    /// <summary>Gives a sample a presentation time and duration.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="time">The time in 100 nanosecond units.</param>
    /// <param name="duration">The duration in 100 nanosecond units.</param>
    internal static void SetTimes(void* sample, long time, long duration)
    {
        _ = ((delegate* unmanaged[Stdcall]<void*, long, int>)Slot(sample, SetSampleTimeSlot))(sample, time);
        _ = ((delegate* unmanaged[Stdcall]<void*, long, int>)Slot(sample, SetSampleDurationSlot))(sample, duration);
    }

    /// <summary>Reads a sample's presentation time.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The time in 100 nanosecond units, or 0 when the sample has none.</returns>
    internal static long GetTime(void* sample)
    {
        long time;
        return ((delegate* unmanaged[Stdcall]<void*, long*, int>)Slot(sample, GetSampleTimeSlot))(sample, &time) >= 0 ? time : 0;
    }

    /// <summary>Adds a buffer to a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="buffer">The buffer.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AddBuffer(void* sample, void* buffer) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, int>)Slot(sample, AddBufferSlot))(sample, buffer);

    /// <summary>Gets one buffer holding all of a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="buffer">Receives the buffer, to be released.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ConvertToContiguous(void* sample, void** buffer) =>
        ((delegate* unmanaged[Stdcall]<void*, void**, int>)Slot(sample, ConvertToContiguousSlot))(sample, buffer);

    /// <summary>Locks a buffer so its bytes can be read or written.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="data">Receives the address of the bytes.</param>
    /// <param name="length">Receives the number of bytes in use, or null.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LockBuffer(void* buffer, byte** data, uint* length) =>
        ((delegate* unmanaged[Stdcall]<void*, byte**, uint*, uint*, int>)Slot(buffer, LockSlot))(buffer, data, null, length);

    /// <summary>Unlocks a buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UnlockBuffer(void* buffer) =>
        _ = ((delegate* unmanaged[Stdcall]<void*, int>)Slot(buffer, UnlockSlot))(buffer);

    /// <summary>Reads the number of bytes in use in a buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>The number of bytes, or 0.</returns>
    internal static uint GetLength(void* buffer)
    {
        uint length;
        return ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(buffer, GetCurrentLengthSlot))(buffer, &length) >= 0 ? length : 0;
    }

    /// <summary>Sets the number of bytes in use in a buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The number of bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetLength(void* buffer, uint length) =>
        _ = ((delegate* unmanaged[Stdcall]<void*, uint, int>)Slot(buffer, SetCurrentLengthSlot))(buffer, length);
}
