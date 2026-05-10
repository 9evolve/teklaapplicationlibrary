// ErectionPlanningTool.cs
// Tekla Structures macro — field erection planning companion to SteelPMToolBOX
// Drop into your macros folder and run via Tools > Macros > ErectionPlanningTool
//
// ─── BEFORE FIRST USE ────────────────────────────────────────────────────────
// Edit the Cfg constants to match your objects.inp UDA names.
// Crane lift chart is saved per-model at <ModelPath>\ErectionPlanningTool.liftchart.csv
//
// Tabs:
//   1. Sequence    — assign per-phase erection sequence numbers
//   2. Crane Picks — group picks, lift chart capacity check, model colorization
//   3. Zone Dash   — lot/zone progress summary
//   4. Field Status— streamlined status stamping, daily log, punchlist export
//   5. Validate    — erection-specific rule engine

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
                Model model = new Model();
                if (!model.GetConnectionStatus())
                {
                    MessageBox.Show("Not connected to a Tekla model.",
                        "Erection Planning Tool",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_form == null || _form.IsDisposed)
                {
                    _form = new MainForm(model);
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
                Trace.WriteLine("ErectionPlanningTool: " + ex.ToString());
                MessageBox.Show("Error: " + ex.Message, "Erection Planning Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   CONFIGURATION  —  EDIT TO MATCH YOUR ENVIRONMENT
    // ════════════════════════════════════════════════════════════════════
    public static class Cfg
    {
        // ── Shared UDA names (mirror SteelPMToolBOX) ─────────────────────
        // GUESS — verify against your objects.inp
        public const string UDA_FIELD_STATUS      = "UDA_FIELD_STATUS";
        public const string UDA_FAB_STATUS        = "UDA_FAB_STATUS";
        public const string UDA_INSPECTOR         = "UDA_INSPECTOR";
        public const string UDA_FAB_LOT           = "UDA_FAB_LOT";
        public const string UDA_FIELD_NOTES       = "UDA_FIELD_NOTES";

        public const string UDA_FIELD_SET_DATE    = "FIELD_SET_DATE";
        public const string UDA_FIELD_BOLT_DATE   = "FIELD_BOLTED_DATE";
        public const string UDA_FIELD_WELD_DATE   = "FIELD_WELDED_DATE";
        public const string UDA_FIELD_INSP_DATE   = "FIELD_INSPECTED_DATE";

        // ── Erection-specific UDA names ───────────────────────────────────
        // GUESS — add to objects.inp or rename to match existing UDAs
        public const string UDA_ERECTION_SEQ      = "ERECTION_SEQUENCE";
        public const string UDA_CRANE_PICK_ID     = "CRANE_PICK_ID";

        // ── Field stage definitions ───────────────────────────────────────
        public static readonly string[] FieldStages = new[]
        {
            "Not started", "Set", "Bolted", "Welded", "Inspected"
        };

        public static string FieldStageDateUda(int idx)
        {
            if (idx == 1) return UDA_FIELD_SET_DATE;
            if (idx == 2) return UDA_FIELD_BOLT_DATE;
            if (idx == 3) return UDA_FIELD_WELD_DATE;
            if (idx == 4) return UDA_FIELD_INSP_DATE;
            return null;
        }

        // Part class numbers applied during crane pick colorization.
        // Map these to colors in your Tekla representation settings.
        public const string CLASS_OVER  = "1";   // over capacity  → configure as red
        public const string CLASS_NEAR  = "4";   // near capacity  → configure as yellow
        public const string CLASS_OK    = "3";   // within capacity→ configure as green
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

        public static List<Assembly> GetAllAssemblies(Model model)
        {
            var result = new List<Assembly>();
            try
            {
                var en = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(new[] { typeof(Assembly) });
                while (en.MoveNext())
                {
                    var a = en.Current as Assembly;
                    if (a != null) result.Add(a);
                }
            }
            catch (Exception ex) { Trace.WriteLine("GetAllAssemblies: " + ex.Message); }
            return result;
        }

        public static string GetReport(ModelObject obj, string prop)
        {
            string val = string.Empty;
            try { obj.GetReportProperty(prop, ref val); } catch { }
            return val == null ? string.Empty : val.Trim();
        }

        public static double GetReportDouble(ModelObject obj, string prop)
        {
            double val = 0;
            try { obj.GetReportProperty(prop, ref val); } catch { }
            return val;
        }

        public static int GetReportInt(ModelObject obj, string prop)
        {
            int val = 0;
            try { obj.GetReportProperty(prop, ref val); } catch { }
            return val;
        }

        public static string GetUda(ModelObject obj, string uda)
        {
            string val = string.Empty;
            try { obj.GetUserProperty(uda, ref val); } catch { }
            return val == null ? string.Empty : val.Trim();
        }

        public static int GetUdaInt(ModelObject obj, string uda)
        {
            int val = 0;
            try { obj.GetUserProperty(uda, ref val); } catch { }
            return val;
        }

        public static string GetAssemblyMark(Assembly assy)
        {
            string mark = GetReport(assy, "ASSEMBLY_POS");
            if (string.IsNullOrEmpty(mark))
            {
                string p = GetReport(assy, "ASSEMBLY_PREFIX");
                string n = GetReport(assy, "ASSEMBLY_NUMBER");
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
                new Tekla.Structures.Model.UI.ModelObjectSelector().Select(arr);
            }
            catch (Exception ex) { Trace.WriteLine("Select: " + ex.Message); }
        }

        // Returns the XY centroid of an assembly's main part center line.
        public static T3D.Point GetAssemblyCentroid(Assembly assy)
        {
            try
            {
                var part = assy.GetMainPart() as Part;
                if (part == null) return null;
                var cl = part.GetCenterLine(true);
                if (cl == null || cl.Count == 0) return null;
                var pts = new List<T3D.Point>();
                foreach (T3D.Point p in cl) pts.Add(p);
                double sx = 0, sy = 0, sz = 0;
                foreach (var p in pts) { sx += p.X; sy += p.Y; sz += p.Z; }
                return new T3D.Point(sx / pts.Count, sy / pts.Count, sz / pts.Count);
            }
            catch { return null; }
        }

        public static int GetPhaseNumber(Assembly assy)
        {
            return GetReportInt(assy, "PHASE");
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

        public MainForm(Model model)
        {
            _model = model;
            Text = "Erection Planning Tool";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(640, 780);
            MinimumSize = new Size(620, 640);
            BuildUI();
        }

        private void BuildUI()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };

            var tp1 = new TabPage("Sequence");
            var seq = new SequencePlannerTab(_model); seq.Dock = DockStyle.Fill;
            tp1.Controls.Add(seq);

            var tp2 = new TabPage("Crane Picks");
            var crane = new CranePickTab(_model); crane.Dock = DockStyle.Fill;
            tp2.Controls.Add(crane);

            var tp3 = new TabPage("Zone Dash");
            var zone = new ZoneDashboardTab(_model); zone.Dock = DockStyle.Fill;
            tp3.Controls.Add(zone);

            var tp4 = new TabPage("Field Status");
            var field = new FieldStatusTab(_model); field.Dock = DockStyle.Fill;
            tp4.Controls.Add(field);

            var tp5 = new TabPage("Validate");
            var val = new ErectionValidatorTab(_model); val.Dock = DockStyle.Fill;
            tp5.Controls.Add(val);

            tabs.TabPages.AddRange(new[] { tp1, tp2, tp3, tp4, tp5 });
            Controls.Add(tabs);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 1 — SEQUENCE PLANNER
    // ════════════════════════════════════════════════════════════════════
    public class SequencePlannerTab : UserControl
    {
        private readonly Model _model;
        private ComboBox _phaseCombo;
        private ListView _list;
        private Label _info;
        private TextBox _seqBox;
        private List<Assembly> _assemblies = new List<Assembly>();

        public SequencePlannerTab(Model model)
        {
            _model = model;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            var phLabel = new Label { Text = "Phase:", Location = new Point(8, y + 4), Size = new Size(48, 18) };
            Controls.Add(phLabel);

            _phaseCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(58, y + 2),
                Size = new Size(100, 22)
            };
            Controls.Add(_phaseCombo);

            Controls.Add(MakeBtn("↻", 162, y, 32, OnLoadPhases));
            Controls.Add(MakeBtn("Load phase", 200, y, 96, OnLoad));
            Controls.Add(MakeBtn("Auto-number", 302, y, 100, OnAutoNumber));
            Controls.Add(MakeBtn("Select in model", 408, y, 120, delegate {
                Helpers.SelectInModel(_assemblies.Cast<ModelObject>());
            }));
            y += 34;

            _list = new ListView
            {
                Location = new Point(8, y),
                Size = new Size(600, 440),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                Font = new Font("Consolas", 9)
            };
            _list.Columns.Add("Seq#", 52);
            _list.Columns.Add("Mark", 90);
            _list.Columns.Add("Profile", 120);
            _list.Columns.Add("Phase", 52);
            _list.Columns.Add("Weight (kg)", 88);
            _list.Columns.Add("Lot", 88);
            Controls.Add(_list);
            y += 450;

            _info = new Label
            {
                Text = "Click ↻ to load phases, then Load phase.",
                Location = new Point(8, y),
                Size = new Size(440, 18),
                ForeColor = Color.Gray
            };
            Controls.Add(_info);
            y += 24;

            var seqLabel = new Label { Text = "Set seq# on selected rows:", Location = new Point(8, y + 4), Size = new Size(170, 18) };
            Controls.Add(seqLabel);

            _seqBox = new TextBox { Location = new Point(180, y + 2), Size = new Size(60, 20), Text = "1" };
            Controls.Add(_seqBox);

            Controls.Add(MakeBtn("Set", 246, y, 48, delegate {
                int n;
                if (!int.TryParse(_seqBox.Text, out n)) return;
                foreach (ListViewItem item in _list.SelectedItems)
                    item.Text = n.ToString();
            }));

            var applyBtn = new Button
            {
                Text = "Apply to model",
                Location = new Point(454, y - 4),
                Size = new Size(154, 30),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };
            applyBtn.Click += new EventHandler(OnApply);
            Controls.Add(applyBtn);
            y += 36;

            Controls.Add(MakeBtn("Export CSV", 8, y, 100, OnExport));
        }

        private void OnLoadPhases(object sender, EventArgs e)
        {
            _phaseCombo.Items.Clear();
            var phases = new HashSet<int>();
            foreach (var a in Helpers.GetAllAssemblies(_model))
                phases.Add(Helpers.GetPhaseNumber(a));
            foreach (var p in phases.OrderBy(x => x))
                _phaseCombo.Items.Add(p.ToString());
            if (_phaseCombo.Items.Count > 0) _phaseCombo.SelectedIndex = 0;
            _info.Text = "Found " + _phaseCombo.Items.Count + " phase(s).";
        }

        private void OnLoad(object sender, EventArgs e)
        {
            if (_phaseCombo.SelectedItem == null) { MessageBox.Show("Select a phase first."); return; }
            int phaseNum;
            int.TryParse(_phaseCombo.SelectedItem.ToString(), out phaseNum);

            _assemblies = Helpers.GetAllAssemblies(_model)
                .Where(a => Helpers.GetPhaseNumber(a) == phaseNum)
                .ToList();

            _list.Items.Clear();
            foreach (var a in _assemblies)
            {
                int seq = Helpers.GetUdaInt(a, Cfg.UDA_ERECTION_SEQ);
                double wt = Helpers.GetReportDouble(a, "WEIGHT");
                string lot = Helpers.GetUda(a, Cfg.UDA_FAB_LOT);

                var item = new ListViewItem(seq > 0 ? seq.ToString() : "");
                item.SubItems.Add(Helpers.GetAssemblyMark(a));
                item.SubItems.Add(Helpers.GetReport(a, "PROFILE"));
                item.SubItems.Add(phaseNum.ToString());
                item.SubItems.Add(wt.ToString("0.0"));
                item.SubItems.Add(lot);
                item.Tag = a;
                _list.Items.Add(item);
            }
            _info.Text = "Phase " + phaseNum + ": " + _assemblies.Count + " assemblies.";
        }

        private void OnAutoNumber(object sender, EventArgs e)
        {
            int i = 1;
            foreach (ListViewItem item in _list.Items)
                item.Text = (i++).ToString();
        }

        private void OnApply(object sender, EventArgs e)
        {
            int updated = 0, failed = 0;
            foreach (ListViewItem item in _list.Items)
            {
                var a = item.Tag as Assembly;
                if (a == null) continue;
                int seq;
                if (!int.TryParse(item.Text, out seq)) continue;
                try
                {
                    a.SetUserProperty(Cfg.UDA_ERECTION_SEQ, seq);
                    if (a.Modify()) updated++; else failed++;
                }
                catch { failed++; }
            }
            _model.CommitChanges();
            _info.Text = "Applied: " + updated + "  Failed: " + failed;
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_list.Items.Count == 0) { MessageBox.Show("Load a phase first."); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "erection_sequence.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using (var w = new StreamWriter(dlg.FileName))
                {
                    w.WriteLine("Sequence,Mark,Profile,Phase,Weight_kg,Lot");
                    foreach (ListViewItem item in _list.Items)
                    {
                        var cells = new[] { item.Text }.Concat(
                            item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                                .Skip(1).Select(s => s.Text)).ToArray();
                        w.WriteLine(string.Join(",", cells));
                    }
                }
                MessageBox.Show("Exported " + _list.Items.Count + " rows.");
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }

        private Button MakeBtn(string text, int x, int y, int w, EventHandler click)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 26) };
            if (click != null) b.Click += click;
            return b;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 2 — CRANE PICK MANAGER
    // ════════════════════════════════════════════════════════════════════
    public class CranePickTab : UserControl
    {
        private readonly Model _model;
        private Label _craneLabel;
        private T3D.Point _cranePos = null;
        private DataGridView _liftGrid;
        private TextBox _pickIdBox;
        private ListView _pickSummary;
        private Label _info;
        // Stores original Part.Class values before colorization so Clear can restore them.
        // Keyed by Identifier.GUID string.
        private Dictionary<string, string> _originalClasses = new Dictionary<string, string>();

        public CranePickTab(Model model)
        {
            _model = model;
            BuildUI();
            LoadLiftChart();
        }

        private void BuildUI()
        {
            int y = 8;

            // ── Crane setup ──────────────────────────────────────────────
            var cg = new GroupBox { Text = "Crane Setup", Location = new Point(8, y), Size = new Size(608, 150) };

            _craneLabel = new Label
            {
                Text = "Position: (not set — click Pick to select a point in the model)",
                Location = new Point(8, 24),
                Size = new Size(460, 18),
                ForeColor = Color.Gray
            };
            cg.Controls.Add(_craneLabel);

            var pickCraneBtn = new Button
            {
                Text = "Pick crane position…",
                Location = new Point(474, 20),
                Size = new Size(120, 24)
            };
            pickCraneBtn.Click += new EventHandler(OnPickCranePos);
            cg.Controls.Add(pickCraneBtn);

            var lcHint = new Label
            {
                Text = "Lift chart — Radius (ft) → Max Capacity (short tons). Saved per-model.",
                Location = new Point(8, 52),
                Size = new Size(440, 18),
                ForeColor = Color.Gray
            };
            cg.Controls.Add(lcHint);

            var addRowBtn = new Button { Text = "+ Row", Location = new Point(454, 49), Size = new Size(58, 22) };
            addRowBtn.Click += delegate { _liftGrid.Rows.Add("", ""); };
            cg.Controls.Add(addRowBtn);

            var delRowBtn = new Button { Text = "− Row", Location = new Point(516, 49), Size = new Size(58, 22) };
            delRowBtn.Click += delegate {
                if (_liftGrid.SelectedRows.Count > 0)
                    _liftGrid.Rows.Remove(_liftGrid.SelectedRows[0]);
            };
            cg.Controls.Add(delRowBtn);

            var saveLcBtn = new Button { Text = "Save chart", Location = new Point(474, 75), Size = new Size(120, 22) };
            saveLcBtn.Click += delegate { SaveLiftChart(); };
            cg.Controls.Add(saveLcBtn);

            _liftGrid = new DataGridView
            {
                Location = new Point(8, 75),
                Size = new Size(458, 66),
                AllowUserToAddRows = false,
                ColumnHeadersHeight = 22,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8)
            };
            _liftGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Radius (ft)", Width = 100 });
            _liftGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Max Cap (tons)", Width = 120 });
            cg.Controls.Add(_liftGrid);

            Controls.Add(cg);
            y += 160;

            // ── Pick assignment ──────────────────────────────────────────
            var pg = new GroupBox { Text = "Pick Assignment", Location = new Point(8, y), Size = new Size(608, 60) };

            pg.Controls.Add(new Label { Text = "Pick ID:", Location = new Point(8, 26), Size = new Size(56, 18) });

            _pickIdBox = new TextBox { Location = new Point(66, 24), Size = new Size(100, 20), Text = "P-001" };
            pg.Controls.Add(_pickIdBox);

            var assignBtn = new Button
            {
                Text = "Assign to selection",
                Location = new Point(172, 21),
                Size = new Size(140, 26),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White
            };
            assignBtn.Click += new EventHandler(OnAssignPick);
            pg.Controls.Add(assignBtn);

            var clearPickBtn = new Button { Text = "Clear pick on selection", Location = new Point(318, 21), Size = new Size(160, 26) };
            clearPickBtn.Click += delegate {
                foreach (var o in Helpers.GetSelectedObjects())
                {
                    o.SetUserProperty(Cfg.UDA_CRANE_PICK_ID, "");
                    o.Modify();
                }
                _model.CommitChanges();
                _info.Text = "Pick ID cleared on selection.";
            };
            pg.Controls.Add(clearPickBtn);

            Controls.Add(pg);
            y += 70;

            // ── Pick summary ─────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text = "Pick Summary:",
                Location = new Point(8, y + 4),
                Size = new Size(120, 18),
                Font = new Font(Font, FontStyle.Bold)
            });
            y += 24;

            _pickSummary = new ListView
            {
                Location = new Point(8, y),
                Size = new Size(608, 240),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                Font = new Font("Consolas", 9)
            };
            _pickSummary.Columns.Add("Pick ID", 80);
            _pickSummary.Columns.Add("Pcs", 40);
            _pickSummary.Columns.Add("Weight (t)", 82);
            _pickSummary.Columns.Add("Radius (ft)", 82);
            _pickSummary.Columns.Add("Cap (tons)", 82);
            _pickSummary.Columns.Add("Status", 120);
            Controls.Add(_pickSummary);
            y += 250;

            _info = new Label { Text = "", Location = new Point(8, y), Size = new Size(500, 18), ForeColor = Color.Gray };
            Controls.Add(_info);
            y += 22;

            var calcBtn = new Button
            {
                Text = "Calculate all picks",
                Location = new Point(8, y),
                Size = new Size(150, 28),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };
            calcBtn.Click += new EventHandler(OnCalculate);
            Controls.Add(calcBtn);

            var colorBtn = new Button { Text = "Color model", Location = new Point(164, y), Size = new Size(110, 28) };
            colorBtn.Click += new EventHandler(OnColorModel);
            Controls.Add(colorBtn);

            var clearColorBtn = new Button { Text = "Clear colors", Location = new Point(280, y), Size = new Size(110, 28) };
            clearColorBtn.Click += new EventHandler(OnClearColors);
            Controls.Add(clearColorBtn);

            var selOverBtn = new Button { Text = "Select over-cap", Location = new Point(396, y), Size = new Size(130, 28) };
            selOverBtn.Click += new EventHandler(OnSelectOverCapacity);
            Controls.Add(selOverBtn);
        }

        private void OnPickCranePos(object sender, EventArgs e)
        {
            var t = new System.Threading.Thread(delegate()
            {
                try
                {
                    var picker = new Tekla.Structures.Model.UI.Picker();
                    T3D.Point pt = picker.PickPoint("Pick crane base position (XY only used)");
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        _cranePos = pt;
                        _craneLabel.Text = string.Format(
                            "Position: X={0:0.#}  Y={1:0.#}  Z={2:0.#} mm",
                            pt.X, pt.Y, pt.Z);
                        _craneLabel.ForeColor = Color.FromArgb(0, 140, 0);
                    }));
                }
                catch
                {
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        _craneLabel.Text = "Position: pick cancelled";
                    }));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OnAssignPick(object sender, EventArgs e)
        {
            string pickId = _pickIdBox.Text.Trim();
            if (string.IsNullOrEmpty(pickId)) { MessageBox.Show("Enter a Pick ID."); return; }
            var sel = Helpers.GetSelectedObjects();
            if (sel.Count == 0) { MessageBox.Show("No selection."); return; }
            int ok = 0;
            foreach (var o in sel)
            {
                o.SetUserProperty(Cfg.UDA_CRANE_PICK_ID, pickId);
                if (o.Modify()) ok++;
            }
            _model.CommitChanges();
            _info.Text = "Assigned pick \"" + pickId + "\" to " + ok + " object(s).";
        }

        private struct LiftEntry { public double Radius; public double Capacity; }

        private List<LiftEntry> GetLiftChart()
        {
            var entries = new List<LiftEntry>();
            foreach (DataGridViewRow row in _liftGrid.Rows)
            {
                string rv = row.Cells[0].Value != null ? row.Cells[0].Value.ToString() : "";
                string cv = row.Cells[1].Value != null ? row.Cells[1].Value.ToString() : "";
                double r, c;
                if (double.TryParse(rv, out r) && double.TryParse(cv, out c) && r > 0 && c > 0)
                    entries.Add(new LiftEntry { Radius = r, Capacity = c });
            }
            entries.Sort((a, b) => a.Radius.CompareTo(b.Radius));
            return entries;
        }

        private double Interpolate(List<LiftEntry> chart, double radius)
        {
            if (chart.Count == 0) return double.MaxValue;
            if (radius <= chart[0].Radius) return chart[0].Capacity;
            if (radius >= chart[chart.Count - 1].Radius) return chart[chart.Count - 1].Capacity;
            for (int i = 0; i < chart.Count - 1; i++)
            {
                if (radius >= chart[i].Radius && radius <= chart[i + 1].Radius)
                {
                    double t = (radius - chart[i].Radius) / (chart[i + 1].Radius - chart[i].Radius);
                    return chart[i].Capacity + t * (chart[i + 1].Capacity - chart[i].Capacity);
                }
            }
            return chart[chart.Count - 1].Capacity;
        }

        private void OnCalculate(object sender, EventArgs e)
        {
            var chart = GetLiftChart();
            var allAssemblies = Helpers.GetAllAssemblies(_model);

            var picks = new Dictionary<string, List<Assembly>>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in allAssemblies)
            {
                string pid = Helpers.GetUda(a, Cfg.UDA_CRANE_PICK_ID);
                if (string.IsNullOrEmpty(pid)) continue;
                if (!picks.ContainsKey(pid)) picks[pid] = new List<Assembly>();
                picks[pid].Add(a);
            }

            _pickSummary.Items.Clear();
            int overCount = 0;

            foreach (var kv in picks.OrderBy(x => x.Key))
            {
                double totalWeightKg = 0;
                double maxRadius = 0;

                foreach (var a in kv.Value)
                {
                    totalWeightKg += Helpers.GetReportDouble(a, "WEIGHT");
                    if (_cranePos != null)
                    {
                        T3D.Point pt = Helpers.GetAssemblyCentroid(a);
                        if (pt != null)
                        {
                            double dx = pt.X - _cranePos.X;
                            double dy = pt.Y - _cranePos.Y;
                            double r = Math.Sqrt(dx * dx + dy * dy);
                            if (r > maxRadius) maxRadius = r;
                        }
                    }
                }

                // kg → short tons; mm → ft
                double weightTons = totalWeightKg / 907.185;
                double radiusFt   = maxRadius / 304.8;
                double capTons    = (_cranePos != null && chart.Count > 0)
                    ? Interpolate(chart, radiusFt) : double.MaxValue;

                double ratio = (capTons > 0 && capTons != double.MaxValue)
                    ? weightTons / capTons : 0;

                string status;
                Color rowColor;
                if (capTons == double.MaxValue)
                {
                    status   = _cranePos == null ? "No crane pos" : "No chart";
                    rowColor = Color.White;
                }
                else if (ratio > 1.0)
                {
                    status   = "OVER (" + (ratio * 100).ToString("0") + "%)";
                    rowColor = Color.FromArgb(255, 180, 180);
                    overCount++;
                }
                else if (ratio >= 0.80)
                {
                    status   = "Near (" + (ratio * 100).ToString("0") + "%)";
                    rowColor = Color.FromArgb(255, 242, 153);
                }
                else
                {
                    status   = "OK (" + (ratio * 100).ToString("0") + "%)";
                    rowColor = Color.FromArgb(180, 230, 180);
                }

                var item = new ListViewItem(kv.Key);
                item.SubItems.Add(kv.Value.Count.ToString());
                item.SubItems.Add(weightTons.ToString("0.00"));
                item.SubItems.Add(_cranePos != null ? radiusFt.ToString("0.0") : "—");
                item.SubItems.Add(capTons == double.MaxValue ? "—" : capTons.ToString("0.00"));
                item.SubItems.Add(status);
                item.BackColor = rowColor;
                item.Tag = kv.Value;
                _pickSummary.Items.Add(item);
            }

            _info.Text = picks.Count + " pick(s) calculated." +
                (overCount > 0 ? "  ⚠ " + overCount + " pick(s) OVER capacity!" : "  All picks OK.");
        }

        private void OnColorModel(object sender, EventArgs e)
        {
            if (_pickSummary.Items.Count == 0)
            {
                MessageBox.Show("Run 'Calculate all picks' first.");
                return;
            }
            _originalClasses.Clear();

            foreach (ListViewItem item in _pickSummary.Items)
            {
                var assys = item.Tag as List<Assembly>;
                if (assys == null) continue;

                string status  = item.SubItems[5].Text;
                string classVal = status.StartsWith("OVER") ? Cfg.CLASS_OVER
                    : status.StartsWith("Near") ? Cfg.CLASS_NEAR : Cfg.CLASS_OK;

                foreach (var a in assys)
                {
                    var part = a.GetMainPart() as Part;
                    if (part == null) continue;
                    string guid = part.Identifier.GUID.ToString();
                    if (!_originalClasses.ContainsKey(guid))
                        _originalClasses[guid] = part.Class;
                    part.Class = classVal;
                    part.Modify();
                }
            }
            _model.CommitChanges();
            _info.Text = "Colors applied (classes " + Cfg.CLASS_OVER + "/" + Cfg.CLASS_NEAR +
                "/" + Cfg.CLASS_OK + "). Map these to colors in your Tekla representation settings.";
        }

        private void OnClearColors(object sender, EventArgs e)
        {
            if (_originalClasses.Count == 0) { _info.Text = "No colors to clear."; return; }
            foreach (var a in Helpers.GetAllAssemblies(_model))
            {
                var part = a.GetMainPart() as Part;
                if (part == null) continue;
                string guid = part.Identifier.GUID.ToString();
                string orig;
                if (_originalClasses.TryGetValue(guid, out orig))
                {
                    part.Class = orig;
                    part.Modify();
                }
            }
            _model.CommitChanges();
            _originalClasses.Clear();
            _info.Text = "Colors cleared.";
        }

        private void OnSelectOverCapacity(object sender, EventArgs e)
        {
            var over = new List<ModelObject>();
            foreach (ListViewItem item in _pickSummary.Items)
            {
                if (!item.SubItems[5].Text.StartsWith("OVER")) continue;
                var assys = item.Tag as List<Assembly>;
                if (assys != null) over.AddRange(assys.Cast<ModelObject>());
            }
            if (over.Count == 0) { MessageBox.Show("No over-capacity picks found. Run Calculate first."); return; }
            Helpers.SelectInModel(over);
            Operation.DisplayPrompt("Selected " + over.Count + " object(s) in over-capacity picks.");
        }

        private string LiftChartPath()
        {
            try
            {
                string mp = _model.GetInfo().ModelPath;
                if (!string.IsNullOrEmpty(mp))
                    return Path.Combine(mp, "ErectionPlanningTool.liftchart.csv");
            }
            catch { }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ErectionPlanningTool.liftchart.csv");
        }

        private void SaveLiftChart()
        {
            try
            {
                using (var w = new StreamWriter(LiftChartPath()))
                {
                    w.WriteLine("# ErectionPlanningTool lift chart — Radius (ft), Capacity (short tons)");
                    foreach (DataGridViewRow row in _liftGrid.Rows)
                    {
                        string rv = row.Cells[0].Value != null ? row.Cells[0].Value.ToString() : "";
                        string cv = row.Cells[1].Value != null ? row.Cells[1].Value.ToString() : "";
                        if (!string.IsNullOrEmpty(rv) && !string.IsNullOrEmpty(cv))
                            w.WriteLine(rv + "," + cv);
                    }
                }
                _info.Text = "Lift chart saved to " + LiftChartPath();
            }
            catch (Exception ex) { MessageBox.Show("Save failed: " + ex.Message); }
        }

        private void LoadLiftChart()
        {
            try
            {
                string path = LiftChartPath();
                if (!File.Exists(path)) return;
                _liftGrid.Rows.Clear();
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.TrimStart().StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 2) _liftGrid.Rows.Add(parts[0].Trim(), parts[1].Trim());
                }
            }
            catch (Exception ex) { Trace.WriteLine("LoadLiftChart: " + ex.Message); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 3 — ZONE DASHBOARD
    // ════════════════════════════════════════════════════════════════════
    public class ZoneDashboardTab : UserControl
    {
        private readonly Model _model;
        private ListView _zoneList;
        private Label _info;

        public ZoneDashboardTab(Model model)
        {
            _model = model;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            Controls.Add(new Label
            {
                Text = "Groups assemblies by " + Cfg.UDA_FAB_LOT + ". Progress from " + Cfg.UDA_FIELD_STATUS + ".",
                Location = new Point(8, y),
                Size = new Size(600, 18),
                ForeColor = Color.Gray
            });
            y += 26;

            _zoneList = new ListView
            {
                Location = new Point(8, y),
                Size = new Size(600, 490),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                Font = new Font("Consolas", 9)
            };
            _zoneList.Columns.Add("Zone / Lot", 100);
            _zoneList.Columns.Add("Total", 50);
            _zoneList.Columns.Add("Not started", 80);
            _zoneList.Columns.Add("Set", 50);
            _zoneList.Columns.Add("Bolted", 60);
            _zoneList.Columns.Add("Welded", 60);
            _zoneList.Columns.Add("Inspected", 70);
            _zoneList.Columns.Add("% Done", 60);
            Controls.Add(_zoneList);
            y += 500;

            _info = new Label { Text = "", Location = new Point(8, y), Size = new Size(400, 18), ForeColor = Color.Gray };
            Controls.Add(_info);
            y += 24;

            var refreshBtn = new Button
            {
                Text = "Refresh",
                Location = new Point(8, y),
                Size = new Size(100, 28),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };
            refreshBtn.Click += new EventHandler(OnRefresh);
            Controls.Add(refreshBtn);

            var exportBtn = new Button { Text = "Export CSV", Location = new Point(114, y), Size = new Size(100, 28) };
            exportBtn.Click += new EventHandler(OnExport);
            Controls.Add(exportBtn);

            var selBtn = new Button { Text = "Select zone in model", Location = new Point(220, y), Size = new Size(160, 28) };
            selBtn.Click += new EventHandler(OnSelectZone);
            Controls.Add(selBtn);
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            var assemblies = Helpers.GetAllAssemblies(_model);
            // counts[]: total, not-started, set, bolted, welded, inspected
            var zones = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in assemblies)
            {
                string lot = Helpers.GetUda(a, Cfg.UDA_FAB_LOT);
                if (string.IsNullOrEmpty(lot)) lot = "(no lot)";
                if (!zones.ContainsKey(lot)) zones[lot] = new int[6];
                int[] c = zones[lot];
                c[0]++;

                string fs = Helpers.GetUda(a, Cfg.UDA_FIELD_STATUS);
                int idx = Array.IndexOf(Cfg.FieldStages, fs);
                if      (idx <= 0) c[1]++;
                else if (idx == 1) c[2]++;
                else if (idx == 2) c[3]++;
                else if (idx == 3) c[4]++;
                else if (idx == 4) c[5]++;
            }

            _zoneList.Items.Clear();
            foreach (var kv in zones.OrderBy(x => x.Key))
            {
                int[] c = kv.Value;
                double pct = c[0] > 0 ? (c[5] * 100.0 / c[0]) : 0;
                var item = new ListViewItem(kv.Key);
                item.SubItems.Add(c[0].ToString());
                item.SubItems.Add(c[1].ToString());
                item.SubItems.Add(c[2].ToString());
                item.SubItems.Add(c[3].ToString());
                item.SubItems.Add(c[4].ToString());
                item.SubItems.Add(c[5].ToString());
                item.SubItems.Add(pct.ToString("0") + "%");
                item.Tag = kv.Key;
                if (pct >= 100) item.BackColor = Color.FromArgb(180, 230, 180);
                else if (pct >= 50) item.BackColor = Color.FromArgb(255, 242, 153);
                _zoneList.Items.Add(item);
            }
            _info.Text = zones.Count + " zone(s) · " + assemblies.Count + " total assemblies.";
        }

        private void OnSelectZone(object sender, EventArgs e)
        {
            if (_zoneList.SelectedItems.Count == 0) { MessageBox.Show("Select a zone row first."); return; }
            string lot = _zoneList.SelectedItems[0].Tag as string;
            var toSelect = Helpers.GetAllAssemblies(_model)
                .Where(a => {
                    string l = Helpers.GetUda(a, Cfg.UDA_FAB_LOT);
                    if (string.IsNullOrEmpty(l)) l = "(no lot)";
                    return string.Equals(l, lot, StringComparison.OrdinalIgnoreCase);
                })
                .Cast<ModelObject>().ToList();
            Helpers.SelectInModel(toSelect);
            Operation.DisplayPrompt("Selected " + toSelect.Count + " object(s) in zone \"" + lot + "\".");
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_zoneList.Items.Count == 0) { MessageBox.Show("Click Refresh first."); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "zone_dashboard.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using (var w = new StreamWriter(dlg.FileName))
                {
                    w.WriteLine("Zone,Total,NotStarted,Set,Bolted,Welded,Inspected,PctDone");
                    foreach (ListViewItem item in _zoneList.Items)
                    {
                        var cells = new[] { item.Text }.Concat(
                            item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                                .Skip(1).Select(s => s.Text)).ToArray();
                        w.WriteLine(string.Join(",", cells));
                    }
                }
                MessageBox.Show("Exported " + _zoneList.Items.Count + " rows.");
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 4 — FIELD STATUS
    // ════════════════════════════════════════════════════════════════════
    public class FieldStatusTab : UserControl
    {
        private readonly Model _model;
        private ComboBox _statusCombo;
        private TextBox _inspectorBox;
        private CheckBox _stampDate;
        private Label _selInfo;
        private ListView _dailyLog;
        private Label _dailyInfo;

        public FieldStatusTab(Model model)
        {
            _model = model;
            BuildUI();
        }

        private void BuildUI()
        {
            int y = 8;

            _selInfo = new Label
            {
                Text = "▸ Select assemblies in model, then set status below.",
                Location = new Point(8, y),
                Size = new Size(600, 18),
                BackColor = Color.FromArgb(255, 248, 225),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            Controls.Add(_selInfo);
            y += 24;

            var sg = new GroupBox { Text = "Set Field Status", Location = new Point(8, y), Size = new Size(608, 100) };

            sg.Controls.Add(new Label { Text = "Status:", Location = new Point(8, 26), Size = new Size(56, 18) });

            _statusCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(66, 24),
                Size = new Size(160, 22)
            };
            foreach (var s in Cfg.FieldStages) _statusCombo.Items.Add(s);
            _statusCombo.SelectedIndex = 0;
            sg.Controls.Add(_statusCombo);

            _stampDate = new CheckBox
            {
                Text = "Auto-stamp date UDA with today (" + Helpers.Today() + ")",
                Location = new Point(8, 52),
                Size = new Size(320, 18),
                Checked = true
            };
            sg.Controls.Add(_stampDate);

            sg.Controls.Add(new Label { Text = "Inspector:", Location = new Point(8, 74), Size = new Size(66, 18) });

            _inspectorBox = new TextBox { Location = new Point(76, 72), Size = new Size(140, 20) };
            sg.Controls.Add(_inspectorBox);

            var applyBtn = new Button
            {
                Text = "Apply to selection",
                Location = new Point(444, 24),
                Size = new Size(152, 30),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };
            applyBtn.Click += new EventHandler(OnApplyStatus);
            sg.Controls.Add(applyBtn);

            Controls.Add(sg);
            y += 110;

            var dlg = new GroupBox
            {
                Text = "Today's Activity  (" + Helpers.Today() + ")",
                Location = new Point(8, y),
                Size = new Size(608, 230)
            };

            var refreshBtn = new Button { Text = "Refresh", Location = new Point(8, 22), Size = new Size(80, 24) };
            refreshBtn.Click += new EventHandler(OnRefreshDaily);
            dlg.Controls.Add(refreshBtn);

            var punchBtn = new Button { Text = "Export inspection punchlist", Location = new Point(94, 22), Size = new Size(190, 24) };
            punchBtn.Click += new EventHandler(OnExportPunchlist);
            dlg.Controls.Add(punchBtn);

            _dailyLog = new ListView
            {
                Location = new Point(8, 52),
                Size = new Size(588, 140),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                Font = new Font("Consolas", 9)
            };
            _dailyLog.Columns.Add("Mark", 90);
            _dailyLog.Columns.Add("Status", 90);
            _dailyLog.Columns.Add("Inspector", 100);
            _dailyLog.Columns.Add("Date", 90);
            _dailyLog.Columns.Add("Lot", 90);
            dlg.Controls.Add(_dailyLog);

            _dailyInfo = new Label { Text = "", Location = new Point(8, 198), Size = new Size(588, 18), ForeColor = Color.Gray };
            dlg.Controls.Add(_dailyInfo);

            Controls.Add(dlg);
        }

        private void OnApplyStatus(object sender, EventArgs e)
        {
            var sel = Helpers.GetSelectedObjects();
            if (sel.Count == 0) { MessageBox.Show("No selection."); return; }

            string newStatus = _statusCombo.SelectedItem.ToString();
            string inspector = _inspectorBox.Text.Trim();
            string today     = Helpers.Today();
            int updated = 0, failed = 0;

            foreach (var obj in sel)
            {
                try
                {
                    obj.SetUserProperty(Cfg.UDA_FIELD_STATUS, newStatus);
                    if (_stampDate.Checked)
                    {
                        int idx = Array.IndexOf(Cfg.FieldStages, newStatus);
                        string dateUda = Cfg.FieldStageDateUda(idx);
                        if (dateUda != null) obj.SetUserProperty(dateUda, today);
                    }
                    if (!string.IsNullOrEmpty(inspector))
                        obj.SetUserProperty(Cfg.UDA_INSPECTOR, inspector);
                    if (obj.Modify()) updated++; else failed++;
                }
                catch { failed++; }
            }
            _model.CommitChanges();
            _selInfo.Text = "▸ Updated " + updated + " to \"" + newStatus + "\"." +
                (failed > 0 ? "  " + failed + " failed." : "");
        }

        private void OnRefreshDaily(object sender, EventArgs e)
        {
            string today = Helpers.Today();
            _dailyLog.Items.Clear();
            int count = 0;

            foreach (var a in Helpers.GetAllAssemblies(_model))
            {
                string fs = Helpers.GetUda(a, Cfg.UDA_FIELD_STATUS);
                if (string.IsNullOrEmpty(fs)) continue;

                bool activityToday = false;
                for (int i = 1; i <= 4; i++)
                {
                    string du = Cfg.FieldStageDateUda(i);
                    if (du != null && Helpers.GetUda(a, du) == today)
                    { activityToday = true; break; }
                }
                if (!activityToday) continue;

                var item = new ListViewItem(Helpers.GetAssemblyMark(a));
                item.SubItems.Add(fs);
                item.SubItems.Add(Helpers.GetUda(a, Cfg.UDA_INSPECTOR));
                item.SubItems.Add(today);
                item.SubItems.Add(Helpers.GetUda(a, Cfg.UDA_FAB_LOT));
                _dailyLog.Items.Add(item);
                count++;
            }
            _dailyInfo.Text = count + " piece(s) with activity today.";
        }

        private void OnExportPunchlist(object sender, EventArgs e)
        {
            var items = Helpers.GetAllAssemblies(_model)
                .Where(a => {
                    string fs = Helpers.GetUda(a, Cfg.UDA_FIELD_STATUS);
                    return string.Equals(fs, "Bolted", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fs, "Welded", StringComparison.OrdinalIgnoreCase);
                }).ToList();

            if (items.Count == 0)
            {
                MessageBox.Show("No bolted/welded pieces awaiting inspection.");
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = "inspection_punchlist_" + Helpers.Today() + ".csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using (var w = new StreamWriter(dlg.FileName))
                {
                    w.WriteLine("Mark,Profile,FieldStatus,Lot,Inspector");
                    foreach (var a in items)
                        w.WriteLine(string.Join(",", new[]
                        {
                            Helpers.GetAssemblyMark(a),
                            Helpers.GetReport(a, "PROFILE"),
                            Helpers.GetUda(a, Cfg.UDA_FIELD_STATUS),
                            Helpers.GetUda(a, Cfg.UDA_FAB_LOT),
                            Helpers.GetUda(a, Cfg.UDA_INSPECTOR)
                        }));
                }
                MessageBox.Show("Punchlist exported: " + items.Count + " piece(s).");
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   TAB 5 — ERECTION VALIDATOR
    // ════════════════════════════════════════════════════════════════════
    public class ErectionValidatorTab : UserControl
    {
        private readonly Model _model;
        private RadioButton _scopeAll, _scopeSel;
        private CheckedListBox _ruleList;
        private ListView _results;
        private Label _summary;
        private List<ErectionRule> _rules;
        private List<ModelObject> _lastFailures = new List<ModelObject>();

        public ErectionValidatorTab(Model model)
        {
            _model = model;
            _rules = BuildRules();
            BuildUI();
        }

        private List<ErectionRule> BuildRules()
        {
            return new List<ErectionRule>
            {
                new ErectionRule(
                    "Erection sequence assigned",
                    Cfg.UDA_ERECTION_SEQ + " > 0",
                    o => Helpers.GetUdaInt(o, Cfg.UDA_ERECTION_SEQ) > 0),

                new ErectionRule(
                    "Crane pick ID assigned",
                    Cfg.UDA_CRANE_PICK_ID + " not blank",
                    o => !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_CRANE_PICK_ID))),

                new ErectionRule(
                    "Set pieces have set date",
                    "if Field Status=Set → " + Cfg.UDA_FIELD_SET_DATE + " not blank",
                    o => {
                        string fs = Helpers.GetUda(o, Cfg.UDA_FIELD_STATUS);
                        if (!string.Equals(fs, "Set", StringComparison.OrdinalIgnoreCase)) return true;
                        return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_FIELD_SET_DATE));
                    }),

                new ErectionRule(
                    "Inspected pieces have inspector",
                    "if Field Status=Inspected → " + Cfg.UDA_INSPECTOR + " not blank",
                    o => {
                        string fs = Helpers.GetUda(o, Cfg.UDA_FIELD_STATUS);
                        if (!string.Equals(fs, "Inspected", StringComparison.OrdinalIgnoreCase)) return true;
                        return !string.IsNullOrEmpty(Helpers.GetUda(o, Cfg.UDA_INSPECTOR));
                    }),

                new ErectionRule(
                    "Sequenced pieces are shipped or on site",
                    "if Seq# > 0 → Fab Status = Shipped or On site",
                    o => {
                        if (Helpers.GetUdaInt(o, Cfg.UDA_ERECTION_SEQ) <= 0) return true;
                        string fab = Helpers.GetUda(o, Cfg.UDA_FAB_STATUS);
                        return string.Equals(fab, "Shipped", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(fab, "On site",  StringComparison.OrdinalIgnoreCase);
                    })
            };
        }

        private void BuildUI()
        {
            int y = 8;

            var sg = new GroupBox { Text = "Scope", Location = new Point(8, y), Size = new Size(608, 56) };
            _scopeAll = new RadioButton { Text = "All assemblies in model", Location = new Point(12, 22), Size = new Size(180, 18), Checked = true };
            _scopeSel = new RadioButton { Text = "Current selection", Location = new Point(200, 22), Size = new Size(150, 18) };
            sg.Controls.Add(_scopeAll);
            sg.Controls.Add(_scopeSel);
            Controls.Add(sg);
            y += 66;

            var rg = new GroupBox { Text = "Rules", Location = new Point(8, y), Size = new Size(608, 118) };
            _ruleList = new CheckedListBox
            {
                Location = new Point(8, 20),
                Size = new Size(588, 88),
                CheckOnClick = true
            };
            foreach (var r in _rules)
                _ruleList.Items.Add(r.Name + "  —  " + r.Description, true);
            rg.Controls.Add(_ruleList);
            Controls.Add(rg);
            y += 128;

            _results = new ListView
            {
                Location = new Point(8, y),
                Size = new Size(608, 310),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                Font = new Font("Consolas", 9)
            };
            _results.Columns.Add("Mark", 90);
            _results.Columns.Add("Profile", 110);
            _results.Columns.Add("Phase", 50);
            _results.Columns.Add("Pick ID", 80);
            _results.Columns.Add("Failed Rule", 260);
            Controls.Add(_results);
            y += 320;

            _summary = new Label { Text = "Run rules to populate.", Location = new Point(8, y), Size = new Size(500, 18) };
            Controls.Add(_summary);
            y += 24;

            var selBtn = new Button { Text = "Select in model", Location = new Point(8, y), Size = new Size(130, 28) };
            selBtn.Click += delegate {
                if (_lastFailures.Count == 0) { MessageBox.Show("No failures."); return; }
                Helpers.SelectInModel(_lastFailures);
                Operation.DisplayPrompt("Selected " + _lastFailures.Count + " failing object(s).");
            };
            Controls.Add(selBtn);

            var exportBtn = new Button { Text = "Export CSV", Location = new Point(144, y), Size = new Size(100, 28) };
            exportBtn.Click += new EventHandler(OnExport);
            Controls.Add(exportBtn);

            var runBtn = new Button
            {
                Text = "Run rules",
                Location = new Point(454, y),
                Size = new Size(160, 30),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };
            runBtn.Click += new EventHandler(OnRun);
            Controls.Add(runBtn);
        }

        private void OnRun(object sender, EventArgs e)
        {
            var scope = _scopeSel.Checked
                ? Helpers.GetSelectedObjects()
                : Helpers.GetAllAssemblies(_model).Cast<ModelObject>().ToList();

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
                    try { pass = rule.Check(obj); } catch { }
                    if (pass) continue;

                    totalFails++;
                    if (!failsByRule.ContainsKey(rule.Name)) failsByRule[rule.Name] = 0;
                    failsByRule[rule.Name]++;
                    _lastFailures.Add(obj);

                    var a = obj as Assembly;
                    string mark = a != null
                        ? Helpers.GetAssemblyMark(a)
                        : Helpers.GetReport(obj, "ASSEMBLY_POS");

                    var item = new ListViewItem(mark);
                    item.SubItems.Add(Helpers.GetReport(obj, "PROFILE"));
                    item.SubItems.Add(Helpers.GetReport(obj, "PHASE"));
                    item.SubItems.Add(Helpers.GetUda(obj, Cfg.UDA_CRANE_PICK_ID));
                    item.SubItems.Add(rule.Name);
                    item.Tag = obj;
                    _results.Items.Add(item);
                }
            }

            var sb = new StringBuilder();
            sb.Append(totalFails + " failure(s) in " + scope.Count + " object(s)");
            if (failsByRule.Count > 0)
            {
                sb.Append("  ·  ");
                sb.Append(string.Join(", ", failsByRule
                    .Select(kv => kv.Key + ": " + kv.Value).ToArray()));
            }
            _summary.Text = sb.ToString();
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_results.Items.Count == 0) { MessageBox.Show("Nothing to export."); return; }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = "erection_validation_" + Helpers.Today() + ".csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using (var w = new StreamWriter(dlg.FileName))
                {
                    w.WriteLine("Mark,Profile,Phase,PickID,FailedRule");
                    foreach (ListViewItem item in _results.Items)
                    {
                        var cells = new[] { item.Text }.Concat(
                            item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                                .Skip(1).Select(s => s.Text)).ToArray();
                        w.WriteLine(string.Join(",", cells));
                    }
                }
                MessageBox.Show("Exported " + _results.Items.Count + " rows.");
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }
    }

    public class ErectionRule
    {
        public string Name;
        public string Description;
        public Predicate<ModelObject> Check;

        public ErectionRule(string n, string d, Predicate<ModelObject> c)
        {
            Name = n; Description = d; Check = c;
        }
    }
}
