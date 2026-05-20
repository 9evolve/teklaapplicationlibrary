// ErectabilityChecker.cs
// Steel Erectability Toolbox — standalone Tekla Structures 2024+ macro
// Tabs: Move/Rotate  |  Bolt Clearance  |  Erection Sequence
//
// Drop into: Tekla\<version>\environments\common\macros\modeling\
// Run via:   Tools → Macros → Modeling → ErectabilityChecker

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
        private static MainForm _form;

        public static void Run(IScript akit)
        {
            try
            {
                var model = new Model();
                if (!model.GetConnectionStatus())
                {
                    MessageBox.Show("Not connected to a Tekla Structures model.",
                        "Steel Erectability Toolbox",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_form == null || _form.IsDisposed)
                {
                    _form = new MainForm(model, akit);
                    _form.FormClosed += delegate { _form = null; };
                    _form.ShowDialog();
                }
                else
                {
                    _form.BringToFront();
                    _form.Activate();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("ErectabilityChecker: " + ex);
                MessageBox.Show("Error: " + ex.Message, "Steel Erectability Toolbox",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   HELPERS
    // ════════════════════════════════════════════════════════════════════
    internal static class Helpers
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

        public static string GetReport(ModelObject obj, string prop)
        {
            string val = string.Empty;
            try { obj.GetReportProperty(prop, ref val); } catch { }
            return val == null ? string.Empty : val.Trim();
        }

        public static string GetAssemblyMark(ModelObject obj)
        {
            string mark = GetReport(obj, "ASSEMBLY_POS");
            if (string.IsNullOrEmpty(mark))
                mark = (GetReport(obj, "ASSEMBLY_PREFIX") + GetReport(obj, "ASSEMBLY_NUMBER")).Trim();
            return string.IsNullOrEmpty(mark) ? "?" : mark;
        }

        public static void SelectInModel(IEnumerable<ModelObject> objs)
        {
            try
            {
                var arr = new ArrayList();
                foreach (var o in objs) arr.Add(o);
                new Tekla.Structures.Model.UI.ModelObjectSelector().Select(arr);
            }
            catch (Exception ex) { Trace.WriteLine("Select: " + ex.Message); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   MAIN FORM
    // ════════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        public MainForm(Model model, IScript akit)
        {
            Text = "Steel Erectability Toolbox";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(620, 760);
            MinimumSize = new Size(580, 640);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            var tp1 = new TabPage("Move / Rotate");
            var mover = new MoverTab(model, akit) { Dock = DockStyle.Fill };
            tp1.Controls.Add(mover);

            var tp2 = new TabPage("Bolt Clearance");
            var bolts = new BoltClearanceTab(model) { Dock = DockStyle.Fill };
            tp2.Controls.Add(bolts);

            var tp3 = new TabPage("Erection Sequence ⋯");
            var seq = new ErectionSequenceTab { Dock = DockStyle.Fill };
            tp3.Controls.Add(seq);

            tabs.TabPages.AddRange(new TabPage[] { tp1, tp2, tp3 });

            var status = new StatusStrip();
            var lbl = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            try { lbl.Text = "Model: " + model.GetInfo().ModelName; }
            catch { lbl.Text = "Model: connected"; }
            status.Items.Add(lbl);

            Controls.Add(tabs);
            Controls.Add(status);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 1 — MOVE / ROTATE
    // ════════════════════════════════════════════════════════════════════
    public class MoverTab : UserControl
    {
        private readonly Model _model;
        private readonly IScript _akit;
        private Label _selInfo, _readout, _pivotLabel;
        private TextBox _inc, _angleBox;
        private ComboBox _axisCombo;
        private T3D.Point _pivotPoint;
        private List<ModelObject> _bound = new List<ModelObject>();
        private double _dx, _dy, _dz;

        public MoverTab(Model model, IScript akit)
        {
            _model = model; _akit = akit;
            AutoScroll = true;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            _selInfo = AddLabel(8, y, 568, 18, "▸ No selection bound — pick parts then click Bind");
            _selInfo.BackColor = Color.FromArgb(255, 248, 225);
            _selInfo.BorderStyle = BorderStyle.FixedSingle;
            _selInfo.Padding = new Padding(4, 0, 0, 0);
            y += 26;

            AddLabel(8, y + 4, 100, 18, "Increment (mm):");
            _inc = new TextBox { Text = "5", Location = new Point(110, y + 2), Size = new Size(60, 20) };
            Controls.Add(_inc);

            int px = 180;
            foreach (var kv in new[] { "1=1 mm", "5=5", "10=10", "25=25 mm" })
            {
                var parts = kv.Split('=');
                var b = new Button { Text = parts[1], Tag = parts[0], Location = new Point(px, y), Size = new Size(52, 24) };
                b.Click += (s, e) => _inc.Text = ((Button)s).Tag.ToString();
                Controls.Add(b); px += 54;
            }
            y += 32;

            // Nudge group
            var ng = new GroupBox { Text = "Nudge", Location = new Point(8, y), Size = new Size(280, 148) };
            int nx = 56, ny = 18, sz = 40, gap = 4;
            AddNudge(ng, nx + sz + gap,           ny,                "↑", "Y", +1);
            AddNudge(ng, nx,                       ny + sz + gap,     "←", "X", -1);
            AddNudge(ng, nx + (sz + gap) * 2,      ny + sz + gap,     "→", "X", +1);
            AddNudge(ng, nx + sz + gap,            ny + (sz + gap)*2, "↓", "Y", -1);
            var zp = MakeBtn("Z+▲", new Point(nx + (sz+gap)*3, ny), new Size(56, 28)); zp.Click += delegate { Nudge("Z", +1); }; ng.Controls.Add(zp);
            var zm = MakeBtn("Z−▼", new Point(nx + (sz+gap)*3, ny + 34), new Size(56, 28)); zm.Click += delegate { Nudge("Z", -1); }; ng.Controls.Add(zm);
            Controls.Add(ng);

            // Rotate group
            var rg = new GroupBox { Text = "Rotate", Location = new Point(296, y), Size = new Size(284, 148) };
            rg.Controls.Add(new Label { Text = "Axis:", Location = new Point(8, 22), Size = new Size(36, 18) });
            _axisCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(46, 20), Size = new Size(120, 22) };
            _axisCombo.Items.AddRange(new object[] { "Z (vertical)", "X", "Y" });
            _axisCombo.SelectedIndex = 0;
            rg.Controls.Add(_axisCombo);

            rg.Controls.Add(new Label { Text = "Angle °:", Location = new Point(8, 50), Size = new Size(54, 18) });
            _angleBox = new TextBox { Text = "90", Location = new Point(64, 48), Size = new Size(50, 20) };
            rg.Controls.Add(_angleBox);

            int qx = 120;
            foreach (string a in new[] { "45", "90", "180" })
            {
                string cap = a + "°";
                var qb = new Button { Text = cap, Tag = a, Location = new Point(qx, 47), Size = new Size(42, 22) };
                qb.Click += (s, e) => _angleBox.Text = ((Button)s).Tag.ToString();
                rg.Controls.Add(qb); qx += 44;
            }

            var pickBtn = MakeBtn("Pick pivot point…", new Point(8, 76), new Size(268, 24));
            pickBtn.Click += new EventHandler(OnPickPivot);
            rg.Controls.Add(pickBtn);

            _pivotLabel = new Label { Text = "Pivot: not set", Location = new Point(8, 106), Size = new Size(268, 16), ForeColor = Color.Gray };
            rg.Controls.Add(_pivotLabel);

            var rotBtn = MakeBtn("Apply rotation", new Point(8, 116), new Size(268, 26));
            rotBtn.BackColor = Color.FromArgb(33, 150, 243); rotBtn.ForeColor = Color.White;
            rotBtn.Font = new Font(Font, FontStyle.Bold);
            rotBtn.Click += new EventHandler(OnApplyRotation);
            rg.Controls.Add(rotBtn);
            Controls.Add(rg);
            y += 158;

            // Action row
            int bx = 8;
            foreach (var kv in new[] {
                new KeyValuePair<string,EventHandler>("Bind selection",  new EventHandler(OnBind)),
                new KeyValuePair<string,EventHandler>("Free",            new EventHandler(OnFree)),
                new KeyValuePair<string,EventHandler>("⟲ Reset",    new EventHandler(OnReset)),
                new KeyValuePair<string,EventHandler>("↶ Undo",     new EventHandler(OnUndo)) })
            {
                var b = MakeBtn(kv.Key, new Point(bx, y), new Size(136, 26));
                b.Click += kv.Value; Controls.Add(b); bx += 140;
            }
            y += 34;

            _readout = new Label { Location = new Point(8, y), Size = new Size(568, 48) };
            _readout.BackColor = Color.FromArgb(26, 26, 26); _readout.ForeColor = Color.FromArgb(93, 222, 136);
            _readout.Font = new Font("Consolas", 9); _readout.TextAlign = ContentAlignment.MiddleLeft;
            _readout.Padding = new Padding(6, 4, 4, 4);
            Controls.Add(_readout);
            UpdateReadout();
        }

        private void AddNudge(Control parent, int x, int y, string text, string axis, int dir)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(40, 40) };
            b.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
            b.Click += delegate { Nudge(axis, dir); };
            parent.Controls.Add(b);
        }

        private Label AddLabel(int x, int y, int w, int h, string text)
        {
            var l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(l); return l;
        }

        private static Button MakeBtn(string text, Point loc, Size size)
        {
            return new Button { Text = text, Location = loc, Size = size };
        }

        private void OnBind(object s, EventArgs e)
        {
            _bound = Helpers.GetSelectedObjects();
            _dx = _dy = _dz = 0;
            _selInfo.Text = _bound.Count == 0
                ? "▸ Nothing selected — pick parts first, then click Bind."
                : "▸ Bound: " + (_bound.Count == 1 ? Helpers.GetAssemblyMark(_bound[0]) : _bound.Count + " objects") + "  (delta reset)";
            UpdateReadout();
        }

        private void OnFree(object s, EventArgs e)
        {
            _bound.Clear(); _dx = _dy = _dz = 0;
            _selInfo.Text = "▸ Selection freed.";
            UpdateReadout();
        }

        private void OnUndo(object s, EventArgs e) { _akit.Callback("acmdEditUndo", "", "main_frame"); }

        private void Nudge(string axis, int dir)
        {
            if (_bound.Count == 0) { MessageBox.Show("Bind a selection first.", "Move"); return; }
            double inc;
            if (!double.TryParse(_inc.Text, out inc) || inc <= 0) { MessageBox.Show("Increment must be a positive number.", "Move"); return; }
            double mx = axis == "X" ? inc * dir : 0;
            double my = axis == "Y" ? inc * dir : 0;
            double mz = axis == "Z" ? inc * dir : 0;
            try
            {
                var v = new T3D.Vector(mx, my, mz);
                int moved = 0;
                foreach (var obj in _bound) if (Operation.MoveObject(obj, v)) moved++;
                _model.CommitChanges();
                _dx += mx; _dy += my; _dz += mz;
                UpdateReadout();
                Operation.DisplayPrompt("Nudged " + moved + " object(s) " + axis + (dir > 0 ? "+" : "−") + inc.ToString("0.##") + " mm");
            }
            catch (Exception ex) { MessageBox.Show("Move failed: " + ex.Message, "Move"); }
        }

        private void OnPickPivot(object s, EventArgs e)
        {
            var t = new System.Threading.Thread(delegate()
            {
                try
                {
                    var picker = new Tekla.Structures.Model.UI.Picker();
                    T3D.Point pt = picker.PickPoint("Pick rotation pivot");
                    Invoke(new MethodInvoker(delegate()
                    {
                        _pivotPoint = pt;
                        _pivotLabel.Text = string.Format("Pivot: X={0:0.##}  Y={1:0.##}  Z={2:0.##}", pt.X, pt.Y, pt.Z);
                        _pivotLabel.ForeColor = Color.FromArgb(0, 128, 0);
                    }));
                }
                catch (Exception ex)
                {
                    Invoke(new MethodInvoker(delegate()
                    {
                        _pivotLabel.Text = "Pivot: pick cancelled";
                        _pivotLabel.ForeColor = Color.Gray;
                        Trace.WriteLine("PickPivot: " + ex.Message);
                    }));
                }
            });
            t.IsBackground = true; t.Start();
        }

        private void OnApplyRotation(object s, EventArgs e)
        {
            if (_bound.Count == 0) { MessageBox.Show("Bind a selection first.", "Rotate"); return; }
            if (_pivotPoint == null) { MessageBox.Show("Pick a pivot point first.", "Rotate"); return; }
            double angle;
            if (!double.TryParse(_angleBox.Text, out angle) || angle == 0) { MessageBox.Show("Enter a non-zero angle.", "Rotate"); return; }

            double rad = angle * Math.PI / 180.0;
            double c = Math.Cos(rad), sn = Math.Sin(rad);
            string axisName = _axisCombo.SelectedItem != null ? _axisCombo.SelectedItem.ToString() : "Z (vertical)";

            T3D.Vector startX, startY, endX, endY;
            if (axisName.StartsWith("X"))
            { startX = new T3D.Vector(0, 1, 0); startY = new T3D.Vector(0, 0, 1); endX = new T3D.Vector(0, c, sn); endY = new T3D.Vector(0, -sn, c); }
            else if (axisName.StartsWith("Y"))
            { startX = new T3D.Vector(1, 0, 0); startY = new T3D.Vector(0, 0, 1); endX = new T3D.Vector(c, 0, -sn); endY = new T3D.Vector(sn, 0, c); }
            else
            { startX = new T3D.Vector(1, 0, 0); startY = new T3D.Vector(0, 1, 0); endX = new T3D.Vector(c, sn, 0); endY = new T3D.Vector(-sn, c, 0); }

            var startCS = new T3D.CoordinateSystem(_pivotPoint, startX, startY);
            var endCS   = new T3D.CoordinateSystem(_pivotPoint, endX, endY);
            try
            {
                int moved = 0;
                foreach (var obj in _bound) if (Operation.MoveObject(obj, startCS, endCS)) moved++;
                _model.CommitChanges();
                Operation.DisplayPrompt("Rotated " + moved + " object(s) " + angle.ToString("0.##") + "° around " + axisName + ".");
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Rotate (CS) failed: " + ex.Message + " — falling back to acmdRotateObjects");
                _akit.Callback("acmdRotateObjects", "", "main_frame");
            }
        }

        private void OnReset(object s, EventArgs e)
        {
            if (_bound.Count == 0 || (_dx == 0 && _dy == 0 && _dz == 0)) { Operation.DisplayPrompt("Nothing to reset."); return; }
            try
            {
                var back = new T3D.Vector(-_dx, -_dy, -_dz);
                foreach (var obj in _bound) Operation.MoveObject(obj, back);
                _model.CommitChanges(); _dx = _dy = _dz = 0;
                UpdateReadout(); Operation.DisplayPrompt("Reset to original position.");
            }
            catch (Exception ex) { MessageBox.Show("Reset failed: " + ex.Message, "Move"); }
        }

        private void UpdateReadout()
        {
            _readout.Text = "BOUND   " + _bound.Count + " object(s)\r\n" +
                            "Δ       X: " + _dx.ToString("0.##") + " mm" +
                            "   Y: " + _dy.ToString("0.##") + " mm" +
                            "   Z: " + _dz.ToString("0.##") + " mm";
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 2 — BOLT CLEARANCE
    // ════════════════════════════════════════════════════════════════════
    internal enum BoltRowStatus { Pass = 0, Warn = 1, Fail = 2 }

    internal class BoltCheckRow
    {
        public string Mark;
        public string Connection;
        public string BoltSize;
        public double WrenchClearanceMm;
        public double EdgeDistMm;
        public BoltRowStatus Status;
        public string Issue;
        public ModelObject SourceObject;
    }

    public class BoltClearanceTab : UserControl
    {
        private readonly Model _model;
        private RadioButton _scopeSel, _scopeAll;
        private NumericUpDown _wrenchBox, _edgeBox, _spacingBox;
        private CheckBox _tightenCheck;
        private Button _runBtn;
        private Label _elapsed, _summary;
        private ListView _results;
        private readonly List<BoltCheckRow> _rows = new List<BoltCheckRow>();
        private readonly List<ModelObject> _failObjects = new List<ModelObject>();

        public BoltClearanceTab(Model model) { _model = model; AutoScroll = true; BuildUI(); }

        private void BuildUI()
        {
            int y = 8;

            // Scope
            var sg = new GroupBox { Text = "Scope", Location = new Point(8, y), Size = new Size(578, 50) };
            _scopeSel = new RadioButton { Text = "Current selection",         Location = new Point(10, 20), Size = new Size(160, 20), Checked = true };
            _scopeAll = new RadioButton { Text = "All assemblies in model",   Location = new Point(180, 20), Size = new Size(200, 20) };
            sg.Controls.Add(_scopeSel); sg.Controls.Add(_scopeAll);
            Controls.Add(sg); y += 58;

            // Thresholds
            var tg = new GroupBox { Text = "Clearance thresholds", Location = new Point(8, y), Size = new Size(578, 116) };
            AddThreshRow(tg,  8, 22, "Min wrench clearance (mm):", 38, out _wrenchBox,  "— 1½\" standard; 32 mm for tight-access");
            AddThreshRow(tg,  8, 50, "Min edge distance (mm):",    32, out _edgeBox,    "— bolt c/l to nearest plate edge");
            AddThreshRow(tg,  8, 78, "Min bolt spacing (mm):",     70, out _spacingBox, "— centre-to-centre in same group");
            _tightenCheck = new CheckBox { Text = "Flag tightening clearance (top flange / slab obstruction)", Checked = true, Location = new Point(10, 98), Size = new Size(550, 18) };
            tg.Controls.Add(_tightenCheck);
            Controls.Add(tg); y += 124;

            // Run row
            _runBtn = new Button { Text = "Run checks", Location = new Point(8, y), Size = new Size(120, 28) };
            _runBtn.BackColor = Color.FromArgb(33, 150, 243); _runBtn.ForeColor = Color.White;
            _runBtn.Font = new Font(Font, FontStyle.Bold);
            _runBtn.Click += new EventHandler(OnRun);
            Controls.Add(_runBtn);
            _elapsed = new Label { Text = "", Location = new Point(136, y + 6), Size = new Size(440, 18), ForeColor = Color.Gray };
            Controls.Add(_elapsed); y += 36;

            // Results
            _results = new ListView { Location = new Point(8, y), Size = new Size(578, 260) };
            _results.View = View.Details; _results.GridLines = true; _results.FullRowSelect = true;
            _results.Font = new Font("Consolas", 8.5f);
            _results.Columns.Add("Mark",           80);
            _results.Columns.Add("Connection",    105);
            _results.Columns.Add("Bolt",           58);
            _results.Columns.Add("Clearance (mm)", 92);
            _results.Columns.Add("Edge dist (mm)", 92);
            _results.Columns.Add("Issue",          136);
            Controls.Add(_results); y += 270;

            // Summary + buttons
            _summary = new Label { Text = "Click ‘Run checks’ to begin.", Location = new Point(8, y), Size = new Size(578, 20) };
            Controls.Add(_summary); y += 26;

            var selFailBtn = new Button { Text = "Select failures in model", Location = new Point(8, y), Size = new Size(168, 26) };
            selFailBtn.Click += new EventHandler(OnSelectFailures); Controls.Add(selFailBtn);

            var exportBtn = new Button { Text = "Export CSV…", Location = new Point(182, y), Size = new Size(100, 26) };
            exportBtn.Click += new EventHandler(OnExportCsv); Controls.Add(exportBtn);
        }

        private void AddThreshRow(GroupBox parent, int x, int y, string label, decimal defVal, out NumericUpDown nud, string hint)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(x, y + 2), Size = new Size(190, 18) });
            nud = new NumericUpDown { Location = new Point(x + 196, y), Size = new Size(70, 22), Minimum = 0, Maximum = 9999, Value = defVal };
            parent.Controls.Add(nud);
            parent.Controls.Add(new Label { Text = hint, Location = new Point(x + 272, y + 2), Size = new Size(290, 18), ForeColor = Color.Gray, Font = new Font(Font.FontFamily, 8.5f) });
        }

        private List<BoltGroup> GetBoltGroupsInScope()
        {
            var all = new List<BoltGroup>();
            try
            {
                foreach (Type t in new[] { typeof(BoltArray), typeof(BoltCircle) })
                {
                    var en = _model.GetModelObjectSelector().GetAllObjectsWithType(t);
                    while (en.MoveNext())
                    {
                        var bg = en.Current as BoltGroup;
                        if (bg != null) all.Add(bg);
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine("GetBoltGroups: " + ex.Message); }

            if (_scopeAll.Checked) return all;

            // Filter to bolt groups connected to selected assemblies / parts
            var selIds = new HashSet<int>();
            foreach (var obj in Helpers.GetSelectedObjects())
            {
                selIds.Add(obj.Identifier.ID);
                if (obj is Assembly)
                {
                    var mp = ((Assembly)obj).GetMainPart();
                    if (mp != null) selIds.Add(mp.Identifier.ID);
                }
            }

            return all.Where(bg =>
                selIds.Contains(bg.Identifier.ID) ||
                (bg.PartToBoltTo != null && selIds.Contains(bg.PartToBoltTo.Identifier.ID))).ToList();
        }

        private void OnRun(object s, EventArgs e)
        {
            _rows.Clear(); _failObjects.Clear(); _results.Items.Clear();
            _elapsed.Text = "Running…"; _runBtn.Enabled = false;
            Application.DoEvents();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                double wrenchMin  = (double)_wrenchBox.Value;
                double edgeMin    = (double)_edgeBox.Value;
                double spacingMin = (double)_spacingBox.Value;
                bool flagTighten  = _tightenCheck.Checked;

                var groups = GetBoltGroupsInScope();
                foreach (var bg in groups)
                    CheckBoltGroup(bg, wrenchMin, edgeMin, spacingMin, flagTighten);

                PopulateResults();

                int fail = _rows.Count(r => r.Status == BoltRowStatus.Fail);
                int warn = _rows.Count(r => r.Status == BoltRowStatus.Warn);
                int pass = _rows.Count(r => r.Status == BoltRowStatus.Pass);
                _summary.Text = string.Format("✕ {0} failures   ⚠ {1} warnings   ✓ {2} passed   ({3} groups checked)",
                    fail, warn, pass, _rows.Count);
                _elapsed.Text = string.Format("Checked {0} bolt groups in {1:0.0} s", groups.Count, sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _elapsed.Text = "Error: " + ex.Message;
                Trace.WriteLine("BoltClearanceTab.OnRun: " + ex);
            }
            finally { _runBtn.Enabled = true; }
        }

        private void CheckBoltGroup(BoltGroup bg, double wrenchMin, double edgeMin, double spacingMin, bool flagTighten)
        {
            try
            {
                var mainPart = bg.PartToBoltTo as Part;
                if (mainPart == null) return;

                string mark = Helpers.GetReport(mainPart, "ASSEMBLY_POS");
                if (string.IsNullOrEmpty(mark)) mark = Helpers.GetReport(mainPart, "PART_POS");

                var secPart = bg.PartToBeBolted as Part;
                string connection = secPart != null ? Helpers.GetReport(secPart, "PROFILE") : "";
                if (string.IsNullOrEmpty(connection)) connection = "?";

                string boltSize = string.IsNullOrEmpty(bg.BoltSize) ? "?" : bg.BoltSize;

                // Bounding box of main part (AABB in model coordinates)
                var solid = mainPart.GetSolid();
                var minPt = solid.MinimumPoint;
                var maxPt = solid.MaximumPoint;
                var boltPos = bg.FirstPosition;

                // Edge distance: bolt c/l to nearest plate face in XY (AABB approximation)
                double edgeDist = Math.Max(0, Math.Min(
                    Math.Min(boltPos.X - minPt.X, maxPt.X - boltPos.X),
                    Math.Min(boltPos.Y - minPt.Y, maxPt.Y - boltPos.Y)));

                // Wrench clearance: space above bolt to top of part in Z
                double wrenchClearance = Math.Max(0, maxPt.Z - boltPos.Z);

                // Bolt spacing: min spacing from DistanceX / DistanceY arrays
                double minSpacing = double.MaxValue;
                var ba = bg as BoltArray;
                if (ba != null)
                {
                    foreach (ArrayList dList in new[] { ba.DistanceX, ba.DistanceY })
                    {
                        if (dList == null) continue;
                        foreach (object d in dList)
                        {
                            try
                            {
                                double v = Convert.ToDouble(d);
                                if (v > 0) minSpacing = Math.Min(minSpacing, v);
                            }
                            catch { }
                        }
                    }
                }

                // Evaluate checks
                var issues = new List<string>();
                var status = BoltRowStatus.Pass;

                if (edgeDist < edgeMin)
                { issues.Add("✕ Edge distance"); status = BoltRowStatus.Fail; }

                if (wrenchClearance < wrenchMin)
                { issues.Add("✕ Wrench clearance"); status = BoltRowStatus.Fail; }
                else if (flagTighten && wrenchClearance < wrenchMin * 1.1)
                { issues.Add("⚠ Tight — top flange"); if (status == BoltRowStatus.Pass) status = BoltRowStatus.Warn; }

                if (minSpacing < double.MaxValue && minSpacing < spacingMin)
                { issues.Add("✕ Bolt spacing"); status = BoltRowStatus.Fail; }

                string issueText = issues.Count > 0 ? string.Join(", ", issues.ToArray()) : "✓ OK";

                _rows.Add(new BoltCheckRow
                {
                    Mark = string.IsNullOrEmpty(mark) ? "?" : mark,
                    Connection = connection,
                    BoltSize = boltSize,
                    WrenchClearanceMm = wrenchClearance,
                    EdgeDistMm = edgeDist,
                    Status = status,
                    Issue = issueText,
                    SourceObject = mainPart
                });

                if (status == BoltRowStatus.Fail) _failObjects.Add(mainPart);
            }
            catch (Exception ex) { Trace.WriteLine("CheckBoltGroup: " + ex.Message); }
        }

        private void PopulateResults()
        {
            _results.BeginUpdate();
            foreach (var row in _rows.OrderBy(r => r.Status == BoltRowStatus.Fail ? 0 : r.Status == BoltRowStatus.Warn ? 1 : 2))
            {
                var item = new ListViewItem(row.Mark);
                item.SubItems.Add(row.Connection);
                item.SubItems.Add(row.BoltSize);
                item.SubItems.Add(row.WrenchClearanceMm.ToString("0.0"));
                item.SubItems.Add(row.EdgeDistMm.ToString("0.0"));
                item.SubItems.Add(row.Issue);
                if      (row.Status == BoltRowStatus.Fail) item.ForeColor = Color.FromArgb(192, 57, 43);
                else if (row.Status == BoltRowStatus.Warn) item.ForeColor = Color.FromArgb(211, 84,  0);
                _results.Items.Add(item);
            }
            _results.EndUpdate();
        }

        private void OnSelectFailures(object s, EventArgs e)
        {
            if (_failObjects.Count == 0) { MessageBox.Show("No failures to select. Run checks first.", "Bolt Clearance"); return; }
            Helpers.SelectInModel(_failObjects);
            Operation.DisplayPrompt("Selected " + _failObjects.Count + " object(s) with bolt clearance failures.");
        }

        private void OnExportCsv(object s, EventArgs e)
        {
            if (_rows.Count == 0) { MessageBox.Show("Nothing to export. Run checks first.", "Bolt Clearance"); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "bolt_clearance_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Mark,Connection,Bolt,Clearance(mm),EdgeDist(mm),Status,Issue");
                foreach (var row in _rows)
                    sb.AppendLine(string.Join(",", new[] { Q(row.Mark), Q(row.Connection), Q(row.BoltSize),
                        row.WrenchClearanceMm.ToString("0.0"), row.EdgeDistMm.ToString("0.0"),
                        row.Status.ToString(), Q(row.Issue) }));
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exported " + _rows.Count + " rows.\n" + dlg.FileName, "Bolt Clearance",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message, "Bolt Clearance"); }
        }

        private static string Q(string v) { return "\"" + (v ?? "").Replace("\"", "\"\"") + "\""; }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 3 — ERECTION SEQUENCE (placeholder)
    // ════════════════════════════════════════════════════════════════════
    public class ErectionSequenceTab : UserControl
    {
        public ErectionSequenceTab()
        {
            BackColor = Color.WhiteSmoke;
            var lbl = new Label
            {
                Text = "Erection sequence & shear tab orientation checks\r\n— Coming soon —",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 11, FontStyle.Italic),
                Dock = DockStyle.Fill
            };
            Controls.Add(lbl);
        }
    }
}
