// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.App.ViewModels;

/// <summary>A resource a view model owns until it hands it over; disposing it releases the resource unless it was handed over.</summary>
/// <typeparam name="T">The resource type.</typeparam>
/// <param name="value">The resource.</param>
[DebuggerDisplay("Handover: {Value}")]
internal sealed class Handover<T>(T value) : IDisposable
    where T : class, IDisposable
{
    /// <summary>The resource, or <see langword="null"/> once handed over or released.</summary>
    private T? _value = value;

    /// <summary>Gets the resource while this still owns it.</summary>
    internal T? Value => Volatile.Read(ref _value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _value, null)?.Dispose();

    /// <summary>Hands the resource over; the caller then disposes it.</summary>
    /// <returns>The resource, or <see langword="null"/> when it was already handed over or released.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? HandOver() => Interlocked.Exchange(ref _value, null);
}
