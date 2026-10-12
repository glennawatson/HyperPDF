// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>
/// Loads and saves the remembered signature and initials. They live in their own file beside the settings, readable
/// only by their owner, and the file is deleted when nothing is remembered.
/// </summary>
[DebuggerDisplay("SignatureMarkStore: {FilePath}")]
public sealed class SignatureMarkStore
{
    /// <summary>The file name beside the settings file.</summary>
    internal const string FileName = "signature-marks.json";

    /// <summary>Initializes a new instance of the <see cref="SignatureMarkStore"/> class.</summary>
    /// <param name="filePath">The file.</param>
    public SignatureMarkStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
    }

    /// <summary>Gets the file path.</summary>
    public string FilePath { get; }

    /// <summary>Creates the store that sits beside a settings file.</summary>
    /// <param name="settings">The settings store.</param>
    /// <returns>The store.</returns>
    public static SignatureMarkStore Beside(SettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(Path.Combine(Path.GetDirectoryName(settings.FilePath) ?? string.Empty, FileName));
    }

    /// <summary>Loads the remembered marks, dropping any that cannot be placed.</summary>
    /// <returns>The marks; empty when the file is missing or unreadable.</returns>
    public SavedSignatureMarks Load()
    {
        var marks = PrivateJsonFile.Load(FilePath, SettingsJsonContext.Default.SavedSignatureMarks) ?? new();
        if (marks.Signature is { IsValid: false })
        {
            marks.Signature = null;
        }

        if (marks.Initials is { IsValid: false })
        {
            marks.Initials = null;
        }

        return marks;
    }

    /// <summary>Saves the remembered marks, or deletes the file when nothing is remembered.</summary>
    /// <param name="marks">The marks.</param>
    public void Save(SavedSignatureMarks marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        if (marks.Signature is null && marks.Initials is null)
        {
            File.Delete(FilePath);
            return;
        }

        PrivateJsonFile.Save(FilePath, marks, SettingsJsonContext.Default.SavedSignatureMarks);
    }
}
