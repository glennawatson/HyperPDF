// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Protection;

namespace HyperPdf.App.ViewModels;

/// <summary>Checks the choices in the protect window before a copy is made, and words what they mean in plain language.</summary>
internal static class ProtectionChecks
{
    /// <summary>The lines a summary holds besides the notes: the saved file and a spare.</summary>
    private const int SummaryExtraLines = 2;

    /// <summary>Gets the choices of encryption, strongest first.</summary>
    internal static IReadOnlyList<ProtectionFormatChoice> Formats { get; } =
    [
        new(ProtectionFormat.Strong, "Strong (AES-256, suggested)", "The strongest common choice. Programs that follow PDF 2.0 open it; most current readers do."),
        new(ProtectionFormat.Compatible, "Compatible (AES-128)", "Older programs open it too. This is the only choice that can stop screen readers, so leave that permission on unless you are sure."),
        new(ProtectionFormat.Authenticated, "Strong with change detection (AES-256 GCM)", "Also detects changed data. Few programs open it yet; test it with the people who must read the file."),
    ];

    /// <summary>Finds the first thing wrong with the passwords and permissions.</summary>
    /// <param name="request">The request.</param>
    /// <param name="userRepeat">The user password typed a second time.</param>
    /// <param name="ownerRepeat">The owner password typed a second time.</param>
    /// <returns>The problem in plain words, or <see langword="null"/> when the choices are usable.</returns>
    internal static string? Problem(ProtectionRequest request, string userRepeat, string ownerRepeat)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.UserPassword, userRepeat, StringComparison.Ordinal))
        {
            return "The two reading passwords are not the same. Type the reading password again.";
        }

        if (!string.Equals(request.OwnerPassword, ownerRepeat, StringComparison.Ordinal))
        {
            return "The two owner passwords are not the same. Type the owner password again.";
        }

        var unrestricted = request.Permissions == ProtectionPermissions.All;
        if (unrestricted && request.UserPassword.Length == 0 && request.OwnerPassword.Length == 0)
        {
            return "Type a reading password or an owner password. Without one, the file would not be protected.";
        }

        var separateOwner = request.OwnerPassword.Length > 0 && !string.Equals(request.OwnerPassword, request.UserPassword, StringComparison.Ordinal);
        return unrestricted || separateOwner
            ? null
            : "To limit what people can do, add an owner password that differs from the reading password. Otherwise the reading password would also give every permission.";
    }

    /// <summary>Words the warning for a cleared accessibility permission.</summary>
    /// <param name="allowAccessibility">Whether screen readers are allowed.</param>
    /// <param name="choice">The chosen encryption.</param>
    /// <returns>The warning, or an empty string when screen readers are allowed.</returns>
    internal static string AccessibilityWarning(bool allowAccessibility, ProtectionFormatChoice? choice)
    {
        if (allowAccessibility)
        {
            return string.Empty;
        }

        return choice?.Format == ProtectionFormat.Compatible
            ? "Warning: people who use a screen reader will not be able to read this file. Leave this on unless you are sure no one needs it."
            : "The PDF 2.0 standard no longer lets a file block screen readers, so this choice has no effect on the strong formats and they can still read the file.";
    }

    /// <summary>Words the outcome of a run, one point per line.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <param name="action">What the run was doing, for the refusal line.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(ProtectionResult result, string path, string action)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Written)
        {
            return [$"No file was saved, so {action} did not happen. Here is why:", $"• {result.Refusal}"];
        }

        var lines = new List<string>(result.Notes.Count + SummaryExtraLines) { $"Saved as {Path.GetFileName(path)}. Your open file was not changed." };
        foreach (var note in result.Notes)
        {
            lines.Add($"• {note}");
        }

        return [.. lines];
    }
}
