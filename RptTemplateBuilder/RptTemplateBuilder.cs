// RptTemplateBuilder.cs
// Tekla Structures macro — .RPT Report Template Builder
// Drop into your macros folder and run via Tools > Macros > RptTemplateBuilder
//
// No live model connection is required. The tool generates a .rpt file
// that you copy into XS_FIRM\Reports\ or XS_PROJECT\Reports\.
//
// Tabs:
//   1. Settings  — template name, content type, sort order, row config
//   2. Filters   — include/exclude rules with attribute library sidebar
//   3. Fields    — output columns with attribute library sidebar
//   4. Preview   — generated .rpt source text
//   5. Help      — format reference and install instructions

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Tekla.Technology.Akit.UserScript
{
    // ════════════════════════════════════════════════════════════════════════
    //   ENTRY POINT
    // ════════════════════════════════════════════════════════════════════════
    public class Script
    {
        private static RptBuilderForm _form;

        public static void Run(IScript akit)
        {
            try
            {
                if (_form == null || _form.IsDisposed)
                {
                    _form = new RptBuilderForm();
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
                Trace.WriteLine("RptTemplateBuilder: " + ex);
                MessageBox.Show("Error: " + ex.Message, "RPT Template Builder",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //   DATA MODELS
    // ════════════════════════════════════════════════════════════════════════

    class FilterRule
    {
        public bool   Enabled   = true;
        public string Attribute = "NAME";
        public string Op        = "==";
        public string Value     = "";
        public string Action    = "exclude"; // exclude | include
    }

    class FieldDef
    {
        public string Name          = "Field";
        public string Attribute     = "NAME";
        public string FormulaMode   = "plain"; // plain | length_smart | weight_smart | custom
        public string CustomFormula = "";
        public string Datatype      = "STRING";
        public int    Length        = 20;
        public int    Decimals      = 0;
        public string Justify       = "LEFT";
        public string SortDir       = "NONE";
        public string OnCombine     = "NONE";
        public string FieldClass    = "";
        public string Unit          = "";
        public string FontName      = "Arial";
        public int    FontSize      = 5;
        public int    FontColor     = 0;
        public int    FontType      = 2;
        public double FontRatio     = 1.0;
        public bool   Visible       = true;
        public bool   AddSeparator  = false;
        public string SeparatorChar = ";";
    }

    // ════════════════════════════════════════════════════════════════════════
    //   ATTRIBUTE LIBRARY
    // ════════════════════════════════════════════════════════════════════════

    static class AttrLib
    {
        public struct Entry
        {
            public readonly string Cat, Name, Type;
            public Entry(string c, string n, string t) { Cat = c; Name = n; Type = t; }
            public override string ToString() { return Name + "  [" + Type + "]"; }
        }

        public static readonly Entry[] All =
        {
            // Identity & Position
            new Entry("Identity & Position", "NAME",            "STRING"),
            new Entry("Identity & Position", "ASSEMBLY_POS",    "STRING"),
            new Entry("Identity & Position", "PART_POS",        "STRING"),
            new Entry("Identity & Position", "PRELIM_MARK",     "STRING"),
            new Entry("Identity & Position", "CAST_UNIT_POS",   "STRING"),
            new Entry("Identity & Position", "PHASE",           "INTEGER"),
            new Entry("Identity & Position", "CLASS",           "STRING"),

            // Profile & Material
            new Entry("Profile & Material",  "PROFILE",         "STRING"),
            new Entry("Profile & Material",  "MATERIAL",        "STRING"),
            new Entry("Profile & Material",  "GRADE",           "STRING"),
            new Entry("Profile & Material",  "FINISH",          "STRING"),
            new Entry("Profile & Material",  "PRODUCT_NAME",    "STRING"),

            // Geometry
            new Entry("Geometry",            "LENGTH",          "DOUBLE"),
            new Entry("Geometry",            "WIDTH",           "DOUBLE"),
            new Entry("Geometry",            "HEIGHT",          "DOUBLE"),
            new Entry("Geometry",            "WEIGHT",          "DOUBLE"),
            new Entry("Geometry",            "VOLUME",          "DOUBLE"),
            new Entry("Geometry",            "AREA",            "DOUBLE"),
            new Entry("Geometry",            "NUMBER",          "INTEGER"),

            // Phase & Sequence
            new Entry("Phase & Sequence",    "SEQUENCE",        "INTEGER"),
            new Entry("Phase & Sequence",    "SEQUENCE_NAME",   "STRING"),
            new Entry("Phase & Sequence",    "LOT_NAME",        "STRING"),
            new Entry("Phase & Sequence",    "ERECTION_SEQ_NO", "STRING"),
            new Entry("Phase & Sequence",    "ZONE",            "STRING"),

            // Procurement
            new Entry("Procurement",         "BOUGHT_ITEM",     "STRING"),
            new Entry("Procurement",         "BOUGHT_ITEM_NAME","STRING"),
            new Entry("Procurement",         "HEAT_NUMBER",     "STRING"),
            new Entry("Procurement",         "BATCH_NUMBER",    "STRING"),
            new Entry("Procurement",         "LOAD_NUMBER",     "STRING"),
            new Entry("Procurement",         "BARCODEMARK",     "STRING"),

            // Fabrication
            new Entry("Fabrication",         "FAB_NAME",        "STRING"),
            new Entry("Fabrication",         "ASSEM_INST_STAT", "STRING"),
            new Entry("Fabrication",         "ASSEM_HOLD_STAT", "STRING"),
            new Entry("Fabrication",         "DRAWING_STATUS",  "STRING"),
            new Entry("Fabrication",         "EXISTING",        "STRING"),

            // SS_ Fields
            new Entry("SS_ Fields",          "SS_CVN",          "STRING"),
            new Entry("SS_ Fields",          "SS_BOUGHT_ITEM",  "STRING"),
            new Entry("SS_ Fields",          "SS_COMMENT",      "STRING"),
            new Entry("SS_ Fields",          "SS_PAINT",        "STRING"),
            new Entry("SS_ Fields",          "SS_CLEAN",        "STRING"),
            new Entry("SS_ Fields",          "SS_CAMBER_ROLL",  "STRING"),
            new Entry("SS_ Fields",          "SS_LOT",          "STRING"),
            new Entry("SS_ Fields",          "SS_FACILITY",     "STRING"),
            new Entry("SS_ Fields",          "SS_E_DWG",        "STRING"),
            new Entry("SS_ Fields",          "SS_D_DWG",        "STRING"),

            // Notes & Misc
            new Entry("Notes & Misc",        "NOTES2",          "STRING"),
            new Entry("Notes & Misc",        "NOTES3",          "STRING"),
            new Entry("Notes & Misc",        "NOTES4",          "STRING"),
            new Entry("Notes & Misc",        "NOTES5",          "STRING"),
        };

        public static IEnumerable<string> Categories()
        {
            return All.Select(a => a.Cat).Distinct();
        }

        public static IEnumerable<Entry> ForCat(string cat)
        {
            return All.Where(a => a.Cat == cat);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //   MAIN FORM
    // ════════════════════════════════════════════════════════════════════════

    public class RptBuilderForm : Form
    {
        // ── template-level settings ──────────────────────────────────────
        string _templateName  = "My_Report";
        string _internalName  = "MY_REPORT";
        string _type          = "TEXTUAL";
        int    _width         = 130;
        int    _maxHeight     = 50;
        string _contentType   = "PART";
        string _rowName       = "row";
        int    _rowHeight     = 5;
        bool   _useColumns    = true;
        string _sortType      = "NONE";
        string _filterLogic   = "OR";
        string _separatorChar = ";";
        string _notes         = "";

        readonly List<FilterRule> _filters = new List<FilterRule>();
        readonly List<FieldDef>   _fields  = new List<FieldDef>();

        // controls that need cross-method access
        DataGridView _filterGrid;
        ListBox      _fieldList;
        Panel        _fieldDetailHost;
        RichTextBox  _previewBox;

        // settings-tab controls that need reading back on Save/Preview
        TextBox        _setTemplateName;
        TextBox        _setInternalName;
        ComboBox       _setContentType;
        ComboBox       _setSortType;
        NumericUpDown  _setWidth;
        NumericUpDown  _setMaxHeight;
        NumericUpDown  _setRowHeight;
        TextBox        _setRowName;
        CheckBox       _setUseColumns;
        ComboBox       _setFilterLogic;
        TextBox        _setSeparatorChar;
        TextBox        _setNotes;

        public RptBuilderForm()
        {
            Text            = "Tekla .RPT Template Builder";
            Size            = new Size(980, 700);
            MinimumSize     = new Size(820, 560);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            BuildUI();
        }

        // ── top-level chrome ────────────────────────────────────────────

        void BuildUI()
        {
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 36,
                BackColor = SystemColors.ControlDark,
            };

            var lblTitle = new Label
            {
                Text      = "Tekla .RPT Template Builder",
                Font      = new Font("Tahoma", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(8, 10),
            };

            var btnSave = new Button
            {
                Text     = "Save .RPT...",
                Location = new Point(760, 6),
                Size     = new Size(96, 24),
                Font     = new Font("Tahoma", 8, FontStyle.Bold),
            };
            btnSave.Click += (s, e) => SaveRpt();

            var btnPresets = new Button
            {
                Text     = "Load Preset ▾",
                Location = new Point(658, 6),
                Size     = new Size(96, 24),
            };
            btnPresets.Click += (s, e) => ShowPresetsMenu(btnPresets);

            toolbar.Controls.AddRange(new Control[] { lblTitle, btnPresets, btnSave });

            var tabs = new TabControl { Dock = DockStyle.Fill };

            var tp1 = new TabPage("  Settings  ");  BuildSettingsTab(tp1);
            var tp2 = new TabPage("  Filters  ");   BuildFiltersTab(tp2);
            var tp3 = new TabPage("  Fields  ");    BuildFieldsTab(tp3);
            var tp4 = new TabPage("  Preview  ");   BuildPreviewTab(tp4);
            var tp5 = new TabPage("  Help  ");      BuildHelpTab(tp5);
            tabs.TabPages.AddRange(new[] { tp1, tp2, tp3, tp4, tp5 });
            tabs.SelectedIndexChanged += (s, e) =>
            {
                if (tabs.SelectedIndex == 3) RefreshPreview();
            };

            Controls.Add(tabs);
            Controls.Add(toolbar);
        }

        // ════════════════════════════════════════════════════════════════
        //   TAB 1 — SETTINGS
        // ════════════════════════════════════════════════════════════════

        void BuildSettingsTab(TabPage tp)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            int y = 12;

            // ── local helpers ────────────────────────────────────────────
            Label Lbl(string t, int yy)
            {
                return new Label
                {
                    Text      = t,
                    Location  = new Point(16, yy + 3),
                    Size      = new Size(152, 18),
                    TextAlign = ContentAlignment.MiddleRight,
                };
            }

            void AddRow(string label, Control ctl, int ctlW = 200)
            {
                ctl.Location = new Point(172, y);
                ctl.Width    = ctlW;
                scroll.Controls.Add(Lbl(label, y));
                scroll.Controls.Add(ctl);
                y += Math.Max(ctl.Height, 20) + 6;
            }

            void Sep()
            {
                scroll.Controls.Add(new Label
                {
                    BorderStyle = BorderStyle.Fixed3D,
                    Location    = new Point(16, y),
                    Size        = new Size(500, 2),
                });
                y += 10;
            }

            // ── identification ───────────────────────────────────────────
            scroll.Controls.Add(SectionLabel("Template identification", 8, y));
            y += 22;

            _setTemplateName = new TextBox { Text = _templateName };
            _setTemplateName.TextChanged += (s, e) => _templateName = _setTemplateName.Text;
            AddRow("Template name:", _setTemplateName);

            _setInternalName = new TextBox { Text = _internalName };
            _setInternalName.TextChanged += (s, e) => _internalName = _setInternalName.Text;
            AddRow("Internal name (no spaces):", _setInternalName);

            _setContentType = Combo(new[] { "PART", "ASSEMBLY", "BOLT", "WELD", "REINFORCEMENT" }, _contentType);
            _setContentType.SelectedIndexChanged += (s, e) => _contentType = _setContentType.SelectedItem?.ToString() ?? "PART";
            AddRow("Content type:", _setContentType, 140);

            Sep();

            // ── sizing ───────────────────────────────────────────────────
            scroll.Controls.Add(SectionLabel("Sizing", 8, y));
            y += 22;

            _setWidth = Nud(10, 9999, _width);
            _setWidth.ValueChanged += (s, e) => _width = (int)_setWidth.Value;
            AddRow("Template width (mm):", _setWidth, 80);

            _setMaxHeight = Nud(1, 9999, _maxHeight);
            _setMaxHeight.ValueChanged += (s, e) => _maxHeight = (int)_setMaxHeight.Value;
            AddRow("Max height (mm):", _setMaxHeight, 80);

            _setRowHeight = Nud(1, 999, _rowHeight);
            _setRowHeight.ValueChanged += (s, e) => _rowHeight = (int)_setRowHeight.Value;
            AddRow("Row height:", _setRowHeight, 80);

            _setRowName = new TextBox { Text = _rowName };
            _setRowName.TextChanged += (s, e) => _rowName = _setRowName.Text;
            AddRow("Row name:", _setRowName, 100);

            _setUseColumns = new CheckBox { Text = "usecolumns = 1", Checked = _useColumns, AutoSize = true };
            _setUseColumns.CheckedChanged += (s, e) => _useColumns = _setUseColumns.Checked;
            _setUseColumns.Location = new Point(172, y);
            scroll.Controls.Add(_setUseColumns);
            y += 26;

            Sep();

            // ── output / filter behavior ─────────────────────────────────
            scroll.Controls.Add(SectionLabel("Output / filter behavior", 8, y));
            y += 22;

            _setSortType = Combo(new[] { "NONE", "ASCENDING", "DESCENDING", "COMBINE" }, _sortType);
            _setSortType.SelectedIndexChanged += (s, e) => _sortType = _setSortType.SelectedItem?.ToString() ?? "NONE";
            AddRow("Sort type:", _setSortType, 140);

            _setFilterLogic = Combo(new[] { "OR", "AND" }, _filterLogic);
            _setFilterLogic.SelectedIndexChanged += (s, e) => _filterLogic = _setFilterLogic.SelectedItem?.ToString() ?? "OR";
            AddRow("Combine filter rules with:", _setFilterLogic, 80);

            _setSeparatorChar = new TextBox { Text = _separatorChar, Width = 40 };
            _setSeparatorChar.TextChanged += (s, e) => _separatorChar = _setSeparatorChar.Text;
            AddRow("Default separator char:", _setSeparatorChar, 40);

            Sep();

            // ── notes ────────────────────────────────────────────────────
            _setNotes = new TextBox
            {
                Text        = _notes,
                Multiline   = true,
                Height      = 60,
                ScrollBars  = ScrollBars.Vertical,
            };
            _setNotes.TextChanged += (s, e) => _notes = _setNotes.Text;
            AddRow("Notes:", _setNotes, 340);

            tp.Controls.Add(scroll);
        }

        // ════════════════════════════════════════════════════════════════
        //   TAB 2 — FILTERS
        // ════════════════════════════════════════════════════════════════

        void BuildFiltersTab(TabPage tp)
        {
            // attribute library sidebar (right)
            var attrPanel  = new Panel { Dock = DockStyle.Right, Width = 196, BorderStyle = BorderStyle.Fixed3D };
            var attrHeader = new Label
            {
                Text      = "Attribute Library",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Tahoma", 8, FontStyle.Bold),
                BackColor = SystemColors.ControlDark,
                ForeColor = Color.White,
            };
            var attrTree = MakeAttrTree();
            attrTree.DoubleClick += (s, e) =>
            {
                if (attrTree.SelectedNode?.Tag is string attr)
                    SetFilterRowAttribute(attr);
            };
            attrPanel.Controls.Add(attrTree);
            attrPanel.Controls.Add(attrHeader);

            // button bar (top)
            var btnBar = new Panel { Dock = DockStyle.Top, Height = 32 };
            var btnAdd = Btn("Add Rule",   4,  4, 80);
            var btnDel = Btn("Remove",    90,  4, 68);
            var btnUp  = Btn("↑",   166,  4, 28);
            var btnDn  = Btn("↓",   198,  4, 28);
            var lblOp  = new Label { Text = "Combine with:", Location = new Point(238, 8), AutoSize = true };
            var cboOp  = Combo(new[] { "OR", "AND" }, _filterLogic, 64);
            cboOp.Location = new Point(322, 5);
            cboOp.SelectedIndexChanged += (s, e) => _filterLogic = cboOp.SelectedItem?.ToString() ?? "OR";
            btnBar.Controls.AddRange(new Control[] { btnAdd, btnDel, btnUp, btnDn, lblOp, cboOp });

            btnAdd.Click += (s, e) => { _filters.Add(new FilterRule()); AppendFilterRow(_filters.Last()); };
            btnDel.Click += (s, e) => RemoveSelectedFilter();
            btnUp.Click  += (s, e) => MoveFilter(-1);
            btnDn.Click  += (s, e) => MoveFilter(+1);

            // filter grid
            _filterGrid = new DataGridView
            {
                Dock                     = DockStyle.Fill,
                RowHeadersVisible        = false,
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                AutoSizeColumnsMode      = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect              = false,
                Font                     = new Font("Consolas", 8),
                EditMode                 = DataGridViewEditMode.EditOnEnter,
                BackgroundColor          = SystemColors.Window,
                BorderStyle              = BorderStyle.None,
            };

            var colOn   = new DataGridViewCheckBoxColumn { HeaderText = "On",        Width = 32,  Name = "On" };
            var colAttr = new DataGridViewTextBoxColumn  { HeaderText = "Attribute",  Width = 180, Name = "Attr" };
            var colOp   = new DataGridViewComboBoxColumn { HeaderText = "Operator",   Width = 80,  Name = "Op" };
            colOp.Items.AddRange("==", "!=", "match", "!match", ">", "<");
            var colVal  = new DataGridViewTextBoxColumn  { HeaderText = "Value",      Width = 200, Name = "Val" };
            var colAct  = new DataGridViewComboBoxColumn { HeaderText = "Action",     Width = 84,  Name = "Act" };
            colAct.Items.AddRange("exclude", "include");

            _filterGrid.Columns.AddRange(colOn, colAttr, colOp, colVal, colAct);

            _filterGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_filterGrid.IsCurrentCellDirty)
                    _filterGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _filterGrid.CellValueChanged += OnFilterCellChanged;

            var gridWrap = new Panel { Dock = DockStyle.Fill };
            gridWrap.Controls.Add(_filterGrid);
            gridWrap.Controls.Add(btnBar);

            tp.Controls.Add(gridWrap);
            tp.Controls.Add(attrPanel);
        }

        void AppendFilterRow(FilterRule r)
        {
            int i = _filterGrid.Rows.Add();
            var row = _filterGrid.Rows[i];
            row.Cells["On"].Value   = r.Enabled;
            row.Cells["Attr"].Value = r.Attribute;
            row.Cells["Op"].Value   = r.Op;
            row.Cells["Val"].Value  = r.Value;
            row.Cells["Act"].Value  = r.Action;
            _filterGrid.ClearSelection();
            _filterGrid.Rows[i].Selected = true;
        }

        void OnFilterCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filters.Count) return;
            var row = _filterGrid.Rows[e.RowIndex];
            var f   = _filters[e.RowIndex];
            if (row.Cells["On"].Value is bool b)   f.Enabled   = b;
            f.Attribute = row.Cells["Attr"].Value?.ToString() ?? f.Attribute;
            f.Op        = row.Cells["Op"].Value?.ToString()   ?? f.Op;
            f.Value     = row.Cells["Val"].Value?.ToString()  ?? f.Value;
            f.Action    = row.Cells["Act"].Value?.ToString()  ?? f.Action;
        }

        void RemoveSelectedFilter()
        {
            if (_filterGrid.SelectedRows.Count == 0) return;
            int i = _filterGrid.SelectedRows[0].Index;
            if (i >= _filters.Count) return;
            _filters.RemoveAt(i);
            _filterGrid.Rows.RemoveAt(i);
        }

        void MoveFilter(int dir)
        {
            if (_filterGrid.SelectedRows.Count == 0) return;
            int i = _filterGrid.SelectedRows[0].Index;
            int j = i + dir;
            if (j < 0 || j >= _filters.Count) return;
            Swap(_filters, i, j);
            _filterGrid.Rows.Clear();
            foreach (var f in _filters) AppendFilterRow(f);
            _filterGrid.Rows[j].Selected = true;
        }

        void SetFilterRowAttribute(string attr)
        {
            if (_filterGrid.SelectedRows.Count > 0)
                _filterGrid.SelectedRows[0].Cells["Attr"].Value = attr;
        }

        void RebuildFilterGrid()
        {
            if (_filterGrid == null) return;
            _filterGrid.Rows.Clear();
            foreach (var f in _filters) AppendFilterRow(f);
        }

        // ════════════════════════════════════════════════════════════════
        //   TAB 3 — FIELDS
        // ════════════════════════════════════════════════════════════════

        void BuildFieldsTab(TabPage tp)
        {
            // attribute library sidebar (right)
            var attrPanel  = new Panel { Dock = DockStyle.Right, Width = 196, BorderStyle = BorderStyle.Fixed3D };
            var attrHeader = new Label
            {
                Text      = "Attribute Library",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Tahoma", 8, FontStyle.Bold),
                BackColor = SystemColors.ControlDark,
                ForeColor = Color.White,
            };
            var attrTree = MakeAttrTree();
            attrTree.DoubleClick += (s, e) =>
            {
                if (attrTree.SelectedNode?.Tag is string attr)
                    ApplyAttrToSelectedField(attr);
            };
            attrPanel.Controls.Add(attrTree);
            attrPanel.Controls.Add(attrHeader);

            // left: field list
            var leftPanel = new Panel { Dock = DockStyle.Left, Width = 180, BorderStyle = BorderStyle.Fixed3D };

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 32 };
            var btnAdd = Btn("Add",    2, 4, 48);
            var btnDel = Btn("Del",   54, 4, 42);
            var btnUp  = Btn("↑", 100, 4, 28);
            var btnDn  = Btn("↓", 132, 4, 28);
            btnBar.Controls.AddRange(new Control[] { btnAdd, btnDel, btnUp, btnDn });

            _fieldList = new ListBox
            {
                Dock      = DockStyle.Fill,
                Font      = new Font("Consolas", 8),
                BorderStyle = BorderStyle.None,
            };
            _fieldList.SelectedIndexChanged += (s, e) => RefreshFieldDetail();

            leftPanel.Controls.Add(_fieldList);
            leftPanel.Controls.Add(btnBar);

            btnAdd.Click += (s, e) => AddField();
            btnDel.Click += (s, e) => RemoveSelectedField();
            btnUp.Click  += (s, e) => MoveField(-1);
            btnDn.Click  += (s, e) => MoveField(+1);

            // centre: field detail panel
            _fieldDetailHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            ShowFieldDetailPlaceholder();

            tp.Controls.Add(_fieldDetailHost);
            tp.Controls.Add(attrPanel);
            tp.Controls.Add(leftPanel);
        }

        void ShowFieldDetailPlaceholder()
        {
            _fieldDetailHost.Controls.Clear();
            _fieldDetailHost.Controls.Add(new Label
            {
                Text      = "Select a field from the list to edit its properties.\nDouble-click an attribute in the library to apply it to the selected field.",
                Dock      = DockStyle.Top,
                Height    = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Tahoma", 8),
                ForeColor = SystemColors.GrayText,
                Padding   = new Padding(8),
            });
        }

        void AddField()
        {
            var f = new FieldDef { Name = "Field" + (_fields.Count + 1) };
            _fields.Add(f);
            _fieldList.Items.Add(f.Name);
            _fieldList.SelectedIndex = _fields.Count - 1;
        }

        void RemoveSelectedField()
        {
            int i = _fieldList.SelectedIndex;
            if (i < 0) return;
            _fields.RemoveAt(i);
            _fieldList.Items.RemoveAt(i);
            if (_fieldList.Items.Count > 0)
                _fieldList.SelectedIndex = Math.Min(i, _fieldList.Items.Count - 1);
            else
                ShowFieldDetailPlaceholder();
        }

        void MoveField(int dir)
        {
            int i = _fieldList.SelectedIndex;
            if (i < 0) return;
            int j = i + dir;
            if (j < 0 || j >= _fields.Count) return;
            Swap(_fields, i, j);
            _fieldList.Items[i] = _fields[i].Name;
            _fieldList.Items[j] = _fields[j].Name;
            _fieldList.SelectedIndex = j;
        }

        void ApplyAttrToSelectedField(string attr)
        {
            int i = _fieldList.SelectedIndex;
            if (i < 0) return;
            _fields[i].Attribute = attr;
            RefreshFieldDetail();
        }

        void RefreshFieldDetail()
        {
            int i = _fieldList.SelectedIndex;
            if (i < 0 || i >= _fields.Count) { ShowFieldDetailPlaceholder(); return; }
            BuildFieldDetailPanel(_fields[i], i);
        }

        void BuildFieldDetailPanel(FieldDef f, int fieldIdx)
        {
            _fieldDetailHost.SuspendLayout();
            _fieldDetailHost.Controls.Clear();

            var p  = new Panel { AutoSize = true, Padding = new Padding(8) };
            int y  = 8;

            Label Lbl(string t, int yy)
            {
                return new Label
                {
                    Text      = t,
                    Location  = new Point(4, yy + 3),
                    Size      = new Size(136, 18),
                    TextAlign = ContentAlignment.MiddleRight,
                };
            }

            void Row(string lbl, Control ctl, int w = 180)
            {
                ctl.Location = new Point(144, y);
                ctl.Width    = w;
                p.Controls.Add(Lbl(lbl, y));
                p.Controls.Add(ctl);
                y += Math.Max(ctl.Height, 20) + 5;
            }

            void HSep()
            {
                p.Controls.Add(new Label
                {
                    BorderStyle = BorderStyle.Fixed3D,
                    Location    = new Point(4, y),
                    Size        = new Size(400, 2),
                });
                y += 8;
            }

            // ── identity ─────────────────────────────────────────────────
            p.Controls.Add(SectionLabel("Identity", 4, y)); y += 20;

            var txtName = new TextBox { Text = f.Name };
            txtName.TextChanged += (s, e) =>
            {
                f.Name = txtName.Text;
                if (fieldIdx < _fieldList.Items.Count)
                    _fieldList.Items[fieldIdx] = f.Name;
            };
            Row("Display name:", txtName);

            var txtAttr = new TextBox { Text = f.Attribute };
            txtAttr.TextChanged += (s, e) => f.Attribute = txtAttr.Text;
            Row("Attribute:", txtAttr);

            // ── formula ───────────────────────────────────────────────────
            HSep();
            p.Controls.Add(SectionLabel("Formula", 4, y)); y += 20;

            var cboMode = Combo(new[] { "plain", "length_smart", "weight_smart", "custom" }, f.FormulaMode);
            cboMode.SelectedIndexChanged += (s, e) => f.FormulaMode = cboMode.SelectedItem?.ToString() ?? "plain";
            Row("Formula mode:", cboMode, 140);

            var txtFormula = new TextBox
            {
                Text       = f.CustomFormula,
                Multiline  = true,
                Height     = 64,
                ScrollBars = ScrollBars.Both,
                Font       = new Font("Consolas", 8),
                WordWrap   = false,
            };
            txtFormula.TextChanged += (s, e) => f.CustomFormula = txtFormula.Text;
            Row("Custom formula:", txtFormula, 300);

            // ── data format ───────────────────────────────────────────────
            HSep();
            p.Controls.Add(SectionLabel("Data format", 4, y)); y += 20;

            var cboType = Combo(new[] { "STRING", "INTEGER", "DOUBLE" }, f.Datatype);
            cboType.SelectedIndexChanged += (s, e) => f.Datatype = cboType.SelectedItem?.ToString() ?? "STRING";
            Row("Datatype:", cboType, 110);

            var nudLen = Nud(1, 999, f.Length);
            nudLen.ValueChanged += (s, e) => f.Length = (int)nudLen.Value;
            Row("Length (chars):", nudLen, 70);

            var nudDec = Nud(0, 10, f.Decimals);
            nudDec.ValueChanged += (s, e) => f.Decimals = (int)nudDec.Value;
            Row("Decimals:", nudDec, 70);

            var cboJust = Combo(new[] { "LEFT", "CENTER", "RIGHT" }, f.Justify);
            cboJust.SelectedIndexChanged += (s, e) => f.Justify = cboJust.SelectedItem?.ToString() ?? "LEFT";
            Row("Justify:", cboJust, 110);

            var cboSortDir = Combo(new[] { "NONE", "ASCENDING", "DESCENDING" }, f.SortDir);
            cboSortDir.SelectedIndexChanged += (s, e) => f.SortDir = cboSortDir.SelectedItem?.ToString() ?? "NONE";
            Row("Sort direction:", cboSortDir, 130);

            var cboCombine = Combo(new[] { "NONE", "SUM", "AVERAGE", "MIN", "MAX" }, f.OnCombine);
            cboCombine.SelectedIndexChanged += (s, e) => f.OnCombine = cboCombine.SelectedItem?.ToString() ?? "NONE";
            Row("On combine:", cboCombine, 110);

            // ── units ─────────────────────────────────────────────────────
            HSep();
            p.Controls.Add(SectionLabel("Unit / class", 4, y)); y += 20;

            var cboClass = Combo(new[] { "", "Length", "Weight", "Area" }, f.FieldClass);
            cboClass.SelectedIndexChanged += (s, e) => f.FieldClass = cboClass.SelectedItem?.ToString() ?? "";
            Row("Class:", cboClass, 110);

            var cboUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var u in new[] { "", "mm", "m", "ft", "in", "kg", "lb", "lbf", "m2" })
                cboUnit.Items.Add(u);
            if (!cboUnit.Items.Contains(f.Unit)) cboUnit.Items.Add(f.Unit);
            cboUnit.SelectedItem = f.Unit;
            cboUnit.SelectedIndexChanged += (s, e) => f.Unit = cboUnit.SelectedItem?.ToString() ?? "";
            Row("Unit:", cboUnit, 110);

            // ── font ──────────────────────────────────────────────────────
            HSep();
            p.Controls.Add(SectionLabel("Font", 4, y)); y += 20;

            var txtFont = new TextBox { Text = f.FontName };
            txtFont.TextChanged += (s, e) => f.FontName = txtFont.Text;
            Row("Font name:", txtFont, 140);

            var nudFS = Nud(1, 200, f.FontSize);
            nudFS.ValueChanged += (s, e) => f.FontSize = (int)nudFS.Value;
            Row("Font size:", nudFS, 70);

            var nudFC = Nud(0, 9999, f.FontColor);
            nudFC.ValueChanged += (s, e) => f.FontColor = (int)nudFC.Value;
            Row("Font color (int):", nudFC, 80);

            var nudFT = Nud(0, 99, f.FontType);
            nudFT.ValueChanged += (s, e) => f.FontType = (int)nudFT.Value;
            Row("Font type:", nudFT, 70);

            var nudFR = new NumericUpDown
            {
                Minimum = 0.1m, Maximum = 10m, Value = (decimal)f.FontRatio,
                DecimalPlaces = 2, Increment = 0.1m,
            };
            nudFR.ValueChanged += (s, e) => f.FontRatio = (double)nudFR.Value;
            Row("Font ratio:", nudFR, 80);

            // ── misc ──────────────────────────────────────────────────────
            HSep();
            p.Controls.Add(SectionLabel("Misc", 4, y)); y += 20;

            var chkVis = new CheckBox { Text = "Visible", Checked = f.Visible, AutoSize = true, Location = new Point(144, y) };
            chkVis.CheckedChanged += (s, e) => f.Visible = chkVis.Checked;
            p.Controls.Add(chkVis);
            y += 26;

            var chkSep = new CheckBox { Text = "Append separator after this field", Checked = f.AddSeparator, AutoSize = true, Location = new Point(144, y) };
            chkSep.CheckedChanged += (s, e) => f.AddSeparator = chkSep.Checked;
            p.Controls.Add(chkSep);
            y += 26;

            var txtSepChar = new TextBox { Text = f.SeparatorChar, Width = 40, Location = new Point(144, y) };
            txtSepChar.TextChanged += (s, e) => f.SeparatorChar = txtSepChar.Text;
            p.Controls.Add(Lbl("Separator char:", y));
            p.Controls.Add(txtSepChar);
            y += 30;

            // fix AutoSize height
            p.Size = new Size(_fieldDetailHost.ClientSize.Width - 20, y + 20);
            _fieldDetailHost.Controls.Add(p);
            _fieldDetailHost.ResumeLayout(true);
        }

        void RebuildFieldList()
        {
            if (_fieldList == null) return;
            _fieldList.Items.Clear();
            foreach (var f in _fields) _fieldList.Items.Add(f.Name);
            ShowFieldDetailPlaceholder();
        }

        // ════════════════════════════════════════════════════════════════
        //   TAB 4 — PREVIEW
        // ════════════════════════════════════════════════════════════════

        void BuildPreviewTab(TabPage tp)
        {
            var btnRefresh = new Button { Text = "Refresh", Dock = DockStyle.Top, Height = 28 };
            _previewBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                Font        = new Font("Consolas", 9),
                BackColor   = Color.White,
                ScrollBars  = RichTextBoxScrollBars.Both,
                WordWrap    = false,
                BorderStyle = BorderStyle.None,
            };
            btnRefresh.Click += (s, e) => RefreshPreview();

            tp.Controls.Add(_previewBox);
            tp.Controls.Add(btnRefresh);
        }

        void RefreshPreview()
        {
            if (_previewBox == null) return;
            try   { _previewBox.Text = GenerateRpt(); }
            catch (Exception ex) { _previewBox.Text = "// Error generating .rpt:\n// " + ex.Message; }
        }

        // ════════════════════════════════════════════════════════════════
        //   TAB 5 — HELP
        // ════════════════════════════════════════════════════════════════

        void BuildHelpTab(TabPage tp)
        {
            tp.Controls.Add(new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                Font        = new Font("Tahoma", 9),
                BackColor   = SystemColors.Window,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Text        = HELP_TEXT,
            });
        }

        // ════════════════════════════════════════════════════════════════
        //   PRESETS
        // ════════════════════════════════════════════════════════════════

        void ShowPresetsMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Material List  (DTI_Material_list_csv style)").Click += (s, e) => LoadMaterialListPreset();
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Clear all filters + fields").Click += (s, e) =>
            {
                if (MessageBox.Show("Clear all filters and fields?", "Clear", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { _filters.Clear(); _fields.Clear(); RebuildFilterGrid(); RebuildFieldList(); }
            };
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        void LoadMaterialListPreset()
        {
            if (MessageBox.Show(
                    "Replace the current template with the Material List preset?",
                    "Load Preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // settings
            _templateName  = "DTI_Material_list_csv";
            _internalName  = "DTI_MATERIAL_LIST_CSV";
            _width         = 130;
            _maxHeight     = 50;
            _contentType   = "PART";
            _sortType      = "COMBINE";
            _rowName       = "row";
            _rowHeight     = 5;
            _useColumns    = true;
            _filterLogic   = "OR";
            _separatorChar = ";";
            _notes         = "Material list — generated by RptTemplateBuilder";

            // sync settings-tab controls if they exist
            if (_setTemplateName  != null) _setTemplateName.Text              = _templateName;
            if (_setInternalName  != null) _setInternalName.Text              = _internalName;
            if (_setContentType   != null) _setContentType.SelectedItem       = _contentType;
            if (_setSortType      != null) _setSortType.SelectedItem          = _sortType;
            if (_setWidth         != null) _setWidth.Value                    = _width;
            if (_setMaxHeight     != null) _setMaxHeight.Value                = _maxHeight;
            if (_setRowHeight     != null) _setRowHeight.Value                = _rowHeight;
            if (_setRowName       != null) _setRowName.Text                   = _rowName;
            if (_setUseColumns    != null) _setUseColumns.Checked             = _useColumns;
            if (_setFilterLogic   != null) _setFilterLogic.SelectedItem       = _filterLogic;
            if (_setSeparatorChar != null) _setSeparatorChar.Text             = _separatorChar;
            if (_setNotes         != null) _setNotes.Text                     = _notes;

            // filters (from confirmed sample)
            _filters.Clear();
            _filters.Add(new FilterRule { Attribute = "MATERIAL", Op = "==",    Value = "CONCRETE", Action = "exclude" });
            _filters.Add(new FilterRule { Attribute = "MATERIAL", Op = "==",    Value = "GROUT",    Action = "exclude" });
            _filters.Add(new FilterRule { Attribute = "NAME",     Op = "match", Value = "JOIST*",   Action = "exclude" });
            _filters.Add(new FilterRule { Attribute = "EXISTING", Op = "==",    Value = "Yes",      Action = "exclude" });

            // fields (from confirmed sample)
            _fields.Clear();
            F("Quantity",   "NUMBER",       "INTEGER", 6,  0, "NONE",  "",       "",   "plain");
            F("Prelim_no",  "PRELIM_MARK",  "INTEGER", 10, 0, "NONE",  "",       "",   "plain");
            F("Profile",    "PROFILE",      "STRING",  20, 0, "NONE",  "",       "",   "plain");
            F("Grade",      "MATERIAL",     "STRING",  12, 0, "NONE",  "",       "",   "plain");
            F("Length",     "LENGTH",       "DOUBLE",  10, 0, "SUM",   "Length", "mm", "length_smart");
            F("Weight",     "WEIGHT",       "DOUBLE",  10, 0, "NONE",  "Weight", "",   "weight_smart");
            F("Phase",      "PHASE",        "INTEGER", 6,  0, "NONE",  "Weight", "kg", "plain");
            F("CVN",        "SS_CVN",       "STRING",  10, 0, "NONE",  "Area",   "m2", "plain");

            RebuildFilterGrid();
            RebuildFieldList();
            MessageBox.Show("Material list preset loaded.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);

            void F(string name, string attr, string dtype, int len, int dec, string onComb, string cls, string unit, string mode)
            {
                _fields.Add(new FieldDef
                {
                    Name        = name,
                    Attribute   = attr,
                    Datatype    = dtype,
                    Length      = len,
                    Decimals    = dec,
                    OnCombine   = onComb,
                    FieldClass  = cls,
                    Unit        = unit,
                    FormulaMode = mode,
                });
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   .RPT GENERATION
        // ════════════════════════════════════════════════════════════════

        string EscRpt(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\r\n", "\\n")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\n");
        }

        string BuildFormula(FieldDef f)
        {
            switch (f.FormulaMode)
            {
                case "length_smart":
                    return
                        "if GetValue(\"SS_BOUGHT_ITEM\") == \"Yes\" then\n" +
                        "  \"\"\n" +
                        "else\n" +
                        "  if GetValue(\"ADVANCED_OPTION.XS_IMPERIAL\") == \"TRUE\" then\n" +
                        "    if match(GetValue(\"PRODUCT_NAME\"),\"*GRTG*\") || match(GetValue(\"PRODUCT_NAME\"),\"*GRIP*\") then\n" +
                        "      format(GetValue(\"WIDTH\"),\"Length\",\"ft\",3)\n" +
                        "    else\n" +
                        "      format(GetValue(\"LENGTH\"),\"Length\",\"ft\",3)\n" +
                        "    endif\n" +
                        "  else\n" +
                        "    if match(GetValue(\"PRODUCT_NAME\"),\"*GRTG*\") || match(GetValue(\"PRODUCT_NAME\"),\"*GRIP*\") then\n" +
                        "      GetValue(\"WIDTH\")\n" +
                        "    else\n" +
                        "      GetValue(\"" + f.Attribute + "\")\n" +
                        "    endif\n" +
                        "  endif\n" +
                        "endif";

                case "weight_smart":
                    return
                        "if GetValue(\"ADVANCED_OPTION.XS_IMPERIAL\") == \"TRUE\" then\n" +
                        "  format(GetValue(\"WEIGHT\"),\"Weight\",\"lbf\",0)\n" +
                        "else\n" +
                        "  format(GetValue(\"WEIGHT\"),\"Weight\",\"kg\",0)\n" +
                        "endif";

                case "custom":
                    return string.IsNullOrWhiteSpace(f.CustomFormula)
                        ? "GetValue(\"" + f.Attribute + "\")"
                        : f.CustomFormula;

                default: // plain
                    return "GetValue(\"" + f.Attribute + "\")";
            }
        }

        string BuildFilterExpression()
        {
            var enabled  = _filters.Where(f => f.Enabled).ToList();
            if (!enabled.Any()) return "";

            var excludes = enabled.Where(f => f.Action == "exclude").ToList();
            var includes = enabled.Where(f => f.Action == "include").ToList();
            string join  = _filterLogic == "AND" ? " && " : " || ";
            var sb       = new StringBuilder();

            string Cond(FilterRule r)
            {
                string gv = "GetValue(\"" + r.Attribute + "\")";
                switch (r.Op)
                {
                    case "match":  return "match("  + gv + ", \"" + EscRpt(r.Value) + "\")";
                    case "!match": return "!match(" + gv + ", \"" + EscRpt(r.Value) + "\")";
                    default:       return gv + " " + r.Op + " \"" + EscRpt(r.Value) + "\"";
                }
            }

            if (excludes.Any())
            {
                sb.Append("if ").AppendLine(string.Join(join, excludes.Select(Cond)) + " then");
                sb.AppendLine("  StepOver()");
                if (includes.Any())
                {
                    sb.Append("elseif ").AppendLine(string.Join(join, includes.Select(Cond)) + " then");
                    sb.AppendLine("  Output()");
                }
                else
                {
                    sb.AppendLine("else");
                    sb.AppendLine("  Output()");
                }
                sb.Append("endif");
            }
            else if (includes.Any())
            {
                sb.Append("if ").AppendLine(string.Join(join, includes.Select(Cond)) + " then");
                sb.AppendLine("  Output()");
                sb.AppendLine("else");
                sb.AppendLine("  StepOver()");
                sb.Append("endif");
            }

            return sb.ToString();
        }

        string GenerateRpt()
        {
            var sb = new StringBuilder();

            sb.AppendLine("version=3.5");
            sb.AppendLine();
            sb.AppendLine("template_name=\"" + EscRpt(_internalName) + "\"");
            sb.AppendLine("name=\""          + EscRpt(_templateName) + "\"");
            sb.AppendLine("type="            + _type);
            sb.AppendLine("contenttype=\""   + EscRpt(_contentType) + "\"");
            sb.AppendLine("width="           + _width    + ".000000");
            sb.AppendLine("maxheight="       + _maxHeight + ".000000");
            sb.AppendLine();
            sb.AppendLine("part_count=1");
            sb.AppendLine();
            sb.AppendLine("part_name_0=\"" + EscRpt(_rowName) + "\"");
            sb.AppendLine("row_height_0="  + _rowHeight + ".000000");
            sb.AppendLine("usecolumns_0="  + (_useColumns ? "1" : "0"));
            sb.AppendLine("sorttype_0="    + _sortType);
            sb.AppendLine();
            sb.AppendLine("field_count_0=" + _fields.Count);
            sb.AppendLine();

            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                sb.AppendLine("field_name_0_"    + i + "=\"" + EscRpt(f.Name)       + "\"");
                sb.AppendLine("formula_0_"       + i + "=\"" + EscRpt(BuildFormula(f)) + "\"");
                sb.AppendLine("datatype_0_"      + i + "="  + f.Datatype);
                sb.AppendLine("length_0_"        + i + "="  + f.Length);
                sb.AppendLine("decimal_0_"       + i + "="  + f.Decimals);
                sb.AppendLine("justify_0_"       + i + "="  + f.Justify);
                sb.AppendLine("class_0_"         + i + "=\"" + EscRpt(f.FieldClass) + "\"");
                sb.AppendLine("unit_0_"          + i + "=\"" + EscRpt(f.Unit)       + "\"");
                sb.AppendLine("sortdirection_0_" + i + "="  + f.SortDir);
                sb.AppendLine("oncombine_0_"     + i + "="  + f.OnCombine);
                sb.AppendLine("fontname_0_"      + i + "=\"" + EscRpt(f.FontName)   + "\"");
                sb.AppendLine("fontsize_0_"      + i + "="  + f.FontSize + ".000000");
                sb.AppendLine("fontcolor_0_"     + i + "="  + f.FontColor);
                sb.AppendLine("fonttype_0_"      + i + "="  + f.FontType);
                sb.AppendLine("fontratio_0_"     + i + "="  + f.FontRatio.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("visible_0_"       + i + "="  + (f.Visible ? "1" : "0"));
                sb.AppendLine("pen_0_"           + i + "=-1");
                if (f.AddSeparator)
                    sb.AppendLine("separator_0_" + i + "=\"" + EscRpt(f.SeparatorChar) + "\"");
                sb.AppendLine();
            }

            string filter = BuildFilterExpression();
            if (!string.IsNullOrEmpty(filter))
                sb.AppendLine("rule=\"" + EscRpt(filter) + "\"");

            if (!string.IsNullOrEmpty(_notes))
                sb.AppendLine("notes=\"" + EscRpt(_notes) + "\"");

            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════
        //   SAVE
        // ════════════════════════════════════════════════════════════════

        void SaveRpt()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title       = "Save .RPT Template";
                dlg.Filter      = "Tekla Report Template (*.rpt)|*.rpt|All files (*.*)|*.*";
                dlg.FileName    = _templateName + ".rpt";
                dlg.DefaultExt  = "rpt";

                string teklaDir = TryFindReportsFolder();
                if (!string.IsNullOrEmpty(teklaDir) && Directory.Exists(teklaDir))
                    dlg.InitialDirectory = teklaDir;

                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    File.WriteAllText(dlg.FileName, GenerateRpt(), new UTF8Encoding(false));
                    MessageBox.Show(
                        "Saved to:\n" + dlg.FileName +
                        "\n\nCopy to XS_FIRM\\Reports\\ or XS_PROJECT\\Reports\\ to use in Tekla.",
                        "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Save failed: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        static string TryFindReportsFolder()
        {
            try
            {
                string firm = "";
                Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_FIRM", ref firm);
                if (!string.IsNullOrEmpty(firm))
                {
                    string dir = Path.Combine(firm, "Reports");
                    if (Directory.Exists(dir)) return dir;
                }
            }
            catch { }
            return null;
        }

        // ════════════════════════════════════════════════════════════════
        //   SHARED HELPERS
        // ════════════════════════════════════════════════════════════════

        static Label SectionLabel(string text, int x, int y)
        {
            return new Label
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                Font      = new Font("Tahoma", 8, FontStyle.Bold),
                ForeColor = SystemColors.ControlDarkDark,
            };
        }

        static ComboBox Combo(string[] items, string selected, int width = 160)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
            foreach (var item in items) c.Items.Add(item);
            c.SelectedItem = selected;
            if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        static NumericUpDown Nud(int min, int max, int val)
        {
            return new NumericUpDown
            {
                Minimum       = min,
                Maximum       = max,
                Value         = Math.Max(min, Math.Min(max, val)),
                DecimalPlaces = 0,
            };
        }

        static Button Btn(string text, int x, int y, int w)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 24) };
        }

        static TreeView MakeAttrTree()
        {
            var tv = new TreeView
            {
                Dock      = DockStyle.Fill,
                Font      = new Font("Consolas", 8),
                ShowLines = true,
            };
            foreach (var cat in AttrLib.Categories())
            {
                var node = tv.Nodes.Add(cat);
                foreach (var a in AttrLib.ForCat(cat))
                    node.Nodes.Add(a.ToString()).Tag = a.Name;
            }
            return tv;
        }

        static void Swap<T>(List<T> list, int i, int j)
        {
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }

        // ════════════════════════════════════════════════════════════════
        //   HELP TEXT
        // ════════════════════════════════════════════════════════════════

        const string HELP_TEXT =
@"TEKLA .RPT TEMPLATE BUILDER
============================

WHAT IS A .RPT FILE?
A Tekla Structures report template (.rpt) defines how the Report tool generates
output. It specifies which objects to include (filters), what data to extract
(fields / formulas), and how to format each column.

HOW TO USE THIS TOOL
  1. Settings tab  — Set the template name, content type, width, sort order.
  2. Filters tab   — Add include/exclude rules. Double-click an attribute in the
                     library on the right to insert its name into the selected row.
  3. Fields tab    — Add output columns. Select a field in the list, then configure
                     its attribute, formula mode, datatype, and unit.
                     Double-click attributes in the library to apply them.
  4. Preview tab   — Click Refresh to see the generated .rpt source.
  5. Save .RPT...  — Prompts for a file path (defaults to XS_FIRM\Reports\ when
                     detectable). Copy the file to Tekla's Reports folder.

FILTER RULE LOGIC
  exclude rules  →  StepOver()  (object is skipped / not written to output)
  include rules  →  Output()    (object is written)

  Combine rules with OR (any match triggers) or AND (all must match).
  match() uses Tekla wildcard syntax:  * = any string,  ? = one character.
  Example: match(GetValue(""NAME""), ""JOIST*"") matches any NAME starting with JOIST.

FORMULA MODES
  plain          GetValue(""ATTR"")
  length_smart   Imperial/metric-aware; handles grating (uses WIDTH); blanks bought items
  weight_smart   Imperial → lbf at 0 decimals; metric → kg at 0 decimals
  custom         You enter the full Tekla report formula expression

SORT TYPES
  NONE        No sorting applied
  ASCENDING   Sort by fields with sort direction = ASCENDING
  DESCENDING  Sort by fields with sort direction = DESCENDING
  COMBINE     Group identical parts; fields with oncombine=SUM are totalled
              (Material list style — confirmed from DTI_Material_list_csv.rpt)

ONCOMBINE VALUES  (applies when sorttype=COMBINE)
  NONE     Keep one representative value (string fields, part mark, etc.)
  SUM      Sum across combined rows (total length, total weight, etc.)
  AVERAGE / MIN / MAX — supported by Tekla but unverified in samples

CLASS & UNIT
  class=""Length""  unit=""mm""   → output in millimetres
  class=""Length""  unit=""ft""   → output in feet (imperial)
  class=""Weight""  unit=""kg""   → output in kilograms
  class=""Weight""  unit=""lbf""  → output in pound-force
  class=""Area""    unit=""m2""   → output in square metres
  Leave both blank for dimensionless values (strings, integers, counts).

FONT COLOR
  Tekla uses internal integer codes for colours.
  0 = black (default),  153 = grey (confirmed from sample files).
  Other codes are environment-specific; check existing .rpt files for values.

INSTALL LOCATION
  XS_FIRM\Reports\        available to all projects firm-wide
  XS_PROJECT\Reports\     available to the current project only
  After copying, run Tools > Reports in Tekla Structures to use the template.

CONFIRMED ATTRIBUTE NAMES  (from objects.inp)
  These do NOT use a USERDEFINED. prefix — plain name only:
  PRELIM_MARK   EXISTING        BOUGHT_ITEM     BOUGHT_ITEM_NAME
  NOTES2–5      ZONE            SEQUENCE        SEQUENCE_NAME
  LOT_NAME      ERECTION_SEQ_NO HEAT_NUMBER     BATCH_NUMBER
  LOAD_NUMBER   BARCODEMARK     DRAWING_STATUS
  SS_CVN        SS_BOUGHT_ITEM  SS_COMMENT      SS_PAINT      SS_CLEAN
  SS_CAMBER_ROLL SS_LOT         SS_FACILITY     SS_E_DWG      SS_D_DWG
  FAB_NAME      ASSEM_INST_STAT ASSEM_HOLD_STAT

  NOTE: ASSEMBLY_POS and PART_POS are commonly used names but should be
  verified against your project's report properties.";
    }
}
