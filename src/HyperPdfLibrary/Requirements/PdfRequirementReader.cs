// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// Reads the catalog's <c>/Requirements</c> array (ISO 32000-2 §7.7.2 and §12.11). The read is bounded by
/// <see cref="PdfLimits"/>, skips what is malformed and treats handlers as data: no script is run.
/// </summary>
internal static class PdfRequirementReader
{
    /// <summary>The penalty of a requirement that gives none (§12.11.1, Table 273).</summary>
    internal const int DefaultPenalty = 100;

    /// <summary>The highest penalty a requirement may carry.</summary>
    private const int MaxPenalty = 100;

    /// <summary>The fewest numbers a version name has (§12.11.4).</summary>
    private const int MinVersionParts = 2;

    /// <summary>The most digits one part of a version name keeps, so comparing versions cannot overflow.</summary>
    private const int MaxVersionPartDigits = 9;

    /// <summary>The requirement type that carries encryption settings.</summary>
    private const string EncryptionType = "Encryption";

    /// <summary>The handler type of an ECMAScript handler.</summary>
    private const string ScriptHandler = "JS";

    /// <summary>The handler type that lets older processors ignore a requirement.</summary>
    private const string NoOpHandler = "NoOp";

    /// <summary>The handlers of a requirement that names none.</summary>
    private static readonly PdfRequirementHandler[] NoHandlers = [];

    /// <summary>Gets the key of the catalog entry.</summary>
    private static ReadOnlySpan<byte> RequirementsKey => "Requirements"u8;

    /// <summary>Gets the key of a requirement's type.</summary>
    private static ReadOnlySpan<byte> TypeKey => "S"u8;

    /// <summary>Gets the key of a requirement's version.</summary>
    private static ReadOnlySpan<byte> VersionKey => "V"u8;

    /// <summary>Gets the key of a requirement's penalty.</summary>
    private static ReadOnlySpan<byte> PenaltyKey => "Penalty"u8;

    /// <summary>Gets the key of a requirement's alternative handlers.</summary>
    private static ReadOnlySpan<byte> HandlersKey => "RH"u8;

    /// <summary>Gets the key of a handler's script name.</summary>
    private static ReadOnlySpan<byte> ScriptKey => "Script"u8;

    /// <summary>Gets the key of an encryption requirement's settings.</summary>
    private static ReadOnlySpan<byte> EncryptKey => "Encrypt"u8;

    /// <summary>Reads the requirements of a document.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The requirements; <see cref="PdfRequirementList.Empty"/> when the catalog has no <c>/Requirements</c> entry.</returns>
    internal static PdfRequirementList Read(PdfObjectStore store)
    {
        var value = store.Catalog.Get(store.Names.Intern(RequirementsKey));
        if (value.IsNull)
        {
            return PdfRequirementList.Empty;
        }

        if (value.AsArray() is not { } array)
        {
            // The entry is there but is not an array, so there is nothing to check; count it as one bad entry.
            return new([], 1, false);
        }

        var count = Math.Min(array.Count, PdfLimits.MaxRequirements);
        var requirements = new List<PdfRequirement>(count);
        var skipped = 0;
        for (var i = 0; i < count; i++)
        {
            if (ReadRequirement(store, array.Get(i).AsDictionary()) is { } requirement)
            {
                requirements.Add(requirement);
            }
            else
            {
                skipped++;
            }
        }

        return new(requirements.AsReadOnly(), skipped, array.Count > count);
    }

    /// <summary>Compares two versions written as dotted numbers, padding the shorter with zeros.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>Negative, zero or positive as <paramref name="left"/> is below, equal to or above <paramref name="right"/>.</returns>
    internal static int CompareVersions(string left, string right)
    {
        var leftRest = left.AsSpan();
        var rightRest = right.AsSpan();
        while (!leftRest.IsEmpty || !rightRest.IsEmpty)
        {
            var order = NextPart(ref leftRest).CompareTo(NextPart(ref rightRest));
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    /// <summary>Reads one requirement dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="dictionary">The entry, or <see langword="null"/> when it is not a dictionary.</param>
    /// <returns>The requirement, or <see langword="null"/> when the entry has no requirement type.</returns>
    private static PdfRequirement? ReadRequirement(PdfObjectStore store, PdfDictionary? dictionary)
    {
        if (dictionary is null)
        {
            return null;
        }

        var names = store.Names;
        if (dictionary.Get(names.Intern(TypeKey)).SpellName(store) is not { Length: > 0 } type)
        {
            return null;
        }

        var penalty = ReadPenalty(dictionary.Get(names.Intern(PenaltyKey)));
        var version = ReadVersion(store, dictionary.Get(names.Intern(VersionKey)));
        var handlers = ReadHandlers(store, dictionary.Get(names.Intern(HandlersKey)));
        var encryption = string.Equals(type, EncryptionType, StringComparison.Ordinal) ? ReadEncryption(store, dictionary.GetDictionary(names.Intern(EncryptKey))) : null;
        return new(type, version, penalty, Array.AsReadOnly(handlers), encryption);
    }

    /// <summary>Reads a penalty, keeping it in the range the standard allows.</summary>
    /// <param name="value">The <c>/Penalty</c> value.</param>
    /// <returns>The penalty; 100 when the value is missing or is not a number.</returns>
    private static int ReadPenalty(PdfValue value) => value.IsNumber ? Math.Clamp(value.AsInt32(DefaultPenalty), 0, MaxPenalty) : DefaultPenalty;

    /// <summary>Reads a <c>/V</c> entry.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The value.</param>
    /// <returns>The version, or <see langword="null"/> when the entry is missing.</returns>
    private static PdfRequirementVersion? ReadVersion(PdfObjectStore store, PdfValue value)
    {
        if (value.IsNull)
        {
            return null;
        }

        if (value.AsDictionary() is { } extension)
        {
            // Only one entry is allowed: the registered prefix of the vendor (§12.11.4).
            var prefix = extension.Count == 1 ? store.Names.GetString(extension.GetKeyAt(0)) : string.Empty;
            return new(prefix, true, prefix.Length > 0);
        }

        var text = value.SpellName(store) ?? string.Empty;
        return new(text, false, IsVersionName(text));
    }

    /// <summary>Determines whether a name is two or more whole numbers separated by periods, and short enough to compare.</summary>
    /// <param name="text">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsVersionName(string text)
    {
        if (text.Length is 0 or > PdfLimits.MaxRequirementText)
        {
            return false;
        }

        var parts = 0;
        foreach (var range in text.AsSpan().Split('.'))
        {
            if (!IsWholeNumber(text.AsSpan(range)))
            {
                return false;
            }

            parts++;
        }

        return parts >= MinVersionParts;
    }

    /// <summary>Determines whether a part of a version is a short run of decimal digits.</summary>
    /// <param name="part">The part.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsWholeNumber(ReadOnlySpan<char> part) => part.Length is > 0 and <= MaxVersionPartDigits && part.IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>Takes the first number off a dotted version.</summary>
    /// <param name="rest">The version still to read; the number and its period are removed.</param>
    /// <returns>The number, or 0 when nothing is left.</returns>
    private static int NextPart(ref ReadOnlySpan<char> rest)
    {
        if (rest.IsEmpty)
        {
            return 0;
        }

        var end = rest.IndexOf('.');
        var part = end < 0 ? rest : rest[..end];
        rest = end < 0 ? default : rest[(end + 1)..];
        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    /// <summary>Reads the alternative handlers of a requirement.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The <c>/RH</c> value: a handler dictionary or an array of them.</param>
    /// <returns>The handlers that are dictionaries with a type, at most <see cref="PdfLimits.MaxRequirementHandlers"/>.</returns>
    private static PdfRequirementHandler[] ReadHandlers(PdfObjectStore store, PdfValue value)
    {
        if (value.AsArray() is { } array)
        {
            var handlers = new List<PdfRequirementHandler>();
            for (var i = 0; i < array.Count && handlers.Count < PdfLimits.MaxRequirementHandlers; i++)
            {
                if (ReadHandler(store, array.Get(i).AsDictionary()) is { } handler)
                {
                    handlers.Add(handler);
                }
            }

            return handlers.Count == 0 ? NoHandlers : [.. handlers];
        }

        return ReadHandler(store, value.AsDictionary()) is { } single ? [single] : NoHandlers;
    }

    /// <summary>Reads one handler dictionary. Its script is named, never loaded or run.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="dictionary">The handler, or <see langword="null"/>.</param>
    /// <returns>The handler, or <see langword="null"/> when it has no type.</returns>
    private static PdfRequirementHandler? ReadHandler(PdfObjectStore store, PdfDictionary? dictionary)
    {
        if (dictionary?.Get(store.Names.Intern(TypeKey)).SpellName(store) is not { Length: > 0 } type)
        {
            return null;
        }

        var isScript = string.Equals(type, ScriptHandler, StringComparison.Ordinal);
        var recognized = isScript || string.Equals(type, NoOpHandler, StringComparison.Ordinal);

        // A script name is valid only on a JS handler (§12.11.5, Table 276).
        var script = isScript ? dictionary.GetText(store.Names.Intern(ScriptKey)) : null;
        if (script is { Length: > PdfLimits.MaxRequirementText })
        {
            script = script[..PdfLimits.MaxRequirementText];
        }

        return new(type, script, recognized);
    }

    /// <summary>Reads the encryption settings an <c>Encryption</c> requirement asks for.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="encrypt">The <c>/Encrypt</c> dictionary, or <see langword="null"/>.</param>
    /// <returns>The settings; empty settings when the dictionary is missing.</returns>
    private static PdfRequirementEncryption ReadEncryption(PdfObjectStore store, PdfDictionary? encrypt)
    {
        if (encrypt is null)
        {
            return new(null, 0, 0);
        }

        var names = store.Names;
        return new(
            encrypt.Get(names.Intern("Filter"u8)).SpellName(store),
            encrypt.GetInt32(names.Intern("V"u8), 0),
            encrypt.GetInt32(names.Intern("R"u8), 0));
    }
}
