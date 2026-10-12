// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.MacOS;

/// <summary>Small helpers over Core Foundation and the Objective-C runtime: strings, preferences, file URLs and autorelease pools.</summary>
internal static unsafe class Foundation
{
    /// <summary>The Core Foundation number type for 64 bit integers (kCFNumberSInt64Type).</summary>
    private const nint Int64Number = 4;

    /// <summary>Creates a Core Foundation string, which is also an NSString.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The string; release it with <see cref="NativeMethods.Release"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static nint CreateString(string value) => CreateString(value.AsSpan());

    /// <summary>Creates a Core Foundation string from characters, which is also an NSString.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The string; release it with <see cref="NativeMethods.Release"/>.</returns>
    internal static nint CreateString(ReadOnlySpan<char> value)
    {
        fixed (char* characters = value)
        {
            return NativeMethods.CreateString(0, characters, value.Length);
        }
    }

    /// <summary>Reads a Core Foundation string.</summary>
    /// <param name="value">The string.</param>
    /// <returns>The text.</returns>
    internal static string ReadString(nint value)
    {
        var length = NativeMethods.GetStringLength(value);
        return string.Create((int)length, value, static (span, source) =>
        {
            fixed (char* buffer = span)
            {
                NativeMethods.GetCharacters(source, new(0, span.Length), buffer);
            }
        });
    }

    /// <summary>Reads a preference from a domain.</summary>
    /// <param name="key">The key.</param>
    /// <param name="domain">The domain, for example <c>kCFPreferencesAnyApplication</c> for the global one.</param>
    /// <returns>The value as a string, number or boolean, or <see langword="null"/>.</returns>
    internal static object? ReadPreference(string key, string domain)
    {
        var keyString = CreateString(key);
        var domainString = CreateString(domain);
        try
        {
            _ = NativeMethods.SynchronizePreferences(domainString);
            var value = NativeMethods.CopyPreference(keyString, domainString);
            if (value == 0)
            {
                return null;
            }

            try
            {
                return Convert(value);
            }
            finally
            {
                NativeMethods.Release(value);
            }
        }
        finally
        {
            NativeMethods.Release(keyString);
            NativeMethods.Release(domainString);
        }
    }

    /// <summary>Creates an <c>NSURL</c> for a file, autoreleased.</summary>
    /// <param name="filePath">The full path.</param>
    /// <returns>The URL.</returns>
    internal static nint FileUrl(string filePath)
    {
        var path = CreateString(filePath);
        try
        {
            return NativeMethods.Send(NativeMethods.GetClass("NSURL"), NativeMethods.Selector("fileURLWithPath:"), path);
        }
        finally
        {
            NativeMethods.Release(path);
        }
    }

    /// <summary>Runs an action inside an autorelease pool, so autoreleased objects are freed.</summary>
    /// <param name="action">The action.</param>
    internal static void InPool(Action action)
    {
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            action();
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Converts a preference value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A string, long or bool, or <see langword="null"/> for other types.</returns>
    private static object? Convert(nint value)
    {
        var type = NativeMethods.GetTypeId(value);
        if (type == NativeMethods.StringTypeId())
        {
            return ReadString(value);
        }

        if (type == NativeMethods.BooleanTypeId())
        {
            return NativeMethods.GetBooleanValue(value) != 0;
        }

        long number = 0;
        return type == NativeMethods.NumberTypeId() && NativeMethods.GetNumberValue(value, Int64Number, &number) != 0 ? number : null;
    }
}
