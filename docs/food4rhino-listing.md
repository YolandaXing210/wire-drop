# Food4Rhino listing — WireDrop

For the "Add new app" form at https://www.food4rhino.com (sign in → Account → Add app).

## Basics

| Field | Value |
|---|---|
| App name | WireDrop |
| Type | Free app |
| Category | Grasshopper — Utility / Productivity |
| Platform | Rhino 8 for Windows, Rhino 8 for Mac |
| Requires | Grasshopper (Rhino 8, running on .NET Core — Rhino 8's default) |
| Version | 1.0.0 |
| Download | Upload `dist/WireDrop-1.0.0.zip` (built by `./package.sh`) |
| License | Proprietary — paste the full text of LICENSE |

## Short description

Drop a wire on empty canvas and pick what it connects to — ranked by what the wire is
actually carrying.

## Full description

Pull a wire off any Grasshopper port and let go over empty canvas. A panel opens right
there, listing every component that port can connect to — the ones that fit best first.
Type to narrow it, arrow keys to move, Enter to place the component already wired up.

- **Ranked by what the wire carries,** not just what the port declares: a generic wire
  holding curves is offered curve components.
- **Works in both directions.** Drag from an output for components that consume it, from an
  input for ones that produce it.
- **Open a wire in the middle.** Right-click a wire (Ctrl-click on macOS) for components
  that fit *between* its two ends; pick one and it is spliced in.
- **Every plugin you have installed,** not only Grasshopper's own tabs — including ones
  that load after Grasshopper starts.
- **Shortcuts** the way Grasshopper's canvas search reads them: type `5` for a slider set to
  5, `0<5<10` for a ranged slider, `3,4` for a point, `"note` for a panel.
- **One undo** takes back both the component and its wire.
- A **WireDrop menu** in Grasshopper's menu bar switches each part on or off.

Free to use for any purpose, including commercial work. Please don't modify or
redistribute it — share this page instead.

## Install (paste into the listing's install notes)

1. Download and unzip.
2. In Grasshopper: **File > Special Folders > Components Folder**, and drop
   `WireDrop.gha` into it.
3. **Windows:** right-click the `.gha` > Properties > tick **Unblock** > OK.
   **macOS**, if it is blocked: `xattr -d com.apple.quarantine "<path to WireDrop.gha>"`
4. Restart Rhino.

## Screenshots / media to prepare

1. The panel open on a dropped wire, with the category row and description strip visible.
2. Splicing: the panel opened on a right-clicked wire, rows showing `Input → Output`.
3. Drop-target outlines while a wire is being dragged.
4. The WireDrop menu.
5. A short GIF of drop → type → Enter.

## After it is live

- Put the listing's URL at the top of `README.md` — it points at Food4Rhino's home page
  until then.
