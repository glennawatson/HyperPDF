// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// One PDF value in 16 bytes. A constant kind tag marks inline payloads; strings point at their bytes in the file or a
/// decoded buffer; arrays, dictionaries and streams point at their objects. Copying a value never allocates.
/// </summary>
[DebuggerDisplay("PdfValue: {Kind}")]
public readonly struct PdfValue : IEquatable<PdfValue>
{
    /// <summary>The bits that hold a string's offset or a reference's number; the other half is in the high bits.</summary>
    private const long LowHalf = 0xFFFF_FFFF;

    /// <summary>The shift of the high half.</summary>
    private const int HighShift = 32;

    /// <summary>Tags a boolean. Each inline tag is a one-character literal whose character is the kind.</summary>
    private const string BooleanTag = "\u0001";

    /// <summary>Tags an integer.</summary>
    private const string IntegerTag = "\u0002";

    /// <summary>Tags a real number.</summary>
    private const string RealTag = "\u0003";

    /// <summary>Tags a name.</summary>
    private const string NameTag = "\u0004";

    /// <summary>Tags an indirect reference.</summary>
    private const string ReferenceTag = "\u0009";

    /// <summary>
    /// The referenced object or string buffer, or for an inline payload its kind tag. The runtime interns each tag
    /// literal once, so the tags need no stored state, and a PDF payload is never a <see cref="string"/>.
    /// </summary>
    private readonly object? _object;

    /// <summary>The inline payload or string range.</summary>
    private readonly long _bits;

    /// <summary>Initializes a new instance of the <see cref="PdfValue"/> struct.</summary>
    /// <param name="bits">The inline payload.</param>
    /// <param name="value">The referenced object.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfValue(long bits, object? value)
    {
        _bits = bits;
        _object = value;
    }

    /// <summary>Gets the null value.</summary>
    public static PdfValue Null => default;

    /// <summary>Gets the kind of value.</summary>
    public PdfKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _object switch
        {
            null => PdfKind.Null,
            string tag => (PdfKind)tag[0],
            byte[] => PdfKind.String,
            PdfArray => PdfKind.Array,
            PdfDictionary => PdfKind.Dictionary,
            PdfStream => PdfKind.Stream,
            _ => throw new InvalidOperationException("The PDF value has an unknown payload kind."),
        };
    }

    /// <summary>Gets a value indicating whether this is null or missing.</summary>
    public bool IsNull => Kind == PdfKind.Null;

    /// <summary>Gets a value indicating whether this is an integer or real number.</summary>
    public bool IsNumber => Kind is PdfKind.Integer or PdfKind.Real;

    /// <summary>Gets a value indicating whether this is a reference to an indirect object.</summary>
    public bool IsReference => Kind == PdfKind.Reference;

    /// <summary>Creates a boolean.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PDF value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfValue FromBoolean(bool value) => new(value ? 1 : 0, BooleanTag);

    /// <summary>Creates an integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PDF value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfValue FromInteger(long value) => new(value, IntegerTag);

    /// <summary>Creates a real number.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The PDF value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfValue FromReal(double value) => new(BitConverter.DoubleToInt64Bits(value), RealTag);

    /// <summary>Creates a name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The PDF value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfValue FromName(PdfName name) => new(name.Id, NameTag);

    /// <summary>Creates a string over decoded bytes, without copying them.</summary>
    /// <param name="buffer">The buffer holding the bytes.</param>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The PDF value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside the buffer.</exception>
    public static PdfValue FromString(byte[] buffer, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset + (uint)length, (uint)buffer.Length);
        return new((uint)offset | ((long)length << HighShift), buffer);
    }

    /// <summary>Creates a string that owns its bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The PDF value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    public static PdfValue FromString(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return FromString(bytes, 0, bytes.Length);
    }

    /// <summary>Creates an array value.</summary>
    /// <param name="array">The array.</param>
    /// <returns>The PDF value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
    public static PdfValue FromArray(PdfArray array)
    {
        ArgumentNullException.ThrowIfNull(array);
        return new(0, array);
    }

    /// <summary>Creates a dictionary value.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The PDF value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public static PdfValue FromDictionary(PdfDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return new(0, dictionary);
    }

    /// <summary>Creates a stream value.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The PDF value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static PdfValue FromStream(PdfStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new(0, stream);
    }

    /// <summary>Creates a reference.</summary>
    /// <param name="id">The referenced object.</param>
    /// <returns>The PDF value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfValue FromReference(PdfObjectId id) => new((uint)id.Number | ((long)id.Generation << HighShift), ReferenceTag);

    /// <summary>Determines whether two values are equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PdfValue left, PdfValue right) => left.Equals(right);

    /// <summary>Determines whether two values differ.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PdfValue left, PdfValue right) => !left.Equals(right);

    /// <summary>Gets the boolean, or <see langword="false"/> for other kinds.</summary>
    /// <returns>The boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AsBoolean() => AsBoolean(false);

    /// <summary>Gets the boolean, or a fallback for other kinds.</summary>
    /// <param name="fallback">The value used when this is not a boolean.</param>
    /// <returns>The boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AsBoolean(bool fallback) => Kind == PdfKind.Boolean ? _bits != 0 : fallback;

    /// <summary>Gets the number as an integer, truncating reals, or zero for other kinds.</summary>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AsInteger() => AsInteger(0);

    /// <summary>Gets the number as an integer, truncating reals, or a fallback for other kinds.</summary>
    /// <param name="fallback">The value used when this is not a number.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AsInteger(long fallback) => Kind switch
    {
        PdfKind.Integer => _bits,
        PdfKind.Real => (long)Math.Clamp(BitConverter.Int64BitsToDouble(_bits), long.MinValue, long.MaxValue),
        _ => fallback,
    };

    /// <summary>Gets the number as a 32-bit integer, clamped, or zero for other kinds.</summary>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AsInt32() => AsInt32(0);

    /// <summary>Gets the number as a 32-bit integer, clamped, or a fallback for other kinds.</summary>
    /// <param name="fallback">The value used when this is not a number.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AsInt32(int fallback) => IsNumber ? (int)Math.Clamp(AsInteger(0), int.MinValue, int.MaxValue) : fallback;

    /// <summary>Gets the number, or zero for other kinds.</summary>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double AsNumber() => AsNumber(0);

    /// <summary>Gets the number, or a fallback for other kinds.</summary>
    /// <param name="fallback">The value used when this is not a number.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double AsNumber(double fallback) => Kind switch
    {
        PdfKind.Integer => _bits,
        PdfKind.Real => BitConverter.Int64BitsToDouble(_bits),
        _ => fallback,
    };

    /// <summary>Gets the number as a float, or zero for other kinds.</summary>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float AsSingle() => AsSingle(0);

    /// <summary>Gets the number as a float, or a fallback for other kinds.</summary>
    /// <param name="fallback">The value used when this is not a number.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float AsSingle(float fallback) => IsNumber ? (float)AsNumber(0) : fallback;

    /// <summary>Gets the name.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when this is a name.</returns>
    public bool TryGetName(out PdfName name)
    {
        name = new((int)_bits);
        return Kind == PdfKind.Name;
    }

    /// <summary>Gets the name, or no name for other kinds.</summary>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfName AsName() => Kind == PdfKind.Name ? new((int)_bits) : default;

    /// <summary>Determines whether this is a certain name.</summary>
    /// <param name="name">The known name.</param>
    /// <returns><see langword="true"/> when this is that name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsName(KnownName name) => Kind == PdfKind.Name && _bits == (long)name;

    /// <summary>Gets a string's bytes, or empty for other kinds.</summary>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsStringBytes() =>
        Kind == PdfKind.String ? Unsafe.As<byte[]>(_object!).AsSpan((int)(_bits & LowHalf), (int)(_bits >>> HighShift)) : [];

    /// <summary>Gets the array, or <see langword="null"/> for other kinds.</summary>
    /// <returns>The array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfArray? AsArray() => Kind == PdfKind.Array ? Unsafe.As<PdfArray>(_object) : null;

    /// <summary>Gets the dictionary, including a stream's dictionary, or <see langword="null"/> for other kinds.</summary>
    /// <returns>The dictionary.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfDictionary? AsDictionary() => Kind switch
    {
        PdfKind.Dictionary => Unsafe.As<PdfDictionary>(_object),
        PdfKind.Stream => Unsafe.As<PdfStream>(_object)!.Dictionary,
        _ => null,
    };

    /// <summary>Gets the stream, or <see langword="null"/> for other kinds.</summary>
    /// <returns>The stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfStream? AsStream() => Kind == PdfKind.Stream ? Unsafe.As<PdfStream>(_object) : null;

    /// <summary>Gets the referenced object's id, or an invalid id for other kinds.</summary>
    /// <returns>The id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfObjectId AsReference() => Kind == PdfKind.Reference ? new((int)(_bits & LowHalf), (int)(_bits >>> HighShift)) : default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PdfValue other) => _bits == other._bits && ReferenceEquals(_object, other._object);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_bits, _object);
}
