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
| `wiredrop-0.3.1-rh8_0-any.yak` | Rhino's Package Manager |
| `WireDrop-0.3.1.zip` | Manual install — contains the `.gha` and `INSTALL.txt` |

The zip is the one to hand to someone who would rather not open a terminal: unzip, drop
the `.gha` into Grasshopper's Components folder (*File > Special Folders*), restart Rhino.

The `.yak` installs through Rhino's package manager, which is tidier to upgrade and remove
later. It is not on McNeel's server, so `--source .` points yak at the folder holding the
file instead — `wiredrop` there is the package name, not the file name:

```
cd <folder holding the .yak>
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" install --source . wiredrop
```

or on Windows, `"C:\Program Files\Rhino 8\System\yak.exe"`. Then restart Rhino;
`yak uninstall wiredrop` takes it away again.

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
| Drag a port | Outlines everything on screen that takes the wire as it is |
| Drag a port to empty canvas | Opens the panel |
| Type | Narrows the list |
| Type a shortcut | `5` slider, `"x` panel, `~x` scribble, `3,4` point, `+` Addition |
| <kbd>↑</kbd> <kbd>↓</kbd> | Move selection |
| <kbd>PgUp</kbd> <kbd>PgDn</kbd> | Jump a screen |
| <kbd>←</kbd> <kbd>→</kbd> | Walk the category row |
| <kbd>Tab</kbd> | Everything: the whole library, every port, obsolete objects included |
| <kbd>Enter</kbd> / click | Place the component and wire it |
| Move, then click | Choose where it lands — it rides the cursor until you click |
| <kbd>Esc</kbd> | Cancel |
| Hover a row | Describes that one; the arrows take the strip back to the selection |

Works in both directions: drag from an **output** and the list shows inputs that
accept it; drag from an **input** and it shows outputs that produce it. One
<kbd>Ctrl</kbd>/<kbd>Cmd</kbd>+<kbd>Z</kbd> undoes both the component and the wire.

Each row is a component and one of its ports, by full port name — *Construct Point ▸ X
coordinate*, not *▸ X*. A component with several connectable ports contributes a row
each, the first carrying the icon and name and the rest marked `↳`.

The category row wears whatever the ribbon above it wears. Grasshopper's own
`RibbonDrawTabIcons` decides whether its tabs are drawn as icons or as names, and the row
reads that same setting and uses the same icons, from
`ComponentServer.GetCategoryIcon` — so the panel never disagrees with the ribbon about
what a category looks like. **All** keeps its name in either mode, having no icon of its
own to wear, and so does any third-party tab whose author never registered one: a chip
falls back to its name rather than going blank.

The row shows every category the wire can reach, wrapping onto as many rows as it needs,
in Grasshopper's own ribbon order — Params, Maths, Sets, Vector, Curve, Surface, Mesh,
Intersect, Transform, Display, then third-party tabs alphabetically. The totals are in the
footer.

What the row shows does not depend on what has been typed. It answers *where can this wire
go*; typing narrows the list underneath it rather than making places disappear, and
choosing a category never hides where the rest went. Categories the current text reaches
nothing in are faded rather than dropped, so the row stays complete without being
misleading. Since the set only changes when <kbd>Tab</kbd> changes the scope, its height
settles once and the list below never moves under the cursor mid-word — which used to need
a second pass over the whole library on every keystroke to reserve the space.

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

### Shortcuts

Grasshopper's canvas search reads a few leading characters as *make me this object*
rather than *find me this name* — the grammar inside
`GH_PopupSearchDialog.CreateImpliedObject`. WireDrop answers to the same ones:

| Type | You get |
|---|---|
| `5`, `0.25`, `-2`, `2+3` | Number Slider set to that value |
| `0<5<10` | Number Slider with that range |
| `3,4` or `1,2,3` | Point parameter holding that point |
| `"note` or `//note` | Panel containing *note* |
| `~note` | Scribble on the canvas reading *note* |
| `+` `-` `*` `/` `\` `%` `&` `<` `>` `=` | Whichever component Grasshopper names with that symbol |
| `f(` | Expression |

None of the parsing is ours. `HarvestRange` and a numeric `ParseExpression` are the two
tests Grasshopper makes before offering a slider — it tries the first early and the second
as its last resort, and testing only the first hid bare numbers entirely, since
`HarvestRange("5")` is false. `SetInitCode` then configures the slider and `ToPoint3d`
reads the point, so what you type means what it means there, including the range
Grasshopper picks around a bare value: 5 becomes 0 to 10, -2 becomes -10 to 0. The row
shows that range, so what Enter will make is on screen before you press it.

The symbol shortcuts are not synthesised. The guid Grasshopper names is looked up in the
catalog and that component is promoted to the top of the list, so it arrives with its own
icon, its real ports and ordinary wiring — and a guid a future Grasshopper drops costs a
missing row rather than a wrong one.

A slider and a point only produce a value, so they answer only a wire pulled off an
**input**. A panel takes anything and hands back text, so it suits either direction. A
scribble has no ports at all: it is placed where the wire was dropped and nothing is
connected to it. Full names are not applied to any of them — on these objects the label
is the content, so copying the name over it would erase what was typed.

Three deliberate differences from Grasshopper:

- **A digit is required before a slider is offered.** Grasshopper's parser resolves
  constants, so `pi` and `e` are numeric. Harmless in its search box, where every hit is
  scored and sorted, but here the top row is the one Enter takes, and someone typing `pi`
  is reaching for Pipe.
- **`3, 4` counts as a point.** Grasshopper wants the digits either side of the comma
  adjacent; the coordinates parse either way and a space after a comma is natural to type.
- **Time, date and `PI` are left out.** Each sets a parsed value on one specific
  parameter, and mid-wire nobody is reaching for a date.

### Reading what is actually on the wire

The declared type is a promise about what a port carries; the value is the fact. A panel
declares text and holds `3,4,5`; a generic parameter declares nothing at all. So the first
value on the port is taken and offered to Grasshopper's own casting, `IGH_Goo.CastFrom` —
the same call a wire makes at solve time, which gets third-party types right for free and
discriminates properly: `3,4,5` casts to a point, a vector and a colour; `hello world`
casts to none of them.

Every port is asked, not only the vague ones, because the fact is always worth more than
the promise. Reading the value is free: **the first item off a tree of a million took
0.005 ms and allocated nothing** — `AllData` hands back a lazy enumerator and the first
item ends it.

`CastFrom` is where the cost is, and not where it looks. A cast that succeeds parses a
string and returns; a cast that **fails** falls through to the secondary conversion, and
Grasshopper's last guess there is that the text names an object in the Rhino document — so
`GH_Point.CastFrom("hello world")` ends up in `FindRhinoObjectByNameAndType`, searching the
open Rhino file. Failing is the common case here, since failing is how the filtering
happens, and on a file holding tens of thousands of objects that search is not free.

Measured on a 40,000 object Rhino model, the shape of the cost is stark:

| the question | cost |
|---|---|
| text → a type the table rates possible | 0.01 – 0.6 ms |
| text → a type the table rates **impossible** | **16 – 28 ms** |
| any non-text value → anything | 0.02 ms |

The expensive column is the object-name search, and it lands entirely on the ports the
table already scores at zero. So the rule that avoids the cost is also the honest one:
**the value is asked to settle doubt, not to overturn certainty or to invent a route the
table has never heard of.** Concretely — a port whose declared type already matches is
never asked, nor is one the table already rates direct; a port it rates *possible* is
asked, since that is what doubt means; a port it rates impossible is asked only when the
value is not text, where the question costs 20 microseconds instead of 20 milliseconds.

On the drag that motivated all this — off a panel reading `3,4,5`, over that same
40,000 object model — asking every type cost **27.8 ms** and asking only the doubtful ones
costs **0.5 ms**.

Two further guards: answers are cached per goo type, since a drag spans thousands of ports
but only a hundred or so distinct types; and a drag may spend 25 ms casting and no more,
after which the remaining ports are ranked on their declared types alone. Running out
costs precision, not correctness — that ranking is where the plugin started.

The answer decides the port both ways. A cast that works promotes it into the top band —
whose heading then says which of the two answers put a row there. A cast that fails
removes it, because connecting a panel reading `hello world` to a point input is not a
weaker option, it is an error waiting to be made. <kbd>Tab</kbd> still widens the panel to
the whole library, so nothing is permanently out of reach.

A port that **takes anything** is left exactly as the type table had it, neither promoted
nor dropped. Nothing is converted on the way into one, so nothing can fail there — and its
declared type is `IGH_Goo`, which cannot be built to ask the question anyway. The same
"no answer" applies to a third-party goo whose `CastFrom` throws: a failure to ask is not
an answer of no, so the port keeps whatever the table gave it.

Three limits worth knowing, since they decide where this helps:

- **Both directions, read from different ends.** Dragging an output, the value is ours and
  the question is which ports take it. Dragging an input, the value belongs to each
  candidate — whatever its output is already carrying — and the question is which of those
  come into ours: a panel reading `3,4,5` answers a Vector input, one reading
  `hello world` does not. That second direction only reaches the canvas outlines, since
  the panel's candidates come from the installed library and a component that is not on
  the canvas is holding nothing to read.
- **Only the first item.** A tree on that port may hold a hundred thousand.
- **Only when there is something to read.** When the solution has not run, the port is
  empty, or a solution is in flight, the type table answers alone. Casting is also
  permissive — `3,4,5` casts to a number and a boolean as well as a point — so this
  sharpens the list; it does not reduce it to one answer.

Set `WireDrop.ReadValues` to false to fall back to the declared types.

### Where the wire could go

While the wire is still on the cursor, every object on screen that could take it is
outlined, so the answer is visible before the button is released rather than only
afterwards in the panel. The colour says how well it fits:

One colour, one meaning: **green is where the value goes in as it is** — the same type, a
lossless cast like Circle→Curve, or a port that takes anything at all and converts nothing.
Everything else is left dark, including ports that would connect through a conversion.
Those are the ones that accept the wire and then quietly do something other than what was
meant, so they are not worth pointing at.

The outline says *this object*; a dot says *this port*. Every port that takes the value
gets one, at the exact point Grasshopper would land the wire — its own `InputGrip` or
`OutputGrip` — so a component with three number inputs shows three dots and which one to
aim for is never a guess. Dropping on empty canvas still opens the panel; the dots are for
when you can see the answer and would rather just finish the wire.

The set is worked out once when the drag starts, since a document does not change
mid-drag, and only tested for visibility per frame, since the viewport does. Nothing in
the document is touched: the outlines are drawn straight onto the canvas in
`CanvasPostPaintObjects`, so a drag that goes nowhere leaves no trace.

Whatever state that event hands over the graphics in, `Viewport.ApplyProjection` puts it
into canvas coordinates — it assigns the transform rather than compounding it, and it is
how Grasshopper's own window-select interaction draws. The pen width is divided by the
zoom so the outline stays the same weight on screen however far out the canvas is.

### Choosing where it lands

A placed object stays on the cursor until you click, so the wire picks its own spot rather
than landing wherever you happened to let go — a node in Blender's shader editor behaves
the same way. <kbd>Esc</kbd> puts it back at the point the wire was dropped, which is
where it would have gone before any of this; it is never left half-placed.

What follows the cursor is the real object, already in the document and already wired, not
a drawn ghost. That is what makes it cheap and what makes it safe: moving an object only
expires its layout, so nothing is recomputed between the drop and the click, and the wire
follows on its own because Grasshopper draws wires from the grips on every frame. There is
nothing to commit and nothing to clean up — the click just stops the following, and one
<kbd>Ctrl</kbd>/<kbd>Cmd</kbd>+<kbd>Z</kbd> still takes back the object and the wire
together, exactly as when placement was immediate.

Grasshopper has no interaction class for this. Its public ones cover dragging, wiring,
panning, zooming and rubber-band selection, but nothing that carries a new object to a
click, so this is the plugin's own — a canvas `MouseMove` that re-hangs the object on the
cursor by the grip being wired, and a `MouseDown` that lets go. Grasshopper's own handlers
run first and read that click as a click on the object, which selects it: the right thing
to be left holding.

Set `WireDrop.FollowCursor` to false in Grasshopper's settings to go back to placing at
the drop point.

### The description strip

Under the list is Grasshopper's own description of whatever the eye is on. Two lines for
the component, one for the port, set in the same font as the rows above — it is text meant
to be read, not a caption. Both are taken off the object proxies when the catalog was
built, so showing them costs a lookup rather than a load.

Whichever of the cursor and the selection moved last decides what it describes. Hovering a
row describes that row; pressing an arrow key hands the strip to the selection even though
the cursor is still resting on some other row, since the selection is now the thing
<kbd>Enter</kbd> would place; moving the mouse over a row takes it back. Leaving the list
altogether falls back to the selection for the same reason. Letting hover win outright
was wrong in exactly one case, and it was the common one: reading down the list with the
arrows while the mouse sits where it was left.

The strip is always there, even with nothing to say. Sizing it to its content would move
the list under the cursor every time the mouse crossed a row, which is the same reason the
category row keeps a fixed height while you type.

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

### What Tab means

Everything, and it had to be made to mean that. Two filters used to survive it. A
component only ever offered the ports in its own best band, so a Number drag on *Circle
CNR* listed Radius and never Center or Normal even when asked for everything — reasonable
as a courtesy in the compatible list, wrong as a rule. And the catalog dropped obsolete and
hidden objects when it was built, which put them out of reach of any search at all: on this
machine that is **497 of 2132 installed objects, 23% of the library**, gone.

Both are lifted now. Obsolete and hidden objects are kept, marked, held out of the
compatible list and out of search, and sorted below everything else when Tab does ask for
them. What Tab still will not show you is an object Grasshopper cannot instantiate — those
never make it into the catalog, because the only way to read a component's ports is to
build one.

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
  Ranking/                  cast scores, fuzzy match, banding, grouping, shortcuts
  UI/                       the panel and its palette
```

## License

MIT — see [LICENSE](LICENSE).
