// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Picks the icon drawn beside a menu entry from its explicit icon name, its radio state, or what it does.</summary>
internal static class MenuIcons
{
    /// <summary>Icons by the name a menu entry gives, made once so a lookup makes nothing.</summary>
    private static readonly Dictionary<string, ChromeIcon> ByName = NameTable();

    /// <summary>Icons by the English menu header without its access-key mark.</summary>
    private static readonly Dictionary<string, ChromeIcon> ByHeader = new(StringComparer.Ordinal)
    {
        ["Open…"] = ChromeIcon.Open,
        ["Open Recent"] = ChromeIcon.Open,
        ["Reopen Closed Tab"] = ChromeIcon.Undo,
        ["Document"] = ChromeIcon.Document,
        ["Save"] = ChromeIcon.Save,
        ["Save As…"] = ChromeIcon.Save,
        ["Save a Copy"] = ChromeIcon.Copy,
        ["Reload"] = ChromeIcon.Redo,
        ["Print…"] = ChromeIcon.Print,
        ["Print with the System Dialog…"] = ChromeIcon.Print,
        ["Document Properties…"] = ChromeIcon.Document,
        ["Compare with Another Document…"] = ChromeIcon.Compare,
        ["Remove Hidden Information…"] = ChromeIcon.Close,
        ["Show the Document in Its Folder"] = ChromeIcon.Open,
        ["View"] = ChromeIcon.Dual,
        ["Go"] = ChromeIcon.Forward,
        ["Page Layout and Zoom"] = ChromeIcon.FitPage,
        ["Reading Aids"] = ChromeIcon.Speaker,
        ["Tools"] = ChromeIcon.Ruler,
        ["Measure, Sign and Edit"] = ChromeIcon.FillSign,
        ["Pages"] = ChromeIcon.Thumbnails,
        ["Add to Pages"] = ChromeIcon.Stamp,
        ["Bookmarks"] = ChromeIcon.Outline,
        ["Signatures and Tags"] = ChromeIcon.Certificate,
        ["Tabs"] = ChromeIcon.Document,
        ["Window"] = ChromeIcon.Maximize,
        ["Preferences…"] = ChromeIcon.Choose,
        ["Licences…"] = ChromeIcon.Document,
        ["Zoom In"] = ChromeIcon.ZoomIn,
        ["Zoom Out"] = ChromeIcon.ZoomOut,
        ["Actual Size"] = ChromeIcon.FitPage,
        ["Fit Width"] = ChromeIcon.FitWidth,
        ["Fit Page"] = ChromeIcon.FitPage,
        ["Rotate Left"] = ChromeIcon.RotateLeft,
        ["Rotate Right"] = ChromeIcon.RotateRight,
        ["First Page"] = ChromeIcon.PageUp,
        ["Previous Page"] = ChromeIcon.PageUp,
        ["Next Page"] = ChromeIcon.PageDown,
        ["Last Page"] = ChromeIcon.PageDown,
        ["Back"] = ChromeIcon.Back,
        ["Forward"] = ChromeIcon.Forward,
        ["Find…"] = ChromeIcon.Search,
        ["Undo"] = ChromeIcon.Undo,
        ["Redo"] = ChromeIcon.Redo,
        ["Next Tab"] = ChromeIcon.Forward,
        ["Previous Tab"] = ChromeIcon.Back,
        ["Go to a Document…"] = ChromeIcon.Search,
        ["Close"] = ChromeIcon.Close,
        ["Close Tab"] = ChromeIcon.Close,
        ["Close Other Tabs"] = ChromeIcon.Close,
        ["Close All Tabs"] = ChromeIcon.Close,
        ["New Window"] = ChromeIcon.Add,
        ["Convert Pictures to a PDF…"] = ChromeIcon.Add,
        ["Search a Folder of Documents…"] = ChromeIcon.Search,
        ["Forget the Recent Documents"] = ChromeIcon.Close,
        ["Copy"] = ChromeIcon.Copy,
        ["Read Aloud from Here"] = ChromeIcon.Speaker,
        ["Highlight"] = ChromeIcon.Highlight,
        ["Underline"] = ChromeIcon.Underline,
        ["Strike Out"] = ChromeIcon.Strike,
        ["Squiggly Underline"] = ChromeIcon.Underline,
        ["Edit Text"] = ChromeIcon.Text,
        ["Text Properties…"] = ChromeIcon.TextSize,
        ["Edit Note"] = ChromeIcon.Note,
        ["Add Note"] = ChromeIcon.Note,
        ["Add Note Here"] = ChromeIcon.Note,
        ["Type Text Here"] = ChromeIcon.Text,
        ["Annotate…"] = ChromeIcon.Highlight,
        ["Reply"] = ChromeIcon.Note,
        ["Status"] = ChromeIcon.Done,
        ["Colour"] = ChromeIcon.Shape,
        ["Accepted"] = ChromeIcon.Done,
        ["Rejected"] = ChromeIcon.Close,
        ["Cancelled"] = ChromeIcon.Stop,
        ["Completed"] = ChromeIcon.Done,
        ["Check the Signatures"] = ChromeIcon.Certificate,
        ["Sign with a Certificate…"] = ChromeIcon.Signature,
        ["Check the Accessibility Tags"] = ChromeIcon.Tags,
        ["Edit the Selected Tag…"] = ChromeIcon.Tags,
        ["Write the Tag Changes to the Document"] = ChromeIcon.Save,
        ["Add a Bookmark Here…"] = ChromeIcon.Add,
        ["Rename the Bookmark…"] = ChromeIcon.Text,
        ["Remove the Bookmark"] = ChromeIcon.Close,
        ["Move Up"] = ChromeIcon.PageUp,
        ["Move Down"] = ChromeIcon.PageDown,
        ["Move In"] = ChromeIcon.Indent,
        ["Move Out"] = ChromeIcon.Outdent,
        ["Turn the Page Right"] = ChromeIcon.RotateRight,
        ["Turn the Page Left"] = ChromeIcon.RotateLeft,
        ["Move the Page Earlier"] = ChromeIcon.PageUp,
        ["Move the Page Later"] = ChromeIcon.PageDown,
        ["Duplicate the Page"] = ChromeIcon.Copy,
        ["Delete the Page"] = ChromeIcon.Close,
        ["Select Every Page"] = ChromeIcon.CheckOn,
        ["Add the Page I Am On to the Selection"] = ChromeIcon.Add,
        ["Extract Pages…"] = ChromeIcon.Save,
        ["Insert Pages from Files…"] = ChromeIcon.Add,
        ["Insert Pictures as Pages…"] = ChromeIcon.Add,
        ["Merge Files…"] = ChromeIcon.Add,
        ["Play or Pause"] = ChromeIcon.Play,
        ["Read This Page"] = ChromeIcon.Speaker,
        ["Read to the End"] = ChromeIcon.PageDown,
        ["Sentence Before"] = ChromeIcon.PreviousSentence,
        ["Next Sentence"] = ChromeIcon.NextSentence,
        ["Get a Better Voice…"] = ChromeIcon.Add,
        ["Larger Focus Text"] = ChromeIcon.ZoomIn,
        ["Smaller Focus Text"] = ChromeIcon.ZoomOut,
        ["Scroll Faster"] = ChromeIcon.Forward,
        ["Scroll Slower"] = ChromeIcon.Back,
        ["Recognise Text"] = ChromeIcon.Text,
        ["Recognise Text in Scanned Pages"] = ChromeIcon.Text,
        ["Recognise Text in Another Language…"] = ChromeIcon.Choose,
        ["Choose the Language to Recognise…"] = ChromeIcon.Choose,
        ["Manage the Recognition Languages…"] = ChromeIcon.Choose,
        ["Stop Recognising"] = ChromeIcon.Stop,
        ["Next Place in the Article"] = ChromeIcon.Forward,
        ["Previous Place in the Article"] = ChromeIcon.Back,
        ["Place My Signature…"] = ChromeIcon.Signature,
        ["Place My Initials…"] = ChromeIcon.Text,
        ["Put the Mark Down"] = ChromeIcon.Done,
        ["Adjust the Mark"] = ChromeIcon.Cursor,
        ["Make the Mark Bigger"] = ChromeIcon.ZoomIn,
        ["Make the Mark Smaller"] = ChromeIcon.ZoomOut,
        ["Remove the Mark"] = ChromeIcon.Close,
        ["Stop Placing"] = ChromeIcon.Stop,
    };

    /// <summary>Gets the icon drawn before a menu entry's text.</summary>
    /// <param name="entry">The menu entry.</param>
    /// <returns>The icon.</returns>
    internal static ChromeIcon Of(MenuEntry entry)
    {
        if (entry.Icon is { Length: > 0 } explicitName && ByName.TryGetValue(explicitName, out var named))
        {
            return named;
        }

        if (entry.Kind == MenuEntryKind.RadioItem)
        {
            return entry.IsChecked ? ChromeIcon.RadioOn : ChromeIcon.RadioOff;
        }

        if (entry.Swatch is not null)
        {
            return ChromeIcon.Shape;
        }

        var clean = ChromeText.WithoutAccessMark(entry.Header);
        return ByHeader.TryGetValue(clean, out var found) ? found : FallbackIcon(entry, clean);
    }

    /// <summary>Picks a sensible icon for dynamic or parameterised menu entries.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="clean">The entry text without its access-key mark.</param>
    /// <returns>The icon.</returns>
    private static ChromeIcon FallbackIcon(MenuEntry entry, string clean)
    {
        if (!entry.IsEnabled && entry.Command is null && entry.Kind == MenuEntryKind.Item)
        {
            return ChromeIcon.None;
        }

        if (clean.StartsWith("Delete ", StringComparison.Ordinal) || clean.StartsWith("Remove ", StringComparison.Ordinal))
        {
            return ChromeIcon.Close;
        }

        if (clean.StartsWith("Add ", StringComparison.Ordinal))
        {
            return ChromeIcon.Add;
        }

        if (clean.StartsWith("Zoom to ", StringComparison.Ordinal))
        {
            return ChromeIcon.ZoomIn;
        }

        if (clean.StartsWith("Protect", StringComparison.Ordinal))
        {
            return ChromeIcon.Lock;
        }

        return entry.Kind == MenuEntryKind.Submenu ? ChromeIcon.More : ChromeIcon.Choose;
    }

    /// <summary>Makes the table of every icon by its name, as a menu entry names it.</summary>
    /// <returns>The table.</returns>
    private static Dictionary<string, ChromeIcon> NameTable()
    {
        var values = Enum.GetValues<ChromeIcon>();
        var table = new Dictionary<string, ChromeIcon>(values.Length, StringComparer.Ordinal);
        foreach (var icon in values)
        {
            table[icon.ToString()] = icon;
        }

        return table;
    }
}
