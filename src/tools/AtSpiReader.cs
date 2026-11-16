// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
namespace HyperPdf.Scripts;

/// <summary>
/// A headless stand-in for a screen reader such as Orca. It watches the accessibility bus for focus announcements, moves focus to every element that can take it, as a reader or
/// a switch device does, and reports what a reader would say for each: the element's role and name, and whether the viewer announced the change of focus at all.
/// </summary>
internal static class AtSpiReader
{
    /// <summary>The component interface, which moves focus.</summary>
    private const string Component = "org.a11y.atspi.Component";

    /// <summary>The state number of an element that can take keyboard focus.</summary>
    private const int FocusableState = 11;

    /// <summary>The state number of an element that has keyboard focus.</summary>
    private const int FocusedState = 12;

    /// <summary>The word of a state set that holds the focused state.</summary>
    private const int FocusedWord = FocusedState / WordBits;

    /// <summary>The word of a state set that holds the focusable state.</summary>
    private const int FocusableWord = FocusableState / WordBits;

    /// <summary>The bits in one word of a state set.</summary>
    private const int WordBits = 32;

    /// <summary>The milliseconds to wait for an announcement after focus moves.</summary>
    private const int AnnounceWaitMilliseconds = 600;

    /// <summary>The milliseconds between looks at whether an announcement came.</summary>
    private const int PollMilliseconds = 25;

    /// <summary>The path of the application's root element.</summary>
    private const string RootPath = "/org/a11y/atspi/accessible/root";

    /// <summary>What the monitor's lines must hold to count as an announcement of focus.</summary>
    private const string StateChanged = "StateChanged";

    /// <summary>The state name an announcement of focus carries.</summary>
    private const string Focused = "focused";

    /// <summary>Reads the viewer as a screen reader would.</summary>
    /// <param name="address">The accessibility bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <returns>0 when every element that takes focus is named and announced.</returns>
    internal static async Task<int> ReadAsync(string address, string application)
    {
        var heard = new List<string>();
        using var monitor = StartMonitor(address, heard);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(AnnounceWaitMilliseconds)).ConfigureAwait(false);
            var focusable = await FindFocusableAsync(address, application).ConfigureAwait(false);
            var silent = 0;
            foreach (var element in focusable)
            {
                var result = await FocusAsync(address, application, element, heard).ConfigureAwait(false);
                silent += result.Announced ? 0 : 1;
                await Console.Out.WriteLineAsync(Format(element, result)).ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync($"{focusable.Count} elements take focus; {silent} were not announced.").ConfigureAwait(false);
            return silent == 0 && focusable.Count > 0 ? 0 : 1;
        }
        finally
        {
            // The monitor holds the caller's pipes open for as long as it runs.
            monitor.Kill(true);
        }
    }

    /// <summary>Describes what a reader would make of moving focus to an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="result">What happened.</param>
    /// <returns>The line to print.</returns>
    private static string Format(Element element, FocusResult result) =>
        $"{(result.Announced ? "READER" : "SILENT")}> {element.Role}: '{element.Name}' (focus {(result.Accepted ? "taken" : "refused")})";

    /// <summary>Starts watching the bus for announcements of focus.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="heard">Receives each announcement, as the monitor prints it.</param>
    /// <returns>The monitor process.</returns>
    /// <exception cref="InvalidOperationException">busctl did not start.</exception>
    private static Process StartMonitor(string address, List<string> heard)
    {
        var start = new ProcessStartInfo("busctl") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { $"--address={address}", "monitor", "--json=short", "--no-pager" })
        {
            start.ArgumentList.Add(argument);
        }

        var monitor = Process.Start(start) ?? throw new InvalidOperationException("busctl did not start.");
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await monitor.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.Contains(StateChanged, StringComparison.Ordinal) && line.Contains(Focused, StringComparison.Ordinal))
                {
                    lock (heard)
                    {
                        heard.Add(line);
                    }
                }
            }
        });
        return monitor;
    }

    /// <summary>Lists the elements of the application that can take keyboard focus.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <returns>The elements, in tree order.</returns>
    /// <exception cref="InvalidDataException">A child has no object path.</exception>
    private static async Task<List<Element>> FindFocusableAsync(string address, string application)
    {
        var found = new List<Element>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(RootPath);
        while (pending.TryPop(out var path))
        {
            if (!seen.Add(path))
            {
                continue;
            }

            try
            {
                if (await IsFocusableAsync(address, application, path).ConfigureAwait(false))
                {
                    found.Add(await DescribeAsync(address, application, path).ConfigureAwait(false));
                }

                using var children = await AccessibilityCheck.CallAsync(address, application, "call", path, "GetChildren").ConfigureAwait(false);
                var list = children.RootElement.GetProperty("data")[0];
                for (var index = list.GetArrayLength() - 1; index >= 0; index--)
                {
                    pending.Push(list[index][1].GetString() ?? throw new InvalidDataException("An accessible child has no object path."));
                }
            }
            catch (InvalidOperationException) when (path != RootPath)
            {
                // An element can go away between being listed and being asked about.
            }
        }

        return found;
    }

    /// <summary>Tells whether an element's state set holds the focusable state.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <param name="path">The element's path.</param>
    /// <returns><see langword="true"/> when it can take focus.</returns>
    private static async Task<bool> IsFocusableAsync(string address, string application, string path)
    {
        using var reply = await AccessibilityCheck.CallAsync(address, application, "call", path, "GetState").ConfigureAwait(false);
        var words = reply.RootElement.GetProperty("data")[0];
        return FocusableWord < words.GetArrayLength() && ((words[FocusableWord].GetUInt32() >> (FocusableState % WordBits)) & 1) != 0;
    }

    /// <summary>Tells whether an element already has focus.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <param name="path">The element's path.</param>
    /// <returns><see langword="true"/> when its state set holds the focused state.</returns>
    private static async Task<bool> IsFocusedAsync(string address, string application, string path)
    {
        using var reply = await AccessibilityCheck.CallAsync(address, application, "call", path, "GetState").ConfigureAwait(false);
        var words = reply.RootElement.GetProperty("data")[0];
        return FocusedWord < words.GetArrayLength() && ((words[FocusedWord].GetUInt32() >> (FocusedState % WordBits)) & 1) != 0;
    }

    /// <summary>Reads an element's role and name, which is what a reader says when it takes focus.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <param name="path">The element's path.</param>
    /// <returns>The element.</returns>
    private static async Task<Element> DescribeAsync(string address, string application, string path)
    {
        using var role = await AccessibilityCheck.CallAsync(address, application, "call", path, "GetRoleName").ConfigureAwait(false);
        using var name = await AccessibilityCheck.CallAsync(address, application, "get-property", path, "Name").ConfigureAwait(false);
        return new(path, role.RootElement.GetProperty("data")[0].GetString() ?? string.Empty, (name.RootElement.GetProperty("data").GetString() ?? string.Empty).Trim());
    }

    /// <summary>Moves focus to an element and waits to see whether the viewer announced it.</summary>
    /// <param name="address">The bus address.</param>
    /// <param name="application">The application's bus name.</param>
    /// <param name="element">The element.</param>
    /// <param name="heard">The announcements heard so far.</param>
    /// <returns>Whether focus was taken and whether an announcement of focus arrived.</returns>
    private static async Task<FocusResult> FocusAsync(string address, string application, Element element, List<string> heard)
    {
        if (await IsFocusedAsync(address, application, element.Path).ConfigureAwait(false))
        {
            // Focus that is already there does not change, so nothing is announced and nothing needs to be.
            return new(true, true);
        }

        int before;
        lock (heard)
        {
            before = heard.Count;
        }

        using var reply = await AccessibilityCheck.CallAsync(address, application, "call", element.Path, Component, "GrabFocus", []).ConfigureAwait(false);
        var accepted = reply.RootElement.GetProperty("data")[0].GetBoolean();
        for (var waited = 0; waited < AnnounceWaitMilliseconds; waited += PollMilliseconds)
        {
            lock (heard)
            {
                if (heard.Count > before)
                {
                    return new(accepted, true);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(PollMilliseconds)).ConfigureAwait(false);
        }

        return new(accepted, false);
    }

    /// <summary>What happened when focus was moved to an element.</summary>
    /// <param name="Accepted">Whether the viewer said it took focus.</param>
    /// <param name="Announced">Whether an announcement of focus followed.</param>
    private readonly record struct FocusResult(bool Accepted, bool Announced);

    /// <summary>An element that takes focus.</summary>
    /// <param name="Path">Its object path.</param>
    /// <param name="Role">Its role name.</param>
    /// <param name="Name">Its spoken name.</param>
    private sealed record Element(string Path, string Role, string Name);
}
