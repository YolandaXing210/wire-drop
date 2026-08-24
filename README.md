# WireDrop

[github.com/YolandaXing210/wire-drop](https://github.com/YolandaXing210/wire-drop)

Pull a wire off any Grasshopper port, let go over empty canvas, and a panel opens
listing everything that port can connect to. Type to narrow it, Enter to place the
component already wired up.

Rhino 8, Windows and macOS.

## Install

The built plugin is already installed on this machine. To rebuild and reinstall:

```bash
cd src/WireDrop
dotnet build -c Release
```

Then copy `src/WireDrop/bin/Release/WireDrop.gha` into Grasshopper's Libraries folder:

- **macOS** `~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/`
- **Windows** `%APPDATA%\Grasshopper\Libraries\` — then right-click the file, Properties, **Unblock**.

Restart Rhino. Grasshopper loads `.gha` files only at startup.

If Rhino is not in the default location, pass its System folder:

```bash
dotnet build -c Release -p:RhinoSystemDir="/path/to/Rhino/System"
```

## Sharing it

`./package.sh` builds both distributables into `dist/`:

| File | For |
|---|---|
| `wiredrop-0.1.0-rh8_0-any.yak` | Rhino's Package Manager |
| `WireDrop-0.1.0.zip` | Manual install — contains the `.gha` and `INSTALL.txt` |

A friend installs the `.yak` with Rhino running:

```
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" install --source . wiredrop
```

or on Windows, `"C:\Program Files\Rhino 8\System\yak.exe"`. Then restart Rhino.

The `rh8_0` tag means any Rhino 8, Windows or macOS. It comes from the Grasshopper
version the plugin compiles against — `GrasshopperVersion` in the csproj, deliberately
pinned to the oldest supported Rhino 8 rather than the newest installed, since that
number becomes the version floor for everyone downstream.

To publish it publicly so it shows up in Package Manager search, `yak login` then
`yak push`. That puts it on McNeel's public server under your Rhino account — a
one-way, public action, so it is not part of `package.sh`.

## Using it

| | |
|---|---|
| Drag a port to empty canvas | Opens the panel |
| Type | Narrows the list |
| <kbd>↑</kbd> <kbd>↓</kbd> | Move selection |
| <kbd>PgUp</kbd> <kbd>PgDn</kbd> | Jump a screen |
| <kbd>←</kbd> <kbd>→</kbd> | Walk the category row |
| <kbd>Tab</kbd> | Widen past compatible components to the whole library |
| <kbd>Enter</kbd> / click | Place the component and wire it |
| <kbd>Esc</kbd> | Cancel |

Works in both directions: drag from an **output** and the list shows inputs that
accept it; drag from an **input** and it shows outputs that produce it. One
<kbd>Ctrl</kbd>/<kbd>Cmd</kbd>+<kbd>Z</kbd> undoes both the component and the wire.

Each row is a component and one of its ports, by full port name — *Construct Point ▸ X
coordinate*, not *▸ X*. A component with several connectable ports contributes a row
each, the first carrying the icon and name and the rest marked `↳`.

The category row above the list shows every category, wrapping onto as many rows as it
needs, in Grasshopper's own ribbon order — Params, Maths, Sets, Vector, Curve, Surface,
Mesh, Intersect, Transform, Display, then third-party tabs alphabetically. Names only;
the totals are in the footer. Categories are still tallied across the unfiltered results,
so choosing one never hides where the rest went, and the row keeps a fixed height so the
list does not jump while you type.

Grasshopper does not expose that order: the component server keeps categories in a
SortedList keyed by name, so registration order is lost before anything can read it.
`CategoryOrder` restates the order Grasshopper declares when it builds the server.

## How it works

Grasshopper's own mouse handlers are subscribed in the canvas constructor, so a
plugin's handlers always run *after* them — by the time `MouseUp` arrives the wire
interaction has already been destroyed. So `CanvasWatcher` captures the drag state on
every `MouseMove` (source port, direction, and whether the wire is over a target) and
`MouseUp` only reads what was captured. A non-null target means Grasshopper already
made the connection and WireDrop stays out of the way.

That state lives in private fields of `GH_WireInteraction`. `WireInteractionFields`
binds them once at load and validates their types; if a future Grasshopper renames
any of them the plugin goes dormant rather than breaking the canvas. No runtime
patching, no Harmony.

Ports are only knowable by instantiating every installed component, which costs a
couple of seconds across ~1500 proxies. `ComponentCatalog` slices that work across
Rhino idle ticks after load, so it is normally finished long before the first drop.

### Keyboard

Keys are handled in `KeyDown`, not `ProcessCmdKey`. The latter is Windows message-loop
plumbing that Rhino's cross-platform WinForms does not raise, so on macOS the arrows
and Enter never reached the panel at all. A `TextBox` also swallows the arrows for
caret movement and Tab for focus navigation, so `SearchBox` overrides `IsInputKey` to
claim them first. The handler is attached to both the form and the search field, since
which one a given Rhino build raises is not knowable up front; a re-entrancy guard
keyed on the event instance stops the second acting twice.

`KeyMap` holds the mapping as a pure function of the virtual key code, free of
Windows.Forms, so the whole keyboard contract is unit-tested.

### Dismissing the panel

Anything that is not "choose something in this panel" cancels it. `Deactivate` alone is
not dependable — Rhino's cross-platform WinForms does not always raise it, and on macOS
the click that refocuses the Rhino window can be swallowed — so the panel also listens
to the canvas directly for `MouseDown`, and to `ViewportChanged` because panning or
zooming moves the canvas out from under the drop point the panel was anchored to.
Nothing can dismiss it until `OnShown` has run, or a build that raises `Deactivate`
during showing would make it flash and vanish.

### Scrolling

The wheel scrolls, and the bar on the right can be dragged. The bar lives inside the
list rectangle, so it is hit-tested before rows — otherwise a grab would be read as
picking whatever row sits underneath. Clicking the track jumps so the thumb centres on
the cursor. `ScrollBar` holds the geometry and its inverse; the round trip from scroll
position to thumb position and back is unit-tested, along with clamping past both ends.

### Matching Grasshopper's display settings

"Draw Full Names" is not a render-time flag. `GH_Canvas.InstantiateNewObject` applies it
**once, at creation**: it walks the new object's attribute tree and copies each `Name`
over its `NickName`. Creating the object ourselves bypassed that, so placed components
showed `C` and `N` while the rest of the canvas showed `Curve` and `Count`.
`Placement.ApplyFullNames` now performs the same step.

### Band headings

Only one heading is pinned at a time — the band the top of the list is currently inside.
As the next heading arrives it shoulders the pinned one upward rather than sliding
underneath, so the two never overlap and headings never stack. The arithmetic is in
`StickyHeader`, free of Windows.Forms and unit-tested at the handover point.

### What a row does and does not say

A row carries the component name and the full port name, and nothing else. Two things
were tried and removed: the port's data type as a tag on the right, and the component's
ribbon category in grey. The band headings — `direct`, `converts` — already answer the
only question the type tag was being asked, which is whether the wire will connect
cleanly, and the category row answers the other. Repeating either on all fourteen
visible rows cost more attention than it returned.

### Sizing

Every dimension comes from `GH_FontServer` — Grasshopper's own UI font, which follows
the font set in its preferences — rather than being tuned for one display. The
arithmetic lives in `LayoutMetrics` as a pure function of the body and small line
heights, so the scaling is unit-tested: rows grow with the font, never fall below a
legible floor, always clear their own text, and the list stays a whole number of rows
so a page jump lands cleanly.

### Ranking

Grasshopper's casting is permissive enough that "compatible only" barely filters —
dragging a Number leaves well over a thousand reachable ports. Ranking, not
filtering, is what makes the list usable:

1. **Band** — `direct` (same type or a lossless widening such as Circle→Curve),
   then `converts`, then `generic`. Banding on exact type alone was wrong: it buried
   *Divide Curve* under a dozen Circle params.
2. **What you picked last time** for this same drag type, remembered in Grasshopper's settings.
3. **Not obscure** — Grasshopper flags a large share of components as hidden from the ribbon.
4. **Frequency**, then exposure, then name.

A component contributes one row per compatible port — *Construct Point* offers X, Y
and Z — but only ports in its own band, so a Number drag no longer suggests
*Blend Colours ▸ Colour A*.

## Tests

```bash
cd tests/WireDrop.Tests && dotnet test
```

`TypeCompat` and `Fuzzy` are deliberately free of Grasshopper types and are compiled
into the test project from source, so the suite runs without a Rhino install to load
assemblies from. The canvas interaction itself has to be checked by hand.

## Layout

```
src/WireDrop/
  WireDropPriority.cs       load hook; attaches to each canvas, starts the catalog
  CanvasWatcher.cs          detects a wire released over empty canvas
  Placement.cs              creates the component, aligns the grip, wires, records undo
  Interop/                  bound private fields of GH_WireInteraction
  Catalog/                  component + port map, frequency prior, recent picks
  Ranking/                  cast scores, fuzzy match, banding and grouping
  UI/                       the panel and its palette
```

## License

MIT — see [LICENSE](LICENSE).
