// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using HyperPdf.Core.Spelling;

namespace HyperPdf.Platform.Windows.Spelling;

/// <summary>Checks spelling with Windows' own spell checker (ISpellChecker), in the reader's language or US English.</summary>
[DebuggerDisplay("WindowsSpellChecker: {Language}")]
public sealed unsafe class WindowsSpellChecker : ISpellChecker, IDisposable
{
    /// <summary>The IUnknown::Release slot.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The ISpellCheckerFactory::IsSupported slot.</summary>
    private const int IsSupportedSlot = 4;

    /// <summary>The ISpellCheckerFactory::CreateSpellChecker slot.</summary>
    private const int CreateSpellCheckerSlot = 5;

    /// <summary>The ISpellChecker::Check slot.</summary>
    private const int CheckSlot = 4;

    /// <summary>The ISpellChecker::Suggest slot.</summary>
    private const int SuggestSlot = 5;

    /// <summary>The IEnumSpellingError::Next and IEnumString::Next slot.</summary>
    private const int NextSlot = 3;

    /// <summary>The in-process server context, CLSCTX_INPROC_SERVER.</summary>
    private const uint InProcess = 1;

    /// <summary>S_OK, which Next returns when it found something.</summary>
    private const int Found = 0;

    /// <summary>The longest word checked; longer runs of letters are not words to check.</summary>
    private const int MaxStackWord = 128;

    /// <summary>The most suggestions offered.</summary>
    private const int MaxSuggestions = 5;

    /// <summary>The major version of Windows 8, which brought the spell checker.</summary>
    private const int WindowsEightMajor = 6;

    /// <summary>The minor version of Windows 8.</summary>
    private const int WindowsEightMinor = 2;

    /// <summary>The language used when the reader's own has no dictionary.</summary>
    private const string FallbackLanguage = "en-US";

    /// <summary>The spell checker factory's class, CLSID_SpellCheckerFactory.</summary>
    private static readonly Guid FactoryClass = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

    /// <summary>The spell checker factory's interface, IID_ISpellCheckerFactory.</summary>
    private static readonly Guid FactoryInterface = new("8E018A9D-2415-4677-BF08-794EA61F94BB");

    /// <summary>The ISpellChecker, or null when Windows has none for the language.</summary>
    private void* _checker;

    /// <summary>Initializes a new instance of the <see cref="WindowsSpellChecker"/> class for the reader's language.</summary>
    public WindowsSpellChecker()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(WindowsEightMajor, WindowsEightMinor))
        {
            return;
        }

        var factoryClass = FactoryClass;
        var factoryInterface = FactoryInterface;
        nint created = 0;
        if (NativeMethods.CoCreateInstance(&factoryClass, null, InProcess, &factoryInterface, (void**)&created) < 0)
        {
            return;
        }

        var factory = (void*)Read(&created);
        if (factory is null)
        {
            return;
        }

        try
        {
            Language = Supported(factory, CultureInfo.CurrentUICulture.Name) ? CultureInfo.CurrentUICulture.Name : FallbackLanguage;
            _checker = Invoke(factory, CreateSpellCheckerSlot, Language);
        }
        finally
        {
            Release(factory);
        }
    }

    /// <summary>Finalizes an instance of the <see cref="WindowsSpellChecker"/> class, releasing the checker if Dispose was not called.</summary>
    ~WindowsSpellChecker() => ReleaseChecker();

    /// <summary>Gets the dictionary's language, such as en-GB.</summary>
    public string Language { get; } = string.Empty;

    /// <inheritdoc/>
    public bool IsAvailable => _checker is not null;

    /// <inheritdoc/>
    public bool IsCorrect(ReadOnlySpan<char> word)
    {
        if (_checker is null || word.Length > MaxStackWord)
        {
            return true;
        }

        // Windows wants the word ending in a null character.
        Span<char> text = stackalloc char[MaxStackWord + 1];
        word.CopyTo(text);
        text[word.Length] = '\0';
        void* errors;
        fixed (char* value = text)
        {
            errors = Invoke(_checker, CheckSlot, value);
        }

        if (errors is null)
        {
            return true;
        }

        try
        {
            void* error = null;
            var found = ((delegate* unmanaged[Stdcall]<void*, void**, int>)Slot(errors, NextSlot))(errors, &error) == Found;
            Release(error);
            return !found;
        }
        finally
        {
            Release(errors);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Suggest(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (_checker is null)
        {
            return [];
        }

        var strings = Invoke(_checker, SuggestSlot, word);
        if (strings is null)
        {
            return [];
        }

        try
        {
            return ReadStrings(strings);
        }
        finally
        {
            Release(strings);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseChecker();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets a function from an object's interface table.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The function.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];

    /// <summary>Releases a COM object.</summary>
    /// <param name="instance">The object, or null.</param>
    private static void Release(void* instance)
    {
        if (instance is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(instance, ReleaseSlot))(instance);
        }
    }

    /// <summary>Calls a method that takes a string and hands back an object.</summary>
    /// <param name="instance">The object called.</param>
    /// <param name="slot">The method's slot.</param>
    /// <param name="text">The string.</param>
    /// <returns>The object handed back, or null on failure.</returns>
    private static void* Invoke(void* instance, int slot, string text)
    {
        fixed (char* value = text)
        {
            return Invoke(instance, slot, value);
        }
    }

    /// <summary>Calls a method that takes a null-terminated string and hands back an object.</summary>
    /// <param name="instance">The object called.</param>
    /// <param name="slot">The method's slot.</param>
    /// <param name="text">The null-terminated string.</param>
    /// <returns>The object handed back, or null on failure.</returns>
    private static void* Invoke(void* instance, int slot, char* text)
    {
        nint result = 0;
        return ((delegate* unmanaged[Stdcall]<void*, char*, nint*, int>)Slot(instance, slot))(instance, text, &result) < 0 ? null : (void*)Read(&result);
    }

    /// <summary>Reads a value a native call wrote.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">Where it was written.</param>
    /// <returns>The value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T Read<T>(T* value)
        where T : unmanaged => *value;

    /// <summary>Determines whether Windows has a dictionary for a language.</summary>
    /// <param name="factory">The spell checker factory.</param>
    /// <param name="language">The language tag.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    private static bool Supported(void* factory, string language)
    {
        var supported = 0;
        fixed (char* tag = language)
        {
            return ((delegate* unmanaged[Stdcall]<void*, char*, int*, int>)Slot(factory, IsSupportedSlot))(factory, tag, &supported) >= 0 && Read(&supported) != 0;
        }
    }

    /// <summary>Reads up to the most suggestions offered from an IEnumString, freeing each string.</summary>
    /// <param name="strings">The enumeration.</param>
    /// <returns>The strings.</returns>
    private static List<string> ReadStrings(void* strings)
    {
        List<string> output = [with(MaxSuggestions)];
        while (output.Count < MaxSuggestions)
        {
            char* value = null;
            uint fetched = 0;
            if (((delegate* unmanaged[Stdcall]<void*, uint, char**, uint*, int>)Slot(strings, NextSlot))(strings, 1, &value, &fetched) != Found || Read(&fetched) == 0)
            {
                break;
            }

            output.Add(new(value));
            Marshal.FreeCoTaskMem((nint)value);
        }

        return output;
    }

    /// <summary>Releases the checker once.</summary>
    private void ReleaseChecker()
    {
        Release(_checker);
        _checker = null;
    }
}
