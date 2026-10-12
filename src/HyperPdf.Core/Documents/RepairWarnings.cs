// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Documents;

/// <summary>Puts the repairs made to a damaged document into plain words.</summary>
public static class RepairWarnings
{
    /// <summary>The calm summary shown when a document was repaired on open.</summary>
    public static readonly string Summary = "This file was damaged. We repaired it to show it. Saving a copy will fix it.";

    /// <summary>The most repairs listed in the details; the rest are counted.</summary>
    private const int MaxListed = 20;

    /// <summary>Describes the repairs, one per line.</summary>
    /// <param name="repairs">The repairs.</param>
    /// <returns>The details, or <see langword="null"/> when nothing was repaired.</returns>
    public static string? Describe(IReadOnlyList<RepairNote> repairs)
    {
        ArgumentNullException.ThrowIfNull(repairs);
        if (repairs.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        var listed = Math.Min(repairs.Count, MaxListed);
        for (var i = 0; i < listed; i++)
        {
            AppendLine(builder, repairs[i]);
        }

        if (repairs.Count > listed)
        {
            _ = builder.Append(CultureInfo.CurrentCulture, $"And {repairs.Count - listed} more.");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Appends one repair and its object number.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="repair">The repair.</param>
    private static void AppendLine(StringBuilder builder, RepairNote repair)
    {
        _ = builder.Append(repair.Description);
        if (repair.ObjectNumber > 0)
        {
            _ = builder.Append(CultureInfo.CurrentCulture, $" (object {repair.ObjectNumber})");
        }

        _ = builder.AppendLine();
    }
}
