// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Spelling;

namespace HyperPdf.Platform.MacOS.Spelling;

/// <summary>Checks spelling with the Mac's own spell checker, NSSpellChecker, in the languages the reader chose in System Settings.</summary>
[DebuggerDisplay("MacSpellChecker: {IsAvailable}")]
public sealed class MacSpellChecker : ISpellChecker
{
    /// <summary>The most suggestions offered.</summary>
    private const int MaxSuggestions = 5;

    /// <summary>NSNotFound, the location reported when nothing is misspelled.</summary>
    private const long NotFound = long.MaxValue;

    /// <summary>The shared NSSpellChecker, or zero when AppKit is not loaded.</summary>
    private readonly nint _checker;

    /// <summary>Initializes a new instance of the <see cref="MacSpellChecker"/> class.</summary>
    public MacSpellChecker()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var type = NativeMethods.GetClass("NSSpellChecker");
        _checker = type == 0 ? 0 : NativeMethods.Send(type, NativeMethods.Selector("sharedSpellChecker"));
    }

    /// <inheritdoc/>
    public bool IsAvailable => _checker != 0;

    /// <inheritdoc/>
    public bool IsCorrect(ReadOnlySpan<char> word)
    {
        if (_checker == 0 || word.IsEmpty)
        {
            return true;
        }

        var pool = NativeMethods.AutoreleasePoolPush();
        var text = Foundation.CreateString(word);
        try
        {
            var range = NativeMethods.SendCheckSpelling(_checker, NativeMethods.Selector("checkSpellingOfString:startingAt:"), text, 0);
            return range.Location == NotFound || range.Length == 0;
        }
        finally
        {
            NativeMethods.Release(text);
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <inheritdoc/>
    public void Suggest(string word, List<string> suggestions)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(suggestions);
        if (_checker == 0 || word.Length == 0)
        {
            return;
        }

        var pool = NativeMethods.AutoreleasePoolPush();
        var text = Foundation.CreateString(word);
        try
        {
            var selector = NativeMethods.Selector("guessesForWordRange:inString:language:inSpellDocumentWithTag:");
            var guesses = NativeMethods.SendGuesses(_checker, selector, new(0, word.Length), text, 0, 0);
            if (guesses != 0)
            {
                ReadStrings(guesses, suggestions);
            }
        }
        finally
        {
            NativeMethods.Release(text);
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Reads up to the most suggestions offered from an NSArray of NSString.</summary>
    /// <param name="array">The array.</param>
    /// <param name="output">The list the strings are added to.</param>
    private static void ReadStrings(nint array, List<string> output)
    {
        var count = (int)Math.Min(NativeMethods.SendCount(array, NativeMethods.Selector("count")), (nuint)MaxSuggestions);
        var objectAt = NativeMethods.Selector("objectAtIndex:");
        for (var i = 0; i < count; i++)
        {
            output.Add(Foundation.ReadString(NativeMethods.SendIndex(array, objectAt, (nuint)i)));
        }
    }
}
