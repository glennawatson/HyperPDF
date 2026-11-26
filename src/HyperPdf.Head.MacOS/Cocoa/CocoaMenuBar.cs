// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>
/// The menu bar macOS draws at the top of the screen: the application menu, then the viewer's menus. Each of the viewer's menus
/// is filled again just before it opens, so its entries follow what the document allows. A small Objective-C class receives the
/// menus' delegate calls and the items' actions and hands them to this object.
/// </summary>
[DebuggerDisplay("CocoaMenuBar: {_menus.Count} menus")]
internal sealed unsafe class CocoaMenuBar : IDisposable
{
    /// <summary>The name the receiving class is registered under.</summary>
    private const string TargetClassName = "HyperPdfMenuTarget";

    /// <summary>The Objective-C type encoding of a method that takes one object and returns nothing.</summary>
    private const string ObjectArgument = "v@:@";

    /// <summary>The class of a menu item.</summary>
    private const string MenuItemClass = "NSMenuItem";

    /// <summary>The state of a menu item that shows a tick.</summary>
    private const nint TickedState = 1;

    /// <summary>The menus the viewer fills, by their place on the bar.</summary>
    private readonly Dictionary<ulong, int> _menus = [];

    /// <summary>What each item of the menu open now does, by the item's tag.</summary>
    private readonly List<Action?> _actions = [];

    /// <summary>Asks the window to close, as Quit does.</summary>
    private readonly Action _quit;

    /// <summary>The handle the receiving object keeps to find this object.</summary>
    private GCHandle _self;

    /// <summary>The object that receives the menus' calls, or 0 before the menus are set.</summary>
    private ulong _target;

    /// <summary>The main menu, or 0 before the menus are set.</summary>
    private ulong _mainMenu;

    /// <summary>Makes each menu's entries.</summary>
    private Func<int, NativeMenuItem[]> _build = static _ => [];

    /// <summary>Initializes a new instance of the <see cref="CocoaMenuBar"/> class.</summary>
    /// <param name="quit">Asks the window to close, asking first about unsaved changes.</param>
    internal CocoaMenuBar(Action quit) => _quit = quit;

    /// <summary>Gets the class of the receiving object, registered with the runtime once.</summary>
    private static nint TargetClass { get; } = RegisterTargetClass();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_mainMenu != 0)
        {
            NativeMethods.SendVoid((nint)_mainMenu, Selectors.Release);
            _mainMenu = 0;
        }

        if (_target != 0)
        {
            NativeMethods.SendVoid((nint)_target, Selectors.Release);
            _target = 0;
        }

        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }

    /// <summary>Shows the application menu and the viewer's menus on the menu bar.</summary>
    /// <param name="application">The application object.</param>
    /// <param name="titles">The viewer's menus' names.</param>
    /// <param name="build">Makes each menu's entries.</param>
    /// <param name="words">The words of the application menu.</param>
    internal void Show(nint application, IReadOnlyList<string> titles, Func<int, NativeMenuItem[]> build, NativeAppMenu words)
    {
        _build = build;
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            EnsureTarget();
            var mainMenu = NewMenu(string.Empty);
            AddSubmenu(mainMenu, ApplicationMenu(words));
            _menus.Clear();
            for (var i = 0; i < titles.Count; i++)
            {
                var menu = NewMenu(titles[i]);
                NativeMethods.SendVoid(menu, Selectors.SetDelegate, (nint)_target);
                _menus[(ulong)menu] = i;
                AddSubmenu(mainMenu, menu);
            }

            NativeMethods.SendVoid(application, Selectors.SetMainMenu, mainMenu);
            if (_mainMenu != 0)
            {
                NativeMethods.SendVoid((nint)_mainMenu, Selectors.Release);
            }

            _mainMenu = (ulong)mainMenu;
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Registers the class whose object receives the menus' calls.</summary>
    /// <returns>The class.</returns>
    private static nint RegisterTargetClass()
    {
        // A window made a second time in the process finds the class already registered.
        var existing = NativeMethods.GetClass(TargetClassName);
        if (existing != 0)
        {
            return existing;
        }

        var cls = NativeMethods.AllocateClassPair(NativeMethods.GetClass("NSObject"), TargetClassName, 0);
        _ = NativeMethods.AddMethod(cls, Selectors.MenuNeedsUpdate, (nint)(delegate* unmanaged<nint, nint, nint, void>)&MenuNeedsUpdate, ObjectArgument);
        _ = NativeMethods.AddMethod(cls, Selectors.ItemChosen, (nint)(delegate* unmanaged<nint, nint, nint, void>)&ItemChosen, ObjectArgument);
        _ = NativeMethods.AddMethod(cls, Selectors.QuitChosen, (nint)(delegate* unmanaged<nint, nint, nint, void>)&QuitChosen, ObjectArgument);
        NativeMethods.RegisterClassPair(cls);
        return cls;
    }

    /// <summary>Fills a menu just before it opens.</summary>
    /// <param name="self">The receiving object.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="menu">The menu.</param>
    [UnmanagedCallersOnly]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MenuNeedsUpdate(nint self, nint selector, nint menu) => From(self)?.Fill(menu);

    /// <summary>Runs the item chosen.</summary>
    /// <param name="self">The receiving object.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="item">The menu item.</param>
    [UnmanagedCallersOnly]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ItemChosen(nint self, nint selector, nint item) => From(self)?.Choose(item);

    /// <summary>Asks the window to close, as Quit does.</summary>
    /// <param name="self">The receiving object.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="item">The menu item.</param>
    [UnmanagedCallersOnly]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuitChosen(nint self, nint selector, nint item) => From(self)?._quit();

    /// <summary>Finds the menu bar a receiving object belongs to.</summary>
    /// <param name="self">The receiving object.</param>
    /// <returns>The menu bar, or <see langword="null"/> once it is disposed.</returns>
    private static CocoaMenuBar? From(nint self)
    {
        var handle = *NativeMethods.IndexedIvars(self);
        return handle == 0 ? null : GCHandle.FromIntPtr(handle).Target as CocoaMenuBar;
    }

    /// <summary>Makes an empty menu whose items are enabled as the viewer says rather than by AppKit.</summary>
    /// <param name="title">The menu's name.</param>
    /// <returns>The menu, which the caller releases.</returns>
    private static nint NewMenu(string title)
    {
        var menu = NativeMethods.Send(NativeMethods.Send(NativeMethods.GetClass("NSMenu"), Selectors.Alloc), Selectors.InitWithTitle, Cocoa.Text(title));
        NativeMethods.SendVoidFlag(menu, Selectors.SetAutoenablesItems, 0);
        return menu;
    }

    /// <summary>Adds a menu to another as an item that opens it, and lets the item own it.</summary>
    /// <param name="parent">The menu it goes in.</param>
    /// <param name="menu">The menu; released here.</param>
    private static void AddSubmenu(nint parent, nint menu)
    {
        var item = NativeMethods.Send(NativeMethods.Send(NativeMethods.GetClass(MenuItemClass), Selectors.Alloc), Selectors.Init);
        NativeMethods.SendVoid(item, Selectors.SetSubmenu, menu);
        NativeMethods.SendVoid(parent, Selectors.AddItem, item);
        NativeMethods.SendVoid(item, Selectors.Release);
        NativeMethods.SendVoid(menu, Selectors.Release);
    }

    /// <summary>Makes a menu item.</summary>
    /// <param name="title">Its words.</param>
    /// <param name="action">The selector it sends.</param>
    /// <param name="shortcut">The shortcut it shows.</param>
    /// <returns>The item, which the caller releases.</returns>
    private static nint NewItem(string title, nint action, in CocoaShortcut shortcut)
    {
        var allocated = NativeMethods.Send(NativeMethods.GetClass(MenuItemClass), Selectors.Alloc);
        var item = NativeMethods.SendTriple(allocated, Selectors.InitMenuItem, Cocoa.Text(title), action, Cocoa.Text(shortcut.Key));
        NativeMethods.SendVoid(item, Selectors.SetKeyEquivalentModifierMask, (nint)(long)shortcut.Modifiers);
        return item;
    }

    /// <summary>Adds an item that sends a standard action to the application.</summary>
    /// <param name="menu">The menu.</param>
    /// <param name="title">Its words.</param>
    /// <param name="action">The application's action.</param>
    private static void AddStandard(nint menu, string title, nint action)
    {
        var item = NewItem(title, action, CocoaShortcut.None);
        NativeMethods.SendVoid(item, Selectors.SetTarget, NativeMethods.Send(NativeMethods.GetClass("NSApplication"), Selectors.SharedApplication));
        NativeMethods.SendVoid(menu, Selectors.AddItem, item);
        NativeMethods.SendVoid(item, Selectors.Release);
    }

    /// <summary>Makes the receiving object once, keeping a handle to this object in it.</summary>
    private void EnsureTarget()
    {
        if (_target != 0)
        {
            return;
        }

        _self = GCHandle.Alloc(this, GCHandleType.Weak);
        var target = NativeMethods.Send(NativeMethods.CreateInstance(TargetClass, (nuint)sizeof(nint)), Selectors.Init);
        *NativeMethods.IndexedIvars(target) = GCHandle.ToIntPtr(_self);
        _target = (ulong)target;
    }

    /// <summary>Makes the application menu: hide, hide others, show all and quit.</summary>
    /// <param name="words">Its words.</param>
    /// <returns>The menu, which the caller releases.</returns>
    private nint ApplicationMenu(NativeAppMenu words)
    {
        var menu = NewMenu(string.Empty);
        AddStandard(menu, words.Hide, Selectors.Hide);
        AddStandard(menu, words.HideOthers, Selectors.HideOthers);
        AddStandard(menu, words.ShowAll, Selectors.UnhideAll);
        NativeMethods.SendVoid(menu, Selectors.AddItem, NativeMethods.Send(NativeMethods.GetClass(MenuItemClass), Selectors.SeparatorItem));

        // Command and Q reaches the viewer as a key, which asks to close the window, so the item only shows it.
        var quit = NewItem(words.Quit, Selectors.QuitChosen, new("q", CocoaShortcut.From("Ctrl+Q").Modifiers));
        NativeMethods.SendVoid(quit, Selectors.SetTarget, (nint)_target);
        NativeMethods.SendVoid(menu, Selectors.AddItem, quit);
        NativeMethods.SendVoid(quit, Selectors.Release);
        return menu;
    }

    /// <summary>Fills one of the viewer's menus with its entries as they are now.</summary>
    /// <param name="menu">The menu.</param>
    private void Fill(nint menu)
    {
        if (!_menus.TryGetValue((ulong)menu, out var index))
        {
            return;
        }

        // Only one menu is open at a time, so the actions of the menus filled before are no longer needed.
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            _actions.Clear();
            NativeMethods.SendVoid(menu, Selectors.RemoveAllItems);
            AddItems(menu, _build(index));
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Adds entries to a menu, with a menu of its own for each entry that has entries.</summary>
    /// <param name="menu">The menu.</param>
    /// <param name="items">The entries.</param>
    private void AddItems(nint menu, NativeMenuItem[] items)
    {
        foreach (var entry in items)
        {
            if (entry.IsSeparator)
            {
                NativeMethods.SendVoid(menu, Selectors.AddItem, NativeMethods.Send(NativeMethods.GetClass(MenuItemClass), Selectors.SeparatorItem));
                continue;
            }

            var item = NewItem(entry.Title, Selectors.ItemChosen, CocoaShortcut.From(entry.Shortcut));
            NativeMethods.SendVoid(item, Selectors.SetTarget, (nint)_target);
            NativeMethods.SendVoid(item, Selectors.SetTag, _actions.Count);
            _actions.Add(entry.Run);
            NativeMethods.SendVoidFlag(item, Selectors.SetEnabled, entry.IsEnabled ? (byte)1 : (byte)0);
            NativeMethods.SendVoid(item, Selectors.SetState, entry.IsChecked ? TickedState : 0);
            if (entry.Children.Length > 0)
            {
                var submenu = NewMenu(entry.Title);
                AddItems(submenu, entry.Children);
                NativeMethods.SendVoid(item, Selectors.SetSubmenu, submenu);
                NativeMethods.SendVoid(submenu, Selectors.Release);
            }

            NativeMethods.SendVoid(menu, Selectors.AddItem, item);
            NativeMethods.SendVoid(item, Selectors.Release);
        }
    }

    /// <summary>Runs the action of a chosen item.</summary>
    /// <param name="item">The item.</param>
    private void Choose(nint item)
    {
        var tag = NativeMethods.Send(item, Selectors.Tag);
        if ((ulong)tag < (ulong)_actions.Count)
        {
            _actions[(int)tag]?.Invoke();
        }
    }
}
