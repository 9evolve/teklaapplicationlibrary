// SteelPMToolbox.cs
// Unified Tekla Structures 2024 macro — tabbed PM utility
// Drop into your macros folder, run via Tools > Macros > SteelPMToolbox
//
// ─── BEFORE FIRST USE ────────────────────────────────────────────────────
// Edit the constants in the Config region below. Most UDA names are GUESSES
// — please confirm against your environment's objects.inp file.
//
// Tabs:
//   1. Move / Rotate  — clash-fixing nudge pad with reset-to-original
//   2. Status         — bump fab/field stage with auto date stamp
//   3. CSV Import     — round-trip loader paired with FW_Report_Export
//   4. Validate       — UDA cross-check with selection-filter scope

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using T3D = Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace Tekla.Technology.Akit.UserScript
{
    // ════════════════════════════════════════════════════════════════════
    //   ENTRY POINT
    // ════════════════════════════════════════════════════════════════════
    public class Script
    {
        // Static field holds the form alive after Run() returns.
        // Re-running the macro brings the existing window forward instead
        // of opening a duplicate.
        private static MainForm _form;

        public static void Run(IScript akit)
        {
            try
            {
                Model model = new Model();
                if (!model.GetConnectionStatus())
                {
                    MessageBox.Show("Not connected to a Tekla model.",
                        "Steel PM Toolbox",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_form == null || _form.IsDisposed)
                {
                    _form = new MainForm(model, akit);
                    _form.FormClosed += delegate { _form = null; };
                    _form.Show();
                }
                else
                {
                    _form.BringToFront();
                    _form.Activate();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Steel PM Toolbox: " + ex.ToString());
                MessageBox.Show("Error: " + ex.Message, "Steel PM Toolbox",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   CONFIGURATION  —  EDIT TO MATCH YOUR ENVIRONMENT
    // ════════════════════════════════════════════════════════════════════
    public static class Cfg
    {
        // ── UDA names ────────────────────────────────────────────────────
        // GUESS — verify against objects.inp and your prototype's UDA names.
        public const string UDA_FAB_STATUS    = "UDA_FAB_STATUS";
        public const string UDA_FIELD_STATUS  = "UDA_FIELD_STATUS";
        public const string UDA_INSPECTOR     = "UDA_INSPECTOR";
        public const string UDA_FAB_LOT       = "UDA_FAB_LOT";
        public const string UDA_FW_DWG        = "UDA_FW_DWG";
        public const string UDA_FIELD_NOTES   = "UDA_FIELD_NOTES";

        // Date UDAs paired with each stage transition
        public const string UDA_FAB_PAINTED_DATE  = "FAB_PAINTED_DATE";
        public const string UDA_FAB_SHIPPED_DATE  = "FAB_SHIPPED_DATE";
        public const string UDA_FAB_ON_SITE_DATE  = "FAB_ON_SITE_DATE";
        public const string UDA_FIELD_SET_DATE    = "FIELD_SET_DATE";
        public const string UDA_FIELD_BOLT_DATE   = "FIELD_BOLTED_DATE";
        public const string UDA_FIELD_WELD_DATE   = "FIELD_WELDED_DATE";
        public const string UDA_FIELD_INSP_DATE   = "FIELD_INSPECTED_DATE";

        // Stage definitions (matches the prototype dashboard)
        public static readonly string[] FabStages = new string[]
        {
            "Detailing", "Shop", "Painted", "Shipped", "On site"
        };
        public static readonly string[] FieldStages = new string[]
        {
            "Not started", "Set", "Bolted", "Welded", "Inspected"
        };

        public static string FabStageDateUda(int idx)
        {
            if (idx == 2) return UDA_FAB_PAINTED_DATE;
            if (idx == 3) return UDA_FAB_SHIPPED_DATE;
            if (idx == 4) return UDA_FAB_ON_SITE_DATE;
            return null;
        }
        public static string FieldStageDateUda(int idx)
        {
            if (idx == 1) return UDA_FIELD_SET_DATE;
            if (idx == 2) return UDA_FIELD_BOLT_DATE;
            if (idx == 3) return UDA_FIELD_WELD_DATE;
            if (idx == 4) return UDA_FIELD_INSP_DATE;
            return null;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   HELPERS
    // ════════════════════════════════════════════════════════════════════
    public static class Helpers
    {
        public static List<ModelObject> GetSelectedObjects()
        {
            var result = new List<ModelObject>();
            try
            {
                var sel = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var en = sel.GetSelectedObjects();
                while (en.MoveNext())
                    if (en.Current != null) result.Add(en.Current);
            }
            catch (Exception ex) { Trace.WriteLine("GetSelected: " + ex.Message); }
            return result;
        }

        public static List<ModelObject> GetAllAssemblies(Model model)
        {
            var result = new List<ModelObject>();
            try
            {
                var en = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(new[] { typeof(Assembly) });
                while (en.MoveNext())
                    if (en.Current != null) result.Add(en.Current);
            }
            catch (Exception ex) { Trace.WriteLine("GetAllAssy: " + ex.Message); }
            return result;
        }

        public static List<ModelObject> GetAllParts(Model model)
        {
            var result = new List<ModelObject>();
            try
            {
                var en = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(new[] { typeof(Part) });
                while (en.MoveNext())
                    if (en.Current != null) result.Add(en.Current);
            }
            catch (Exception ex) { Trace.WriteLine("GetAllParts: " + ex.Message); }
            return result;
        }

        public static string GetReport(ModelObject obj, string prop)
        {
            string val = string.Empty;
            try { obj.GetReportProperty(prop, ref val); } catch { }
            return val == null ? string.Empty : val.Trim();
        }

        public static string GetUda(ModelObject obj, string uda)
        {
            string val = string.Empty;
            try { obj.GetUserProperty(uda, ref val); } catch { }
            return val == null ? string.Empty : val.Trim();
        }

        public static string GetAssemblyMark(ModelObject obj)
        {
            string mark = GetReport(obj, "ASSEMBLY_POS");
            if (string.IsNullOrEmpty(mark))
            {
                string p = GetReport(obj, "ASSEMBLY_PREFIX");
                string n = GetReport(obj, "ASSEMBLY_NUMBER");
                mark = (p + n).Trim();
            }
            return mark;
        }

        public static void SelectInModel(IEnumerable<ModelObject> objs)
        {
            try
            {
                var arr = new ArrayList();
                foreach (var o in objs) arr.Add(o);
                var sel = new Tekla.Structures.Model.UI.ModelObjectSelector();
                sel.Select(arr);
            }
            catch (Exception ex) { Trace.WriteLine("Select: " + ex.Message); }
        }

        public static string Today()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   MAIN FORM
    // ════════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        private readonly Model _model;
        private readonly IScript _akit;
        private TabControl _tabs;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _selLabel;
        private ToolStripStatusLabel _modeLabel;

        public MainForm(Model model, IScript akit)
        {
            _model = model;
            _akit = akit;
            Text = "Steel PM Toolbox";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 720);
            MinimumSize = new Size(540, 600);
            BuildUI();
        }

        private void BuildUI()
        {
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;

            var tp1 = new TabPage("Move / Rotate");
            var mover = new MoverTab(_model, _akit); mover.Dock = DockStyle.Fill;
            tp1.Controls.Add(mover);

            var tp2 = new TabPage("Status");
            var status = new StatusAdvancerTab(_model); status.Dock = DockStyle.Fill;
            tp2.Controls.Add(status);

            var tp3 = new TabPage("CSV Import");
            var csv = new CsvImportTab(_model); csv.Dock = DockStyle.Fill;
            tp3.Controls.Add(csv);

            var tp4 = new TabPage("Validate");
            var val = new ValidatorTab(_model); val.Dock = DockStyle.Fill;
            tp4.Controls.Add(val);

            _tabs.TabPages.AddRange(new TabPage[] { tp1, tp2, tp3, tp4 });
            _tabs.SelectedIndexChanged += new EventHandler(OnTab);

            _statusStrip = new StatusStrip();
            _selLabel = new ToolStripStatusLabel("Selection: 0");
            _selLabel.Spring = true;
            _selLabel.TextAlign = ContentAlignment.MiddleLeft;
            _modeLabel = new ToolStripStatusLabel("MOVE mode");
            _statusStrip.Items.Add(_selLabel);
            _statusStrip.Items.Add(_modeLabel);

            Controls.Add(_tabs);
            Controls.Add(_statusStrip);
        }

        private void OnTab(object sender, EventArgs e)
        {
            string[] modes = new[] { "MOVE mode", "STATUS mode", "CSV mode", "VALIDATE mode" };
            if (_tabs.SelectedIndex >= 0 && _tabs.SelectedIndex < modes.Length)
                _modeLabel.Text = modes[_tabs.SelectedIndex];
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 1 — MOVER
    // ════════════════════════════════════════════════════════════════════
    public class MoverTab : UserControl
    {
        private readonly Model _model;
        private readonly IScript _akit;
        private TextBox _inc;
        private CheckBox _copyMode;
        private Label _readout;
        private Label _selInfo;
        private TextBox _angleBox;
        private ComboBox _axisCombo;

        // Cumulative offset since last bind/reset (mm)
        private double _dx = 0, _dy = 0, _dz = 0;
        private List<ModelObject> _bound = new List<ModelObject>();

        private T3D.Point _pivotPoint = null;
        private Label _pivotLabel;

        public MoverTab(Model model, IScript akit)
        {
            _model = model;
            _akit = akit;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            // Selection info
            _selInfo = new Label();
            _selInfo.Text = "▸ No selection bound. Click Bind to track current selection.";
            _selInfo.Location = new Point(8, y);
            _selInfo.Size = new Size(520, 18);
            _selInfo.BackColor = Color.FromArgb(255, 248, 225);
            _selInfo.BorderStyle = BorderStyle.FixedSingle;
            _selInfo.TextAlign = ContentAlignment.MiddleLeft;
            _selInfo.Padding = new Padding(4, 0, 0, 0);
            Controls.Add(_selInfo);
            y += 22;

            var bindBtn = new Button();
            bindBtn.Text = "Bind selection";
            bindBtn.Location = new Point(8, y);
            bindBtn.Size = new Size(110, 24);
            bindBtn.Click += new EventHandler(OnBind);
            Controls.Add(bindBtn);
            y += 30;

            // Increment
            var incLabel = new Label();
            incLabel.Text = "Increment (mm):";
            incLabel.Location = new Point(8, y + 4);
            incLabel.Size = new Size(95, 18);
            Controls.Add(incLabel);

            _inc = new TextBox();
            _inc.Text = "152.4"; // 6"
            _inc.Location = new Point(108, y + 2);
            _inc.Size = new Size(70, 20);
            Controls.Add(_inc);

            int px = 185;
            string[] presets = new[] { "3.175", "25.4", "152.4", "304.8" };
            string[] presetLabels = new[] { "1/8\"", "1\"", "6\"", "1'-0\"" };
            for (int i = 0; i < presets.Length; i++)
            {
                var b = new Button();
                b.Text = presetLabels[i];
                b.Tag = presets[i];
                b.Location = new Point(px, y);
                b.Size = new Size(50, 22);
                b.Click += new EventHandler(OnPreset);
                Controls.Add(b);
                px += 52;
            }
            y += 32;

            // Translate group
            var tg = new GroupBox();
            tg.Text = "Translate";
            tg.Location = new Point(8, y);
            tg.Size = new Size(260, 168);

            // 3x3 nudge pad
            int padX = 50, padY = 18, sz = 42, gap = 4;
            AddNudge(tg, padX + (sz + gap), padY,             "↑", "Y", +1);
            AddNudge(tg, padX,                padY + (sz+gap),"←", "X", -1);
            AddNudge(tg, padX + (sz+gap)*2,   padY + (sz+gap),"→", "X", +1);
            AddNudge(tg, padX + (sz+gap),     padY + (sz+gap)*2, "↓", "Y", -1);

            var zPlus = new Button();
            zPlus.Text = "Z+ ▲";
            zPlus.Location = new Point(padX + sz*3 + 14, padY);
            zPlus.Size = new Size(60, 28);
            zPlus.Click += delegate { Nudge("Z", +1); };
            tg.Controls.Add(zPlus);

            var zMinus = new Button();
            zMinus.Text = "Z− ▼";
            zMinus.Location = new Point(padX + sz*3 + 14, padY + 32);
            zMinus.Size = new Size(60, 28);
            zMinus.Click += delegate { Nudge("Z", -1); };
            tg.Controls.Add(zMinus);

            _copyMode = new CheckBox();
            _copyMode.Text = "Copy instead of move";
            _copyMode.Location = new Point(8, 138);
            _copyMode.Size = new Size(200, 18);
            tg.Controls.Add(_copyMode);

            Controls.Add(tg);

            // Rotate group
            var rg = new GroupBox();
            rg.Text = "Rotate";
            rg.Location = new Point(276, y);
            rg.Size = new Size(252, 192);

            var pickBtn = new Button();
            pickBtn.Text = "Pick pivot point…";
            pickBtn.Location = new Point(8, 22);
            pickBtn.Size = new Size(232, 24);
            pickBtn.Click += new EventHandler(OnPickPivot);
            rg.Controls.Add(pickBtn);

            var axLabel = new Label();
            axLabel.Text = "Axis:";
            axLabel.Location = new Point(8, 56);
            axLabel.Size = new Size(40, 18);
            rg.Controls.Add(axLabel);

            _axisCombo = new ComboBox();
            _axisCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _axisCombo.Items.AddRange(new object[] { "Z (vertical)", "X", "Y" });
            _axisCombo.SelectedIndex = 0;
            _axisCombo.Location = new Point(50, 54);
            _axisCombo.Size = new Size(120, 22);
            rg.Controls.Add(_axisCombo);

            var angLabel = new Label();
            angLabel.Text = "Angle:";
            angLabel.Location = new Point(8, 86);
            angLabel.Size = new Size(40, 18);
            rg.Controls.Add(angLabel);

            _angleBox = new TextBox();
            _angleBox.Text = "90";
            _angleBox.Location = new Point(50, 84);
            _angleBox.Size = new Size(60, 20);
            rg.Controls.Add(_angleBox);

            int qx = 116;
            foreach (string a in new[] { "90", "45", "15" })
            {
                var qb = new Button();
                qb.Text = a + "°";
                qb.Tag = a;
                qb.Location = new Point(qx, 83);
                qb.Size = new Size(38, 22);
                qb.Click += new EventHandler(OnAngleQuick);
                rg.Controls.Add(qb);
                qx += 40;
            }

            _pivotLabel = new Label();
            _pivotLabel.Text = "Pivot: (not picked)";
            _pivotLabel.Location = new Point(8, 116);
            _pivotLabel.Size = new Size(232, 18);
            _pivotLabel.ForeColor = Color.Gray;
            rg.Controls.Add(_pivotLabel);

            var rotBtn = new Button();
            rotBtn.Text = "Apply rotation";
            rotBtn.Location = new Point(8, 138);
            rotBtn.Size = new Size(232, 30);
            rotBtn.BackColor = Color.FromArgb(33, 150, 243);
            rotBtn.ForeColor = Color.White;
            rotBtn.Font = new Font(Font, FontStyle.Bold);
            rotBtn.Click += new EventHandler(OnApplyRotation);
            rg.Controls.Add(rotBtn);

            Controls.Add(rg);
            y += 202;

            // Action toolbar
            var freeBtn = new Button();
            freeBtn.Text = "Free move…";
            freeBtn.Location = new Point(8, y);
            freeBtn.Size = new Size(120, 26);
            freeBtn.Click += delegate { _akit.Callback("acmdMove", "", "main_frame"); };
            Controls.Add(freeBtn);

            var undoBtn = new Button();
            undoBtn.Text = "↶ Undo last";
            undoBtn.Location = new Point(132, y);
            undoBtn.Size = new Size(120, 26);
            undoBtn.Click += delegate { _akit.Callback("acmdEditUndo", "", "main_frame"); };
            Controls.Add(undoBtn);

            var resetBtn = new Button();
            resetBtn.Text = "⟲ Reset to original";
            resetBtn.Location = new Point(256, y);
            resetBtn.Size = new Size(150, 26);
            resetBtn.ForeColor = Color.FromArgb(160, 32, 32);
            resetBtn.Click += new EventHandler(OnReset);
            Controls.Add(resetBtn);
            y += 34;

            // Coord readout
            _readout = new Label();
            _readout.Location = new Point(8, y);
            _readout.Size = new Size(520, 60);
            _readout.BackColor = Color.FromArgb(26, 26, 26);
            _readout.ForeColor = Color.FromArgb(93, 222, 136);
            _readout.Font = new Font("Consolas", 9);
            _readout.TextAlign = ContentAlignment.MiddleLeft;
            _readout.Padding = new Padding(6, 4, 4, 4);
            UpdateReadout();
            Controls.Add(_readout);
        }

        private void AddNudge(Control parent, int x, int y, string text, string axis, int dir)
        {
            var b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(42, 42);
            b.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            b.Click += delegate { Nudge(axis, dir); };
            parent.Controls.Add(b);
        }

        private void OnPreset(object sender, EventArgs e)
        {
            var b = (Button)sender;
            _inc.Text = b.Tag.ToString();
        }

        private void OnAngleQuick(object sender, EventArgs e)
        {
            var b = (Button)sender;
            _angleBox.Text = b.Tag.ToString();
        }

        private void OnBind(object sender, EventArgs e)
        {
            _bound = Helpers.GetSelectedObjects();
            _dx = _dy = _dz = 0;
            if (_bound.Count == 0)
            {
                _selInfo.Text = "▸ Nothing selected. Pick parts first, then click Bind.";
                return;
            }
            string mark = _bound.Count == 1
                ? Helpers.GetAssemblyMark(_bound[0])
                : (_bound.Count + " objects");
            _selInfo.Text = "▸ Bound: " + mark + "  (delta tracking reset)";
            UpdateReadout();
        }

        private void Nudge(string axis, int dir)
        {
            if (_bound.Count == 0)
            {
                MessageBox.Show("No bound selection. Click 'Bind selection' first.",
                    "Mover", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            double inc;
            if (!double.TryParse(_inc.Text, out inc) || inc <= 0)
            {
                MessageBox.Show("Increment must be a positive number (mm).", "Mover");
                return;
            }
            double mx = 0, my = 0, mz = 0;
            if (axis == "X") mx = inc * dir;
            if (axis == "Y") my = inc * dir;
            if (axis == "Z") mz = inc * dir;

            try
            {
                // GUESS — Operation.MoveObject(ModelObject, Vector) is the
                // documented signature but worth verifying.
                var v = new T3D.Vector(mx, my, mz);
                int moved = 0;
                foreach (var obj in _bound)
                {
                    if (Operation.MoveObject(obj, v)) moved++;
                }
                _model.CommitChanges();
                _dx += mx; _dy += my; _dz += mz;
                UpdateReadout();
                Operation.DisplayPrompt("Nudged " + moved + " object(s) by " +
                    axis + (dir > 0 ? "+" : "-") + inc.ToString("0.##") + " mm");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Move failed: " + ex.Message, "Mover");
            }
        }

        private void OnPickPivot(object sender, EventArgs e)
        {
            // PickPoint() blocks the calling thread; run it on a background thread
            // and marshal the result back to the UI thread via Invoke.
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                try
                {
                    Tekla.Structures.Model.UI.Picker picker =
                        new Tekla.Structures.Model.UI.Picker();
                    T3D.Point pt = picker.PickPoint("Pick rotation pivot");
                    this.Invoke(new System.Windows.Forms.MethodInvoker(delegate()
                    {
                        _pivotPoint = pt;
                        _pivotLabel.Text = "Pivot: X=" + pt.X.ToString("0.##") +
                            "  Y=" + pt.Y.ToString("0.##") +
                            "  Z=" + pt.Z.ToString("0.##");
                        _pivotLabel.ForeColor = Color.FromArgb(0, 128, 0);
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new System.Windows.Forms.MethodInvoker(delegate()
                    {
                        _pivotLabel.Text = "Pivot: pick cancelled";
                        _pivotLabel.ForeColor = Color.Gray;
                        Trace.WriteLine("PickPivot: " + ex.Message);
                    }));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OnApplyRotation(object sender, EventArgs e)
        {
            if (_bound.Count == 0)
            {
                MessageBox.Show("No bound selection. Click 'Bind selection' first.", "Rotate");
                return;
            }
            if (_pivotPoint == null)
            {
                MessageBox.Show("No pivot point picked. Click 'Pick pivot point...' first.",
                    "Rotate");
                return;
            }
            double angle;
            if (!double.TryParse(_angleBox.Text, out angle) || angle == 0)
            {
                MessageBox.Show("Enter a non-zero rotation angle.", "Rotate");
                return;
            }

            double rad = angle * Math.PI / 180.0;
            string axisName = _axisCombo.SelectedItem != null
                ? _axisCombo.SelectedItem.ToString() : "Z (vertical)";

            // Build startCS and endCS at the pivot point using perpendicular reference
            // vectors in the plane of rotation. MoveObject(obj, fromCS, toCS) transforms
            // each object from the "from" coordinate frame to the "to" coordinate frame —
            // the same overload pattern as Operation.CopyObject (confirmed in sample macros).
            //
            // Axis X  →  plane is Y-Z  →  startX=(0,1,0), startY=(0,0,1)
            // Axis Y  →  plane is X-Z  →  startX=(1,0,0), startY=(0,0,1)
            // Axis Z  →  plane is X-Y  →  startX=(1,0,0), startY=(0,1,0)
            T3D.Vector startX, startY, endX, endY;
            double c = Math.Cos(rad), s = Math.Sin(rad);

            if (axisName.StartsWith("X"))
            {
                startX = new T3D.Vector(0, 1, 0);
                startY = new T3D.Vector(0, 0, 1);
                endX   = new T3D.Vector(0,  c, s);
                endY   = new T3D.Vector(0, -s, c);
            }
            else if (axisName.StartsWith("Y"))
            {
                startX = new T3D.Vector(1, 0, 0);
                startY = new T3D.Vector(0, 0, 1);
                endX   = new T3D.Vector( c, 0, -s);
                endY   = new T3D.Vector( s, 0,  c);
            }
            else // Z (vertical) — default
            {
                startX = new T3D.Vector(1, 0, 0);
                startY = new T3D.Vector(0, 1, 0);
                endX   = new T3D.Vector( c, s, 0);
                endY   = new T3D.Vector(-s, c, 0);
            }

            T3D.CoordinateSystem startCS = new T3D.CoordinateSystem(_pivotPoint, startX, startY);
            T3D.CoordinateSystem endCS   = new T3D.CoordinateSystem(_pivotPoint, endX,   endY);

            try
            {
                int moved = 0;
                foreach (var obj in _bound)
                {
                    if (Operation.MoveObject(obj, startCS, endCS)) moved++;
                }
                _model.CommitChanges();
                Operation.DisplayPrompt("Rotated " + moved + " object(s) " +
                    angle.ToString("0.##") + "° around " + axisName + ".");
            }
            catch (Exception ex)
            {
                Trace.WriteLine("MoveObject(CS,CS) failed: " + ex.Message +
                    " — falling back to acmdRotateObjects");
                _akit.Callback("acmdRotateObjects", "", "main_frame");
            }
        }

        private void OnReset(object sender, EventArgs e)
        {
            if (_bound.Count == 0 || (_dx == 0 && _dy == 0 && _dz == 0))
            {
                Operation.DisplayPrompt("Nothing to reset.");
                return;
            }
            try
            {
                var back = new T3D.Vector(-_dx, -_dy, -_dz);
                foreach (var obj in _bound) Operation.MoveObject(obj, back);
                _model.CommitChanges();
                _dx = _dy = _dz = 0;
                UpdateReadout();
                Operation.DisplayPrompt("Reset to original position.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reset failed: " + ex.Message, "Mover");
            }
        }

        private void UpdateReadout()
        {
            string txt = "BOUND   " + _bound.Count + " object(s)\r\n" +
                         "Δ       X: " + _dx.ToString("0.##") + " mm" +
                         "   Y: " + _dy.ToString("0.##") + " mm" +
                         "   Z: " + _dz.ToString("0.##") + " mm";
            _readout.Text = txt;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 2 — STATUS ADVANCER
    // ════════════════════════════════════════════════════════════════════
    public class StatusAdvancerTab : UserControl
    {
        private readonly Model _model;
        private Label _selInfo;
        private ComboBox _fabCombo, _fieldCombo;
        private CheckBox _stampDate;
        private TextBox _inspectorBox;
        private Label _currentFab, _currentField;

        public StatusAdvancerTab(Model model)
        {
            _model = model;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            _selInfo = new Label();
            _selInfo.Text = "▸ No selection — pick assemblies in the model";
            _selInfo.Location = new Point(8, y);
            _selInfo.Size = new Size(520, 18);
            _selInfo.BackColor = Color.FromArgb(255, 248, 225);
            _selInfo.BorderStyle = BorderStyle.FixedSingle;
            _selInfo.TextAlign = ContentAlignment.MiddleLeft;
            _selInfo.Padding = new Padding(4, 0, 0, 0);
            Controls.Add(_selInfo);
            y += 22;

            var refreshBtn = new Button();
            refreshBtn.Text = "Refresh from selection";
            refreshBtn.Location = new Point(8, y);
            refreshBtn.Size = new Size(160, 24);
            refreshBtn.Click += delegate { RefreshFromSelection(); };
            Controls.Add(refreshBtn);
            y += 34;

            // Fab status group
            var fg = new GroupBox();
            fg.Text = "Fabrication status";
            fg.Location = new Point(8, y);
            fg.Size = new Size(520, 92);

            _currentFab = new Label();
            _currentFab.Text = "Current: (no selection)";
            _currentFab.Location = new Point(8, 22);
            _currentFab.Size = new Size(500, 18);
            fg.Controls.Add(_currentFab);

            var fabLabel = new Label();
            fabLabel.Text = "Set to:";
            fabLabel.Location = new Point(8, 48);
            fabLabel.Size = new Size(50, 18);
            fg.Controls.Add(fabLabel);

            _fabCombo = new ComboBox();
            _fabCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _fabCombo.Items.Add("(no change)");
            foreach (var s in Cfg.FabStages) _fabCombo.Items.Add(s);
            _fabCombo.SelectedIndex = 0;
            _fabCombo.Location = new Point(60, 46);
            _fabCombo.Size = new Size(160, 22);
            fg.Controls.Add(_fabCombo);

            _stampDate = new CheckBox();
            _stampDate.Text = "Auto-stamp matching date UDA with today (" + Helpers.Today() + ")";
            _stampDate.Location = new Point(8, 70);
            _stampDate.Size = new Size(440, 18);
            _stampDate.Checked = true;
            fg.Controls.Add(_stampDate);

            Controls.Add(fg);
            y += 102;

            // Field status group
            var ig = new GroupBox();
            ig.Text = "Field install status";
            ig.Location = new Point(8, y);
            ig.Size = new Size(520, 70);

            _currentField = new Label();
            _currentField.Text = "Current: (no selection)";
            _currentField.Location = new Point(8, 22);
            _currentField.Size = new Size(500, 18);
            ig.Controls.Add(_currentField);

            var fieldLabel = new Label();
            fieldLabel.Text = "Set to:";
            fieldLabel.Location = new Point(8, 48);
            fieldLabel.Size = new Size(50, 18);
            ig.Controls.Add(fieldLabel);

            _fieldCombo = new ComboBox();
            _fieldCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _fieldCombo.Items.Add("(no change)");
            foreach (var s in Cfg.FieldStages) _fieldCombo.Items.Add(s);
            _fieldCombo.SelectedIndex = 0;
            _fieldCombo.Location = new Point(60, 46);
            _fieldCombo.Size = new Size(160, 22);
            ig.Controls.Add(_fieldCombo);

            Controls.Add(ig);
            y += 80;

            // Inspector
            var insLabel = new Label();
            insLabel.Text = "Inspector signoff (optional):";
            insLabel.Location = new Point(8, y + 4);
            insLabel.Size = new Size(170, 18);
            Controls.Add(insLabel);

            _inspectorBox = new TextBox();
            _inspectorBox.Location = new Point(180, y + 2);
            _inspectorBox.Size = new Size(120, 20);
            Controls.Add(_inspectorBox);

            var insHint = new Label();
            insHint.Text = "→ writes " + Cfg.UDA_INSPECTOR;
            insHint.Location = new Point(308, y + 4);
            insHint.Size = new Size(220, 18);
            insHint.ForeColor = Color.Gray;
            Controls.Add(insHint);
            y += 30;

            // Apply button
            var applyBtn = new Button();
            applyBtn.Text = "Apply to selection";
            applyBtn.Location = new Point(380, y);
            applyBtn.Size = new Size(148, 30);
            applyBtn.BackColor = Color.FromArgb(33, 150, 243);
            applyBtn.ForeColor = Color.White;
            applyBtn.Font = new Font(Font, FontStyle.Bold);
            applyBtn.Click += new EventHandler(OnApply);
            Controls.Add(applyBtn);
        }

        private void RefreshFromSelection()
        {
            var sel = Helpers.GetSelectedObjects();
            if (sel.Count == 0)
            {
                _selInfo.Text = "▸ No selection — pick assemblies in the model";
                _currentFab.Text = "Current: (no selection)";
                _currentField.Text = "Current: (no selection)";
                return;
            }
            _selInfo.Text = "▸ " + sel.Count + " object(s) selected";

            var fabVals = new Dictionary<string, int>();
            var fieldVals = new Dictionary<string, int>();
            foreach (var o in sel)
            {
                string fv = Helpers.GetUda(o, Cfg.UDA_FAB_STATUS);
                string iv = Helpers.GetUda(o, Cfg.UDA_FIELD_STATUS);
                if (string.IsNullOrEmpty(fv)) fv = "(blank)";
                if (string.IsNullOrEmpty(iv)) iv = "(blank)";
                if (!fabVals.ContainsKey(fv)) fabVals[fv] = 0;
                if (!fieldVals.ContainsKey(iv)) fieldVals[iv] = 0;
                fabVals[fv]++;
                fieldVals[iv]++;
            }
            _currentFab.Text = "Current: " + Summarize(fabVals);
            _currentField.Text = "Current: " + Summarize(fieldVals);
        }

        private string Summarize(Dictionary<string, int> counts)
        {
            var parts = new List<string>();
            foreach (var kv in counts.OrderByDescending(k => k.Value))
                parts.Add(kv.Key + " (" + kv.Value + ")");
            return string.Join(", ", parts.ToArray());
        }

        private void OnApply(object sender, EventArgs e)
        {
            var sel = Helpers.GetSelectedObjects();
            if (sel.Count == 0)
            {
                MessageBox.Show("No selection.", "Status Advancer");
                return;
            }
            string newFab = _fabCombo.SelectedIndex > 0
                ? _fabCombo.SelectedItem.ToString() : null;
            string newField = _fieldCombo.SelectedIndex > 0
                ? _fieldCombo.SelectedItem.ToString() : null;
            string inspector = _inspectorBox.Text.Trim();

            if (newFab == null && newField == null && string.IsNullOrEmpty(inspector))
            {
                MessageBox.Show("Nothing to change.", "Status Advancer");
                return;
            }

            int updated = 0, failed = 0;
            string today = Helpers.Today();

            foreach (var obj in sel)
            {
                bool changed = false;
                try
                {
                    if (newFab != null)
                    {
                        obj.SetUserProperty(Cfg.UDA_FAB_STATUS, newFab);
                        if (_stampDate.Checked)
                        {
                            int idx = Array.IndexOf(Cfg.FabStages, newFab);
                            string dateUda = Cfg.FabStageDateUda(idx);
                            if (dateUda != null) obj.SetUserProperty(dateUda, today);
                        }
                        changed = true;
                    }
                    if (newField != null)
                    {
                        obj.SetUserProperty(Cfg.UDA_FIELD_STATUS, newField);
                        if (_stampDate.Checked)
                        {
                            int idx = Array.IndexOf(Cfg.FieldStages, newField);
                            string dateUda = Cfg.FieldStageDateUda(idx);
                            if (dateUda != null) obj.SetUserProperty(dateUda, today);
                        }
                        changed = true;
                    }
                    if (!string.IsNullOrEmpty(inspector))
                    {
                        obj.SetUserProperty(Cfg.UDA_INSPECTOR, inspector);
                        changed = true;
                    }
                    if (changed && obj.Modify()) updated++;
                    else if (changed) failed++;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Status apply: " + ex.Message);
                    failed++;
                }
            }
            _model.CommitChanges();
            MessageBox.Show("Updated: " + updated + "\nFailed: " + failed,
                "Status Advancer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFromSelection();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 3 — CSV IMPORT
    // ════════════════════════════════════════════════════════════════════
    public class CsvImportTab : UserControl
    {
        private readonly Model _model;
        private TextBox _pathBox;
        private ListView _preview;
        private CheckBox _dryRun;
        private Label _summary;
        private List<string[]> _rows = new List<string[]>();
        private string[] _headers;

        // Hardcoded column mapping for v1
        // GUESS — adjust to match your CSV format
        private const string COL_MARK    = "Mark";
        private const string COL_FIELD   = "FieldStatus";
        private const string COL_INSP    = "Inspector";
        private const string COL_DATE    = "Date";
        private const string COL_NOTES   = "Notes";

        public CsvImportTab(Model model)
        {
            _model = model;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            var pathLabel = new Label();
            pathLabel.Text = "CSV file:";
            pathLabel.Location = new Point(8, y + 4);
            pathLabel.Size = new Size(60, 18);
            Controls.Add(pathLabel);

            _pathBox = new TextBox();
            _pathBox.Location = new Point(70, y + 2);
            _pathBox.Size = new Size(380, 20);
            _pathBox.ReadOnly = true;
            Controls.Add(_pathBox);

            var browseBtn = new Button();
            browseBtn.Text = "Browse…";
            browseBtn.Location = new Point(456, y);
            browseBtn.Size = new Size(72, 24);
            browseBtn.Click += new EventHandler(OnBrowse);
            Controls.Add(browseBtn);
            y += 30;

            var hint = new Label();
            hint.Text = "Expected columns: Mark, FieldStatus, Inspector, Date, Notes  " +
                        "(edit constants in source to change)";
            hint.Location = new Point(8, y);
            hint.Size = new Size(520, 18);
            hint.ForeColor = Color.Gray;
            Controls.Add(hint);
            y += 24;

            _preview = new ListView();
            _preview.Location = new Point(8, y);
            _preview.Size = new Size(520, 280);
            _preview.View = View.Details;
            _preview.GridLines = true;
            _preview.FullRowSelect = true;
            _preview.Font = new Font("Consolas", 9);
            Controls.Add(_preview);
            y += 290;

            _summary = new Label();
            _summary.Text = "No file loaded.";
            _summary.Location = new Point(8, y);
            _summary.Size = new Size(520, 18);
            Controls.Add(_summary);
            y += 22;

            _dryRun = new CheckBox();
            _dryRun.Text = "Dry run (preview only — don't write to model)";
            _dryRun.Location = new Point(8, y);
            _dryRun.Size = new Size(320, 18);
            _dryRun.Checked = true;
            Controls.Add(_dryRun);

            var importBtn = new Button();
            importBtn.Text = "Import";
            importBtn.Location = new Point(380, y - 4);
            importBtn.Size = new Size(148, 30);
            importBtn.BackColor = Color.FromArgb(33, 150, 243);
            importBtn.ForeColor = Color.White;
            importBtn.Font = new Font(Font, FontStyle.Bold);
            importBtn.Click += new EventHandler(OnImport);
            Controls.Add(importBtn);
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _pathBox.Text = dlg.FileName;
            LoadCsv(dlg.FileName);
        }

        private void LoadCsv(string path)
        {
            _rows.Clear();
            _preview.Items.Clear();
            _preview.Columns.Clear();
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length == 0) { _summary.Text = "File is empty."; return; }
                _headers = SplitCsvLine(lines[0]);
                foreach (var h in _headers)
                    _preview.Columns.Add(h, 90);
                for (int i = 1; i < lines.Length; i++)
                {
                    var cells = SplitCsvLine(lines[i]);
                    _rows.Add(cells);
                    var item = new ListViewItem(cells.Length > 0 ? cells[0] : "");
                    for (int j = 1; j < cells.Length; j++)
                        item.SubItems.Add(cells[j]);
                    _preview.Items.Add(item);
                }
                _summary.Text = "Loaded " + _rows.Count + " rows · " +
                    _headers.Length + " columns. Click Import to apply.";
            }
            catch (Exception ex)
            {
                _summary.Text = "Error reading CSV: " + ex.Message;
            }
        }

        private string[] SplitCsvLine(string line)
        {
            // Minimal CSV splitter — does NOT handle quoted commas. GUESS:
            // user's exports are simple. Swap in a real parser if needed.
            return line.Split(',');
        }

        private int IndexOf(string col)
        {
            if (_headers == null) return -1;
            for (int i = 0; i < _headers.Length; i++)
                if (string.Equals(_headers[i].Trim(), col, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private void OnImport(object sender, EventArgs e)
        {
            if (_rows.Count == 0)
            {
                MessageBox.Show("Load a CSV file first.", "Import");
                return;
            }
            int markCol  = IndexOf(COL_MARK);
            int fieldCol = IndexOf(COL_FIELD);
            int inspCol  = IndexOf(COL_INSP);
            int dateCol  = IndexOf(COL_DATE);
            int notesCol = IndexOf(COL_NOTES);

            if (markCol < 0)
            {
                MessageBox.Show("CSV is missing the '" + COL_MARK + "' column.",
                    "Import");
                return;
            }

            // Build assembly mark → object map
            var assemblies = Helpers.GetAllAssemblies(_model);
            var byMark = new Dictionary<string, ModelObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in assemblies)
            {
                string m = Helpers.GetAssemblyMark(a);
                if (!string.IsNullOrEmpty(m) && !byMark.ContainsKey(m))
                    byMark[m] = a;
            }

            int matched = 0, missing = 0, updated = 0, failed = 0;
            var missList = new List<string>();

            foreach (var row in _rows)
            {
                if (row.Length <= markCol) continue;
                string mark = row[markCol].Trim();
                if (string.IsNullOrEmpty(mark)) continue;

                ModelObject obj;
                if (!byMark.TryGetValue(mark, out obj))
                {
                    missing++;
                    missList.Add(mark);
                    continue;
                }
                matched++;
                if (_dryRun.Checked) continue;

                try
                {
                    if (fieldCol >= 0 && row.Length > fieldCol)
                        obj.SetUserProperty(Cfg.UDA_FIELD_STATUS, row[fieldCol].Trim());
                    if (inspCol >= 0 && row.Length > inspCol)
                        obj.SetUserProperty(Cfg.UDA_INSPECTOR, row[inspCol].Trim());
                    if (notesCol >= 0 && row.Length > notesCol)
                        obj.SetUserProperty(Cfg.UDA_FIELD_NOTES, row[notesCol].Trim());
                    // Date column — write to whichever date UDA matches the
                    // new field status (best-effort).
                    if (dateCol >= 0 && row.Length > dateCol && fieldCol >= 0)
                    {
                        int idx = Array.IndexOf(Cfg.FieldStages, row[fieldCol].Trim());
                        string dateUda = Cfg.FieldStageDateUda(idx);
                        if (dateUda != null)
                            obj.SetUserProperty(dateUda, row[dateCol].Trim());
                    }
                    if (obj.Modify()) updated++; else failed++;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("CSV row: " + ex.Message);
                    failed++;
                }
            }
            if (!_dryRun.Checked) _model.CommitChanges();

            var msg = new StringBuilder();
            msg.AppendLine("Matched: " + matched);
            msg.AppendLine("Missing: " + missing);
            msg.AppendLine("Updated: " + updated + (_dryRun.Checked ? "  (dry run)" : ""));
            msg.AppendLine("Failed: " + failed);
            if (missing > 0 && missing <= 20)
            {
                msg.AppendLine("\nNot in model:");
                foreach (var m in missList) msg.AppendLine("  " + m);
            }
            MessageBox.Show(msg.ToString(), "Import complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 4 — VALIDATOR
    // ════════════════════════════════════════════════════════════════════
    public class ValidatorTab : UserControl
    {
        private readonly Model _model;
        private RadioButton _scopeAll, _scopeSel, _scopeFilter;
        private ComboBox _filterCombo;
        private CheckedListBox _ruleList;
        private ListView _results;
        private Label _summary;
        private List<ValidationRule> _rules;
        private List<ModelObject> _lastFailures = new List<ModelObject>();

        public ValidatorTab(Model model)
        {
            _model = model;
            _rules = BuildRules();
            BuildUI();
        }

        private List<ValidationRule> BuildRules()
        {
            var list = new List<ValidationRule>();
            list.Add(new ValidationRule(
                "Field work drawing assigned",
                Cfg.UDA_FW_DWG + " must not be blank",
                delegate(ModelObject o)
                {
                    return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_FW_DWG));
                }));
            list.Add(new ValidationRule(
                "Shipped pieces have ship date",
                "if " + Cfg.UDA_FAB_STATUS + "=Shipped → " + Cfg.UDA_FAB_SHIPPED_DATE,
                delegate(ModelObject o)
                {
                    string s = Helpers.GetUda(o, Cfg.UDA_FAB_STATUS);
                    if (!string.Equals(s, "Shipped", StringComparison.OrdinalIgnoreCase))
                        return true; // rule doesn't apply
                    return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_FAB_SHIPPED_DATE));
                }));
            list.Add(new ValidationRule(
                "Inspected pieces have inspector",
                "if " + Cfg.UDA_FIELD_STATUS + "=Inspected → " + Cfg.UDA_INSPECTOR,
                delegate(ModelObject o)
                {
                    string s = Helpers.GetUda(o, Cfg.UDA_FIELD_STATUS);
                    if (!string.Equals(s, "Inspected", StringComparison.OrdinalIgnoreCase))
                        return true;
                    return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_INSPECTOR));
                }));
            list.Add(new ValidationRule(
                "Lot number assigned",
                Cfg.UDA_FAB_LOT + " must not be blank",
                delegate(ModelObject o)
                {
                    return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_FAB_LOT));
                }));
            return list;
        }

        private void BuildUI()
        {
            int y = 8;

            var sg = new GroupBox();
            sg.Text = "Scope";
            sg.Location = new Point(8, y);
            sg.Size = new Size(520, 96);

            _scopeAll = new RadioButton();
            _scopeAll.Text = "All assemblies in model";
            _scopeAll.Location = new Point(12, 22);
            _scopeAll.Size = new Size(200, 18);
            sg.Controls.Add(_scopeAll);

            _scopeSel = new RadioButton();
            _scopeSel.Text = "Current model selection";
            _scopeSel.Location = new Point(12, 44);
            _scopeSel.Size = new Size(200, 18);
            sg.Controls.Add(_scopeSel);

            _scopeFilter = new RadioButton();
            _scopeFilter.Text = "Selection filter:";
            _scopeFilter.Location = new Point(12, 66);
            _scopeFilter.Size = new Size(110, 18);
            _scopeFilter.Checked = true;
            sg.Controls.Add(_scopeFilter);

            _filterCombo = new ComboBox();
            _filterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _filterCombo.Location = new Point(126, 64);
            _filterCombo.Size = new Size(280, 22);
            PopulateFilters();
            sg.Controls.Add(_filterCombo);

            var refreshFilters = new Button();
            refreshFilters.Text = "↻";
            refreshFilters.Location = new Point(412, 63);
            refreshFilters.Size = new Size(28, 24);
            refreshFilters.Click += delegate { PopulateFilters(); };
            sg.Controls.Add(refreshFilters);

            Controls.Add(sg);
            y += 106;

            // Rule list
            var rg = new GroupBox();
            rg.Text = "Validation rules";
            rg.Location = new Point(8, y);
            rg.Size = new Size(520, 116);

            _ruleList = new CheckedListBox();
            _ruleList.Location = new Point(8, 20);
            _ruleList.Size = new Size(504, 86);
            _ruleList.CheckOnClick = true;
            for (int i = 0; i < _rules.Count; i++)
            {
                _ruleList.Items.Add(_rules[i].Name + "  —  " + _rules[i].Description, true);
            }
            rg.Controls.Add(_ruleList);

            Controls.Add(rg);
            y += 126;

            // Results table
            _results = new ListView();
            _results.Location = new Point(8, y);
            _results.Size = new Size(520, 220);
            _results.View = View.Details;
            _results.GridLines = true;
            _results.FullRowSelect = true;
            _results.Font = new Font("Consolas", 9);
            _results.Columns.Add("Mark", 90);
            _results.Columns.Add("Profile", 110);
            _results.Columns.Add("Phase", 50);
            _results.Columns.Add("Failed rule", 260);
            Controls.Add(_results);
            y += 230;

            _summary = new Label();
            _summary.Text = "Run rules to populate results.";
            _summary.Location = new Point(8, y);
            _summary.Size = new Size(360, 22);
            Controls.Add(_summary);

            var selBtn = new Button();
            selBtn.Text = "⊕ Select in model";
            selBtn.Location = new Point(8, y + 26);
            selBtn.Size = new Size(140, 26);
            selBtn.Click += new EventHandler(OnSelectFailures);
            Controls.Add(selBtn);

            var exportBtn = new Button();
            exportBtn.Text = "📋 Export CSV";
            exportBtn.Location = new Point(154, y + 26);
            exportBtn.Size = new Size(120, 26);
            exportBtn.Click += new EventHandler(OnExportCsv);
            Controls.Add(exportBtn);

            var runBtn = new Button();
            runBtn.Text = "Run rules";
            runBtn.Location = new Point(380, y + 26);
            runBtn.Size = new Size(148, 30);
            runBtn.BackColor = Color.FromArgb(33, 150, 243);
            runBtn.ForeColor = Color.White;
            runBtn.Font = new Font(Font, FontStyle.Bold);
            runBtn.Click += new EventHandler(OnRun);
            Controls.Add(runBtn);
        }

        private void PopulateFilters()
        {
            _filterCombo.Items.Clear();
            // GUESS — common filter folder location. Real lookup should
            // also check firm/project/system attribute folders.
            try
            {
                string modelDir = _model.GetInfo().ModelPath;
                if (!string.IsNullOrEmpty(modelDir))
                {
                    string attrDir = Path.Combine(modelDir, "attributes");
                    if (Directory.Exists(attrDir))
                    {
                        var files = Directory.GetFiles(attrDir, "*.SObjGrp");
                        foreach (var f in files)
                            _filterCombo.Items.Add(Path.GetFileNameWithoutExtension(f));
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine("PopulateFilters: " + ex.Message); }
            if (_filterCombo.Items.Count > 0) _filterCombo.SelectedIndex = 0;
        }

        private List<ModelObject> ResolveScope()
        {
            if (_scopeSel.Checked) return Helpers.GetSelectedObjects();
            if (_scopeAll.Checked) return Helpers.GetAllAssemblies(_model);
            if (_scopeFilter.Checked && _filterCombo.SelectedItem != null)
            {
                // Tekla open API does not expose a programmatic MatchesFilter method.
                // Fall back to all assemblies and notify the user.
                string filterName = _filterCombo.SelectedItem.ToString();
                _summary.Text = "Filter scope (\"" + filterName + "\") not supported via open API — using all assemblies.";
                return Helpers.GetAllAssemblies(_model);
            }
            return Helpers.GetAllAssemblies(_model);
        }

        private void OnRun(object sender, EventArgs e)
        {
            var scope = ResolveScope();
            _results.Items.Clear();
            _lastFailures.Clear();
            int totalFails = 0;
            var failsByRule = new Dictionary<string, int>();

            foreach (var obj in scope)
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    if (!_ruleList.GetItemChecked(i)) continue;
                    var rule = _rules[i];
                    bool pass = false;
                    try { pass = rule.Check(obj); }
                    catch (Exception ex) { Trace.WriteLine("Rule: " + ex.Message); }
                    if (!pass)
                    {
                        totalFails++;
                        if (!failsByRule.ContainsKey(rule.Name)) failsByRule[rule.Name] = 0;
                        failsByRule[rule.Name]++;
                        _lastFailures.Add(obj);

                        var item = new ListViewItem(Helpers.GetAssemblyMark(obj));
                        item.SubItems.Add(Helpers.GetReport(obj, "PROFILE"));
                        item.SubItems.Add(Helpers.GetReport(obj, "PHASE"));
                        item.SubItems.Add(rule.Name);
                        item.Tag = obj;
                        _results.Items.Add(item);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append(totalFails + " failure(s) across " + scope.Count + " object(s)");
            if (failsByRule.Count > 0)
            {
                sb.Append("  ·  ");
                var parts = new List<string>();
                foreach (var kv in failsByRule)
                    parts.Add(kv.Key + ": " + kv.Value);
                sb.Append(string.Join(", ", parts.ToArray()));
            }
            _summary.Text = sb.ToString();
        }

        private void OnSelectFailures(object sender, EventArgs e)
        {
            if (_lastFailures.Count == 0)
            {
                MessageBox.Show("No failures to select. Run rules first.",
                    "Validator");
                return;
            }
            Helpers.SelectInModel(_lastFailures);
            Operation.DisplayPrompt("Selected " + _lastFailures.Count + " failing object(s).");
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            if (_results.Items.Count == 0)
            {
                MessageBox.Show("Nothing to export.", "Validator"); return;
            }
            var dlg = new SaveFileDialog();
            dlg.Filter = "CSV (*.csv)|*.csv";
            dlg.FileName = "validation_failures_" +
                DateTime.Now.ToString("yyyy-MM-dd") + ".csv";
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using (var w = new StreamWriter(dlg.FileName))
                {
                    w.WriteLine("Mark,Profile,Phase,FailedRule");
                    foreach (ListViewItem item in _results.Items)
                    {
                        var cells = new List<string>();
                        cells.Add(item.Text);
                        for (int i = 1; i < item.SubItems.Count; i++)
                            cells.Add(item.SubItems[i].Text);
                        w.WriteLine(string.Join(",", cells.ToArray()));
                    }
                }
                MessageBox.Show("Exported " + _results.Items.Count + " rows.",
                    "Validator");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message, "Validator");
            }
        }
    }

    public class ValidationRule
    {
        public string Name;
        public string Description;
        public Predicate<ModelObject> Check;

        public ValidationRule(string n, string d, Predicate<ModelObject> c)
        {
            Name = n; Description = d; Check = c;
        }
    }
}
