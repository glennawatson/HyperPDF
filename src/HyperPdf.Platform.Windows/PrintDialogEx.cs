// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows;

/// <summary>
/// Native <c>PRINTDLGEXW</c>: the Windows print dialog's settings, which the dialog fills in with the person's choices.
/// This is the 64 bit layout (x64 and Arm64, the only Windows builds); fields the app leaves at zero are not declared.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 136)]
internal readonly struct PrintDialogEx : IEquatable<PrintDialogEx>
{
    /// <summary>The structure size.</summary>
    [FieldOffset(0)]
    private readonly uint _structSize;

    /// <summary>The owner window.</summary>
    [FieldOffset(8)]
    private readonly nint _owner;

    /// <summary>The chosen printer settings, freed with GlobalFree.</summary>
    [FieldOffset(16)]
    private readonly nint _devMode;

    /// <summary>The chosen printer names, freed with GlobalFree.</summary>
    [FieldOffset(24)]
    private readonly nint _devNames;

    /// <summary>The printer device context returned with <c>PD_RETURNDC</c>.</summary>
    [FieldOffset(32)]
    private readonly nint _deviceContext;

    /// <summary>The dialog flags.</summary>
    [FieldOffset(40)]
    private readonly uint _flags;

    /// <summary>The copies chosen.</summary>
    [FieldOffset(80)]
    private readonly uint _copies;

    /// <summary>The page shown first.</summary>
    [FieldOffset(128)]
    private readonly uint _startPage;

    /// <summary>What the user chose.</summary>
    [FieldOffset(132)]
    private readonly uint _resultAction;

    /// <summary>Initializes a new instance of the <see cref="PrintDialogEx"/> struct.</summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="flags">The dialog flags.</param>
    /// <param name="startPage">The page shown first.</param>
    internal unsafe PrintDialogEx(nint owner, uint flags, uint startPage)
    {
        this = default;
        _structSize = (uint)sizeof(PrintDialogEx);
        _owner = owner;
        _flags = flags;
        _copies = 1;
        _startPage = startPage;
    }

    /// <summary>Gets the chosen printer settings.</summary>
    internal nint DevMode => _devMode;

    /// <summary>Gets the chosen printer names.</summary>
    internal nint DevNames => _devNames;

    /// <summary>Gets the printer device context.</summary>
    internal nint DeviceContext => _deviceContext;

    /// <summary>Gets what the user chose.</summary>
    internal uint ResultAction => _resultAction;

    /// <summary>Compares two values.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PrintDialogEx left, PrintDialogEx right) => left.Equals(right);

    /// <summary>Compares two values.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PrintDialogEx left, PrintDialogEx right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PrintDialogEx other) =>
        MemoryMarshal.AsBytes(new ReadOnlySpan<PrintDialogEx>(in this)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<PrintDialogEx>(in other)));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is PrintDialogEx other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_structSize, _owner, _devMode, _devNames, _deviceContext, _flags, HashCode.Combine(_copies, _startPage, _resultAction));
}
