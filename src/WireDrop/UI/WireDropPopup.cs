using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using WireDrop.Catalog;
using WireDrop.Ranking;

namespace WireDrop.UI
{
    /// <summary>
    /// The panel that opens where the wire was dropped. Keyboard first: type to narrow,
    /// up/down to move, left/right to walk the category row, Tab to widen past the
    /// compatible set, Enter to place and wire, Escape to forget it happened.
    /// </summary>
    internal sealed class WireDropPopup : Form
    {
        readonly GH_Canvas _canvas;
        readonly IGH_Param _source;
        readonly bool _fromInput;
        readonly string _dragType;
        readonly PointF _dropCanvas;
        readonly Palette _p = Palette.Current();
        readonly Metrics _m = new Metrics();
        /// <summary>Read once when the panel opens: the wire cannot change while it is up.</summary>
        readonly ValueCast _values;

        readonly TextBox _search;
        readonly BodyPanel _body;
        readonly int _searchH;

        HitList _hits;
        int[] _offsets = Array.Empty<int>();
        int _selected = -1;
        int _scroll;
        int _hover = -1;
        /// <summary>
        /// Which of the cursor and the selection the description strip is following. The
        /// last one to move wins: arrows hand it to the selection even while the cursor
        /// rests on another row, and the cursor takes it back the moment it moves again.
        /// </summary>
        bool _hoverLeads;
        bool _showAll;
        string _category;
        KeyEventArgs _lastHandledKey;
        bool _closing;
        bool _dismissable;
        Rectangle[] _chipBounds = Array.Empty<Rectangle>();
        string[] _chipNames = Array.Empty<string>();
        /// <summary>Categories the search text currently reaches nothing in.</summary>
        bool[] _chipFaded = Array.Empty<bool>();
        int _catH;

        /// <summary>
        /// Whether the category row wears icons rather than names. Not a setting of ours:
        /// it follows the one Grasshopper draws its own ribbon tabs by, so the panel reads
        /// the way the ribbon above it already does.
        /// </summary>
        readonly bool _categoryIcons;
        readonly Dictionary<string, Bitmap> _categoryIcon =
            new Dictionary<string, Bitmap>(StringComparer.Ordinal);

        WireDropPopup(GH_Canvas canvas, IGH_Param source, bool fromInput, PointF dropCanvas)
        {
            _canvas = canvas;
            _source = source;
            _fromInput = fromInput;
            _dropCanvas = dropCanvas;
            _dragType = TypeCompat.ShortName(SafeType(source));
            _values = ValueCast.For(source, fromInput);
            _categoryIcons = Safe(() => CentralSettings.RibbonDrawTabIcons, false);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MinimizeBox = MaximizeBox = false;
            KeyPreview = true;
            BackColor = _p.Back;
            Font = _m.Body;

            _search = new SearchBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Font = _m.Body,
                BackColor = _p.Field,
                ForeColor = _p.Text,
                Location = new Point(_m.Pad, _m.Pad - 1),
                Width = _m.Width - _m.Pad * 2,
            };
            _search.TextChanged += (s, e) =>
            {
                _category = null; _selected = -1; _scroll = 0; _hoverLeads = false; Rebuild();
            };
            _search.KeyDown += OnPanelKeyDown;
            KeyDown += OnPanelKeyDown;
            Controls.Add(_search);

            // The text box sizes its own height from the font; the rest follows it.
            _searchH = _search.Height + _m.Pad * 2 - 1;

            _body = new BodyPanel(this)
            {
                TabStop = false,
                Location = new Point(0, _searchH),
                Size = new Size(_m.Width, _m.ChipH + _m.ListH + _m.HelpH + _m.FootH),
            };
            Controls.Add(_body);

            Rebuild();
        }

        static Type SafeType(IGH_Param p) { try { return p?.Type; } catch { return null; } }

        static T Safe<T>(Func<T> get, T fallback) { try { return get(); } catch { return fallback; } }

        /// <summary>
        /// The icon Grasshopper's ribbon uses for a category, or null when the row is in
        /// names mode, when the category is the All chip, or when whoever registered the
        /// category never gave one — a third-party tab without an icon keeps its name
        /// rather than becoming a blank chip.
        /// </summary>
        Bitmap CategoryIcon(string category)
        {
            if (!_categoryIcons || string.IsNullOrEmpty(category)) return null;
            if (_categoryIcon.TryGetValue(category, out var known)) return known;

            var icon = Safe(() => Instances.ComponentServer?.GetCategoryIcon(category), null);
            _categoryIcon[category] = icon;
            return icon;
        }

        public static void ShowFor(GH_Canvas canvas, IGH_Param source, bool fromInput,
                                   PointF dropCanvas, Point dropControl)
        {
            // Normally the catalog finished building on idle long ago. If a drop beats it
            // there — Rhino busy since load, or a very large library — finishing it costs
            // a second or two, so say so rather than appearing to hang.
            if (!ComponentCatalog.Instance.Ready)
            {
                var previous = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try { ComponentCatalog.Instance.EnsureBuilt(); }
                finally { Cursor.Current = previous; }
            }
            var popup = new WireDropPopup(canvas, source, fromInput, dropCanvas);
            popup.PlaceNear(canvas, dropControl);
            popup.HookCanvas();
            var owner = canvas.FindForm();
            if (owner != null) popup.Show(owner); else popup.Show();
            popup._search.Focus();
        }

        void PlaceNear(GH_Canvas canvas, Point dropControl)
        {
            var origin = canvas.PointToScreen(dropControl);
            var area = Screen.FromPoint(origin).WorkingArea;
            var x = origin.X + 2;
            var y = origin.Y + 2;
            if (x + Width > area.Right - 8) x = Math.Max(area.Left + 8, origin.X - Width - 2);
            if (y + Height > area.Bottom - 8) y = Math.Max(area.Top + 8, area.Bottom - Height - 8);
            Location = new Point(x, y);
        }

        // ---------- dismissal ----------

        /// <summary>
        /// Anything that is not "choose something in this panel" cancels it. Deactivate
        /// alone is not enough: Rhino's cross-platform WinForms does not raise it
        /// dependably, and on macOS the click that refocuses the Rhino window can be
        /// swallowed. Listening to the canvas directly is the reliable half, and the two
        /// together cover clicking the canvas, another Rhino panel, or another app.
        /// </summary>
        void HookCanvas()
        {
            _canvas.MouseDown += OnCanvasInterrupt;
            _canvas.ViewportChanged += OnViewportChanged;
            FormClosed += (s, e) =>
            {
                _canvas.MouseDown -= OnCanvasInterrupt;
                _canvas.ViewportChanged -= OnViewportChanged;
            };
        }

        void OnCanvasInterrupt(object sender, MouseEventArgs e) => Dismiss();

        // Panning or zooming moves the canvas out from under the drop point, so the
        // anchor the panel was opened against no longer means anything.
        void OnViewportChanged(object sender, GH_CanvasViewportChangedEventArgs e) => Dismiss();

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Nothing may dismiss the panel until it is actually up: if a build raises
            // Deactivate while the window is still being shown, it would flash and vanish.
            _dismissable = true;
            _search.Focus();
        }

        void Dismiss()
        {
            if (!_dismissable || _closing) return;
            _closing = true;
            Close();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Dismiss();
        }

        // ---------- model ----------

        void Rebuild()
        {
            _hits = HitBuilder.Build(_dragType, _fromInput, _search.Text, _showAll, _category, _values);
            LayoutCategories();
            var offsets = new int[_hits.Rows.Length + 1];
            for (int i = 0; i < _hits.Rows.Length; i++)
                offsets[i + 1] = offsets[i] + (_hits.Rows[i] is BandHeader ? _m.HeadH : _m.RowH);
            _offsets = offsets;
            if (_selected < 0 || _selected >= _hits.Rows.Length || !(_hits.Rows[_selected] is Hit))
                _selected = FirstRow();
            EnsureVisible();
            _body?.Invalidate();
        }

        /// <summary>
        /// Every category the wire can reach, wrapping onto as many rows as it takes. The
        /// set does not depend on what has been typed — the row answers where this wire can
        /// go, and typing narrows the list under it rather than making places disappear.
        /// That also settles the height once per scope, so the list never moves under the
        /// cursor while you type. Categories the text reaches nothing in are faded, so the
        /// row stays complete without being misleading.
        /// </summary>
        void LayoutCategories()
        {
            var names = new List<string> { null };
            var faded = new List<bool> { false };
            foreach (var c in _hits.Categories)
            {
                names.Add(c.Name);
                faded.Add(c.Matches == 0);
            }

            var widths = names.Select(ChipWidth).ToArray();
            _chipBounds = ChipFlow.Arrange(widths, _m.Width, _m.ChipH, _m.ChipGap, _m.Pad, 5);
            _chipNames = names.ToArray();
            _chipFaded = faded.ToArray();

            var height = ChipFlow.Height(_chipBounds, _m.ChipH, 5);
            if (height == _catH) return;

            _catH = height;
            ResizeToFit();
        }

        const string AllLabel = "All";

        /// <summary>Square for an icon, wide enough for the name otherwise.</summary>
        int ChipWidth(string category) =>
            CategoryIcon(category) != null
                ? _m.ChipH + 4
                : Metrics.Measure(category ?? AllLabel, _m.Small) + 12;

        void ResizeToFit()
        {
            var bodyHeight = _catH + _m.ListH + _m.HelpH + _m.FootH;
            if (_body.Height == bodyHeight) return;
            _body.Height = bodyHeight;
            ClientSize = new Size(_m.Width, _searchH + bodyHeight);
            if (_dismissable) KeepOnScreen();
        }

        void KeepOnScreen()
        {
            var area = Screen.FromPoint(Location).WorkingArea;
            var x = Math.Min(Location.X, area.Right - Width - 8);
            var y = Math.Min(Location.Y, area.Bottom - Height - 8);
            Location = new Point(Math.Max(area.Left + 8, x), Math.Max(area.Top + 8, y));
        }

        int FirstRow()
        {
            for (int i = 0; i < _hits.Rows.Length; i++) if (_hits.Rows[i] is Hit) return i;
            return -1;
        }

        void MoveSelection(int delta)
        {
            if (_hits.Rows.Length == 0) return;
            var i = _selected;
            var step = Math.Sign(delta);
            var remaining = Math.Abs(delta);
            while (remaining > 0)
            {
                var next = i + step;
                while (next >= 0 && next < _hits.Rows.Length && !(_hits.Rows[next] is Hit)) next += step;
                if (next < 0 || next >= _hits.Rows.Length) break;
                i = next;
                remaining--;
            }
            _selected = i;
            EnsureVisible();
            _body.Invalidate();
        }

        void EnsureVisible()
        {
            if (_selected < 0 || _offsets.Length == 0) return;
            var top = _offsets[_selected];
            var bottom = top + _m.RowH;
            if (top < _scroll) _scroll = top;
            else if (bottom > _scroll + _m.ListH) _scroll = bottom - _m.ListH;
            ClampScroll();
        }

        void ClampScroll()
        {
            var total = _offsets.Length > 0 ? _offsets[_offsets.Length - 1] : 0;
            _scroll = Math.Max(0, Math.Min(_scroll, Math.Max(0, total - _m.ListH)));
        }

        void CycleCategory(int direction)
        {
            var names = new List<string> { null };
            names.AddRange(_hits.Categories.Select(c => c.Name));
            var at = names.FindIndex(n => string.Equals(n, _category, StringComparison.Ordinal));
            if (at < 0) at = 0;
            at = Math.Max(0, Math.Min(names.Count - 1, at + direction));
            _category = names[at];
            _selected = -1; _scroll = 0;
            Rebuild();
        }

        void Accept()
        {
            if (_selected < 0 || !(_hits.Rows[_selected] is Hit hit)) return;
            _closing = true;
            Close();
            if (hit.Create != null)
                Placement.InsertImplied(_canvas, _source, _fromInput, _dropCanvas,
                                        hit.Create, hit.Connects);
            else
                Placement.Insert(_canvas, _source, _fromInput, _dropCanvas,
                                 hit.Component, hit.Port, _dragType);
        }

        // ---------- keyboard ----------

        /// <summary>
        /// Handled in KeyDown rather than ProcessCmdKey: the latter is Windows message-loop
        /// plumbing that Rhino's cross-platform WinForms does not raise, so on macOS the
        /// arrows and Enter never reached the panel. Attached to both the form and the
        /// search field because which one a given build raises is not knowable up front;
        /// the guard stops the second acting on an event the first already handled.
        /// </summary>
        void OnPanelKeyDown(object sender, KeyEventArgs e)
        {
            if (ReferenceEquals(_lastHandledKey, e)) return;

            var action = KeyMap.Resolve((int)e.KeyCode);
            if (action == PanelAction.None) return;

            _lastHandledKey = e;
            e.Handled = true;
            e.SuppressKeyPress = true;
            _hoverLeads = false;

            switch (action)
            {
                case PanelAction.MoveUp: MoveSelection(-1); break;
                case PanelAction.MoveDown: MoveSelection(1); break;
                case PanelAction.PageUp: MoveSelection(-(_m.ListH / _m.RowH)); break;
                case PanelAction.PageDown: MoveSelection(_m.ListH / _m.RowH); break;
                case PanelAction.PreviousCategory: CycleCategory(-1); break;
                case PanelAction.NextCategory: CycleCategory(1); break;
                case PanelAction.Accept: Accept(); break;
                case PanelAction.Cancel: _closing = true; Close(); break;
                case PanelAction.ToggleScope:
                    _showAll = !_showAll;
                    _category = null;
                    _selected = -1;
                    _scroll = 0;
                    Rebuild();
                    break;
            }
        }

        /// <summary>A text field that hands the panel's navigation keys to KeyDown.</summary>
        sealed class SearchBox : TextBox
        {
            protected override bool IsInputKey(Keys keyData) =>
                KeyMap.IsPanelKey((int)(keyData & Keys.KeyCode)) || base.IsInputKey(keyData);
        }

        // ---------- painting ----------

        sealed class BodyPanel : Panel
        {
            readonly WireDropPopup _o;

            public BodyPanel(WireDropPopup owner)
            {
                _o = owner;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            const int TrackW = 12;

            bool _draggingThumb;
            int _grabOffset;
            bool _thumbHot;

            Metrics M => _o._m;
            Rectangle ListRect => new Rectangle(0, _o._catH, Width, M.ListH);
            Rectangle HelpRect => new Rectangle(0, _o._catH + M.ListH, Width, M.HelpH);

            int TotalHeight => _o._offsets.Length > 0 ? _o._offsets[_o._offsets.Length - 1] : 0;
            bool HasScrollbar => ScrollBar.Needed(TotalHeight, M.ListH);

            Rectangle TrackRect
            {
                get { var l = ListRect; return new Rectangle(l.Right - TrackW, l.Top, TrackW, l.Height); }
            }

            Rectangle ThumbRect
            {
                get
                {
                    var track = TrackRect;
                    var h = ScrollBar.ThumbHeight(TotalHeight, M.ListH, track.Height);
                    var top = ScrollBar.ThumbTop(TotalHeight, M.ListH, track.Height, _o._scroll);
                    var w = _draggingThumb || _thumbHot ? 7 : 4;
                    return new Rectangle(track.Right - w - 3, track.Top + top, w, h);
                }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                _o._scroll -= Math.Sign(e.Delta) * M.RowH * 3;
                _o.ClampScroll();
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_draggingThumb)
                {
                    var track = TrackRect;
                    var top = e.Y - track.Top - _grabOffset;
                    _o._scroll = ScrollBar.ScrollFromThumbTop(TotalHeight, M.ListH, track.Height, top);
                    _o._hover = -1;
                    Invalidate();
                    return;
                }

                var overThumb = HasScrollbar && TrackRect.Contains(e.Location);
                if (overThumb != _thumbHot) { _thumbHot = overThumb; Invalidate(); }

                var index = overThumb ? -1 : RowAt(e.Location);
                // Moving over a row takes the description strip back from the arrow keys,
                // even when it is the same row the cursor was already resting on.
                if (index >= 0 && !_o._hoverLeads) { _o._hoverLeads = true; Invalidate(); }
                if (index != _o._hover) { _o._hover = index; Invalidate(); }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (_draggingThumb)
                {
                    _draggingThumb = false;
                    Capture = false;
                    _o._search.Focus();
                    Invalidate();
                }
                base.OnMouseUp(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (_draggingThumb) return;
                if (_o._hover != -1 || _thumbHot) { _o._hover = -1; _thumbHot = false; Invalidate(); }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                // The scrollbar sits inside the list rectangle, so it has to be tested
                // first or a grab would be read as picking whatever row is underneath.
                if (HasScrollbar && TrackRect.Contains(e.Location))
                {
                    var thumb = ThumbRect;
                    if (thumb.Contains(e.Location))
                    {
                        _grabOffset = e.Y - thumb.Top;
                    }
                    else
                    {
                        // Clicking the track jumps so the thumb centres on the cursor.
                        var track = TrackRect;
                        _grabOffset = ScrollBar.ThumbHeight(TotalHeight, M.ListH, track.Height) / 2;
                        _o._scroll = ScrollBar.ScrollFromThumbTop(
                            TotalHeight, M.ListH, track.Height, e.Y - track.Top - _grabOffset);
                    }
                    _draggingThumb = true;
                    _o._hover = -1;
                    Capture = true;
                    Invalidate();
                    return;
                }

                for (int i = 0; i < _o._chipBounds.Length && i < _o._chipNames.Length; i++)
                {
                    if (!_o._chipBounds[i].Contains(e.Location)) continue;
                    _o._category = _o._chipNames[i];
                    _o._selected = -1; _o._scroll = 0;
                    _o.Rebuild();
                    _o._search.Focus();
                    return;
                }

                var index = RowAt(e.Location);
                if (index >= 0) { _o._selected = index; _o.Accept(); return; }

                // Keep the caret where the keys are handled.
                _o._search.Focus();
            }

            int RowAt(Point pt)
            {
                var list = ListRect;
                if (!list.Contains(pt)) return -1;
                if (HasScrollbar && TrackRect.Contains(pt)) return -1;
                var y = pt.Y - list.Top + _o._scroll;
                var rows = _o._hits.Rows;
                for (int i = 0; i < rows.Length; i++)
                    if (y >= _o._offsets[i] && y < _o._offsets[i + 1])
                        return rows[i] is Hit ? i : -1;
                return -1;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(_o._p.Back);
                PaintCategories(g, _o._p);
                PaintList(g, _o._p);
                PaintHelp(g, _o._p);
                PaintFooter(g, _o._p);
            }

            void PaintCategories(Graphics g, Palette p)
            {
                var r = new Rectangle(0, 0, Width, _o._catH);
                using (var b = new SolidBrush(p.Back)) g.FillRectangle(b, r);
                using (var pen = new Pen(p.Line)) g.DrawLine(pen, 0, r.Bottom - 1, Width, r.Bottom - 1);

                var bounds = _o._chipBounds;
                var names = _o._chipNames;
                var faded = _o._chipFaded;
                for (int i = 0; i < bounds.Length && i < names.Length; i++)
                {
                    var selected = string.Equals(_o._category, names[i], StringComparison.Ordinal);
                    DrawChip(g, p, bounds[i], names[i], selected, i < faded.Length && faded[i]);
                }
            }

            void DrawChip(Graphics g, Palette p, Rectangle rect, string category, bool selected,
                          bool faded)
            {
                var back = selected ? p.ChipSel : p.Chip;
                if (faded) back = Color.FromArgb(105, back);
                using (var b = new SolidBrush(back)) FillRounded(g, b, rect, 3);

                var icon = _o.CategoryIcon(category);
                if (icon != null)
                {
                    var side = Math.Min(rect.Height - 4, rect.Width - 4);
                    var dest = new Rectangle(rect.X + (rect.Width - side) / 2,
                                             rect.Y + (rect.Height - side) / 2, side, side);
                    try
                    {
                        if (!faded) g.DrawImage(icon, dest);
                        else
                        {
                            using var attributes = new ImageAttributes();
                            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.35f });
                            g.DrawImage(icon, dest, 0, 0, icon.Width, icon.Height,
                                        GraphicsUnit.Pixel, attributes);
                        }
                    }
                    catch { }
                    return;
                }

                var text = category ?? AllLabel;
                var ink = selected ? p.ChipSelText : p.ChipText;
                if (faded) ink = Color.FromArgb(105, ink);
                using var br = new SolidBrush(ink);
                var fmt = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(text, M.Small, br,
                    new RectangleF(rect.X + 6, rect.Y, rect.Width - 10, rect.Height), fmt);
            }

            static void FillRounded(Graphics g, Brush brush, Rectangle r, int radius)
            {
                using var path = new GraphicsPath();
                path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
                path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
                path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                var mode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
                g.SmoothingMode = mode;
            }

            void PaintList(Graphics g, Palette p)
            {
                var list = ListRect;
                using (var b = new SolidBrush(p.Back)) g.FillRectangle(b, list);

                var rows = _o._hits.Rows;
                if (rows.Length == 0)
                {
                    using var dim0 = new SolidBrush(p.Dim);
                    var msg = "Nothing takes a " + _o._dragType + " here.\nPress Tab to search the whole library.";
                    var centre = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(msg, M.Body, dim0, list, centre);
                    return;
                }

                var sticky = StickyHeader.Resolve(
                    i => rows[i] is BandHeader, _o._offsets, rows.Length, _o._scroll, M.HeadH);

                var clip = g.Clip;
                g.SetClip(list);
                using var inkBrush = new SolidBrush(p.Text);
                using var dimBrush = new SolidBrush(p.Dim);
                using var portBrush = new SolidBrush(p.PortText);
                using var selBrush = new SolidBrush(p.SelText);

                var textTop = (M.RowH - M.Body.Height) / 2f;

                for (int i = 0; i < rows.Length; i++)
                {
                    var top = list.Top + _o._offsets[i] - _o._scroll;
                    var h = rows[i] is BandHeader ? M.HeadH : M.RowH;
                    if (top + h < list.Top) continue;
                    if (top > list.Bottom) break;

                    if (rows[i] is BandHeader header)
                    {
                        // The pinned copy is drawn last, at the top; skip the in-flow one.
                        if (sticky.Visible && sticky.Index == i) continue;
                        PaintBandHeader(g, p, header, list, top, dimBrush);
                        continue;
                    }

                    var hit = (Hit)rows[i];
                    var selected = i == _o._selected;
                    if (selected)
                    {
                        using var sb = new SolidBrush(p.Sel);
                        g.FillRectangle(sb, list.Left, top, list.Width, h);
                    }
                    else if (i == _o._hover)
                    {
                        using var hb = new SolidBrush(Color.FromArgb(28, p.Text));
                        g.FillRectangle(hb, list.Left, top, list.Width, h);
                    }

                    if (hit.FirstOfGroup && hit.Component.Icon != null)
                    {
                        try
                        {
                            g.DrawImage(hit.Component.Icon,
                                new Rectangle(M.Pad, top + (M.RowH - M.IconSize) / 2, M.IconSize, M.IconSize));
                        }
                        catch { }
                    }

                    var textBrush = selected ? selBrush : inkBrush;
                    var x = (float)(M.Pad + M.IconSize + 6);
                    var right = list.Right - M.Pad;

                    if (hit.FirstOfGroup)
                    {
                        g.DrawString(hit.Component.Name, M.Name, textBrush, x, top + textTop);
                        x += g.MeasureString(hit.Component.Name, M.Name).Width;
                    }
                    else
                    {
                        // A continuation row: another port on the component named above.
                        g.DrawString("↳", M.Body, dimBrush, x, top + textTop);
                        x += g.MeasureString("↳", M.Body).Width + 2;
                    }

                    g.DrawString(" ▸ ", M.Body, dimBrush, x, top + textTop);
                    x += g.MeasureString(" ▸ ", M.Body).Width;

                    // The port's full name, never the nickname.
                    var portName = hit.Port.Name;
                    var room = right - x;
                    if (room > 12)
                    {
                        var trim = new StringFormat
                        {
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap,
                        };
                        g.DrawString(portName, M.Name, selected ? textBrush : portBrush,
                                     new RectangleF(x, top + textTop, room, h), trim);
                    }
                }

                if (sticky.Visible && rows[sticky.Index] is BandHeader pinned)
                    PaintBandHeader(g, p, pinned, list, list.Top + sticky.Offset, dimBrush);

                g.Clip = clip;

                if (HasScrollbar)
                {
                    var thumb = ThumbRect;
                    if (_thumbHot || _draggingThumb)
                    {
                        var track = TrackRect;
                        using var trb = new SolidBrush(Color.FromArgb(22, p.Text));
                        FillRounded(g, trb, new Rectangle(thumb.X, track.Top, thumb.Width, track.Height),
                                    thumb.Width / 2);
                    }
                    var opacity = _draggingThumb ? 165 : _thumbHot ? 120 : 70;
                    using var tb = new SolidBrush(Color.FromArgb(opacity, p.Text));
                    FillRounded(g, tb, thumb, thumb.Width / 2);
                }
            }

            void PaintBandHeader(Graphics g, Palette p, BandHeader header, Rectangle list,
                                 int top, Brush dim)
            {
                using var back = new SolidBrush(p.Strip);
                g.FillRectangle(back, list.Left, top, list.Width, M.HeadH);
                using var pen = new Pen(p.Line);
                g.DrawLine(pen, list.Left, top, list.Right, top);
                g.DrawLine(pen, list.Left, top + M.HeadH - 1, list.Right, top + M.HeadH - 1);
                g.DrawString(header.Label.ToUpperInvariant(), M.Small, dim,
                             M.Pad, top + (M.HeadH - M.Small.Height) / 2f);
            }

            /// <summary>
            /// Grasshopper's own description of whatever the eye is on: the row under the
            /// cursor while there is one, and the selected row otherwise — so leaving the
            /// list puts back the description of the thing Enter would place. Nothing is
            /// fetched here; the text was read off the object proxies when the catalog was
            /// built, so it costs a lookup, not a load.
            /// </summary>
            void PaintHelp(Graphics g, Palette p)
            {
                var r = HelpRect;
                using (var b = new SolidBrush(p.Strip)) g.FillRectangle(b, r);
                using (var pen = new Pen(p.Line)) g.DrawLine(pen, r.Left, r.Top, r.Right, r.Top);

                var rows = _o._hits.Rows;
                var index = _o._hoverLeads && _o._hover >= 0 ? _o._hover : _o._selected;
                if (index < 0 || index >= rows.Length || !(rows[index] is Hit hit)) return;

                var x = M.Pad;
                var width = r.Width - M.Pad * 2;
                // Set in the row font, not the small one: this is text to be read.
                var line = M.Body.Height;
                var top = r.Top + M.Pad / 2f;

                var description = hit.Component?.Description;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    using var ink = new SolidBrush(p.Text);
                    var wrap = new StringFormat { Trimming = StringTrimming.EllipsisWord };
                    g.DrawString(description.Trim(), M.Body, ink,
                                 new RectangleF(x, top, width, line * 2), wrap);
                }

                var port = PortLine(hit);
                if (port == null) return;

                using var dim = new SolidBrush(p.Dim);
                var clip = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                };
                g.DrawString(port, M.Body, dim,
                             new RectangleF(x, top + line * 2, width, line), clip);
            }

            /// <summary>The port's own name and description, when it has anything to say.</summary>
            static string PortLine(Hit hit)
            {
                var name = hit.Port?.Name;
                if (string.IsNullOrWhiteSpace(name)) return null;
                var description = hit.Port?.Description;
                return string.IsNullOrWhiteSpace(description)
                    ? name
                    : name + " — " + description.Trim();
            }

            void PaintFooter(Graphics g, Palette p)
            {
                var r = new Rectangle(0, Height - M.FootH, Width, M.FootH);
                using (var b = new SolidBrush(p.Footer)) g.FillRectangle(b, r);
                using (var pen = new Pen(p.Line)) g.DrawLine(pen, 0, r.Top, Width, r.Top);

                using var dim = new SolidBrush(p.Dim);
                var y = r.Top + (M.FootH - M.Small.Height) / 2f;

                var left = _o._hits.PortCount + (_o._hits.PortCount == 1 ? " port · " : " ports · ")
                           + _o._hits.ComponentCount + " components";
                g.DrawString(left, M.Small, dim, M.Pad, y);

                var right = _o._showAll ? "Tab compatible only   ←→ category" : "Tab show all   ←→ category";
                var w = g.MeasureString(right, M.Small).Width;
                g.DrawString(right, M.Small, dim, Width - M.Pad - w, y);
            }
        }
    }
}
