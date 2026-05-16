// RptBuilder.cs — Tekla Structures macro
// Builds Tekla .rpt report template files through a GUI.
// Drop into your macros folder, run via Tools > Macros > RptBuilder.
// No model connection required.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Tekla.Technology.Akit.UserScript
{
    // ════════════════════════════════════════════════════════════════════
    //   ENTRY POINT
    // ════════════════════════════════════════════════════════════════════
    public class Script
    {
        private static RptBuilderForm _form;

        public static void Run(IScript akit)
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
    }

    // ════════════════════════════════════════════════════════════════════
    //   DATA MODELS
    // ════════════════════════════════════════════════════════════════════
    public class FilterRule
    {
        public int    Id;
        public bool   Enabled   = true;
        public string Attribute = "";
        public string Operator  = "==";
        public string Value     = "";
        public string Action    = "Exclude";
    }

    public class FieldDef
    {
        public int    Id;
        public string Name          = "";
        public string Attribute     = "";
        public string FormulaMode   = "plain";   // plain | length_smart | weight_smart | custom
        public string CustomFormula = "";
        public string Datatype      = "STRING";
        public int    Length        = 20;
        public int    Decimals      = 0;
        public string Justify       = "LEFT";
        public string SortDirection = "NONE";
        public string OnCombine     = "NONE";
        public string FieldClass    = "";
        public string Unit          = "";
        public string FontName      = "Arial";
        public int    FontSize      = 8;
        public int    FontColor     = 0;
        public int    FontType      = 2;
        public int    FontRatio     = 100;
        public bool   Visible       = true;
    }

    public class TemplateSettings
    {
        public string TemplateName   = "My Report";
        public string InternalName   = "";
        public string Type           = "TEXTUAL";
        public int    Width          = 130;
        public int    MaxHeight      = 0;
        public string ContentType    = "PART";
        public string RowName        = "row";
        public int    RowHeight      = 5;
        public bool   UseColumns     = true;
        public string SortType       = "NONE";
        public string Notes          = "";
        public string SeparatorChar  = ",";
        public string FilterLogicOp  = "OR";
    }

    // ════════════════════════════════════════════════════════════════════
    //   ATTRIBUTE LIBRARY
    // ════════════════════════════════════════════════════════════════════
    public static class AttrLib
    {
        public struct AttrEntry { public string Attr, Type, Hint; }
        public struct Category  { public string Name; public List<AttrEntry> Items; }

        public static readonly List<Category> All = new List<Category>
        {
            new Category { Name = "Identity", Items = new List<AttrEntry> {
                new AttrEntry { Attr="NAME",           Type="STRING",  Hint="Part profile name" },
                new AttrEntry { Attr="NUMBER",         Type="INTEGER", Hint="Quantity / running count" },
                new AttrEntry { Attr="PART_POS",       Type="STRING",  Hint="Part position mark" },
                new AttrEntry { Attr="ASSEMBLY_POS",   Type="STRING",  Hint="Assembly position mark" },
                new AttrEntry { Attr="MARK",           Type="STRING",  Hint="Part mark" },
                new AttrEntry { Attr="ASSEMBLY_MARK",  Type="STRING",  Hint="Assembly mark" },
                new AttrEntry { Attr="PREFIX",         Type="STRING",  Hint="Part prefix" },
                new AttrEntry { Attr="RUNNING_NUMBER", Type="INTEGER", Hint="Running number" },
            }},
            new Category { Name = "Geometry", Items = new List<AttrEntry> {
                new AttrEntry { Attr="PROFILE", Type="STRING", Hint="Profile / section name" },
                new AttrEntry { Attr="LENGTH",  Type="DOUBLE", Hint="Part length (mm)" },
                new AttrEntry { Attr="WIDTH",   Type="DOUBLE", Hint="Part width (mm)" },
                new AttrEntry { Attr="HEIGHT",  Type="DOUBLE", Hint="Part height (mm)" },
                new AttrEntry { Attr="WEIGHT",  Type="DOUBLE", Hint="Part weight (kg)" },
                new AttrEntry { Attr="AREA",    Type="DOUBLE", Hint="Surface area (m2)" },
                new AttrEntry { Attr="VOLUME",  Type="DOUBLE", Hint="Volume (m3)" },
            }},
            new Category { Name = "Material", Items = new List<AttrEntry> {
                new AttrEntry { Attr="MATERIAL",      Type="STRING", Hint="Material / grade name" },
                new AttrEntry { Attr="MATERIAL_TYPE", Type="STRING", Hint="Material type" },
                new AttrEntry { Attr="FINISH",        Type="STRING", Hint="Surface finish" },
            }},
            new Category { Name = "Position", Items = new List<AttrEntry> {
                new AttrEntry { Attr="PHASE",          Type="INTEGER", Hint="Phase number" },
                new AttrEntry { Attr="SEQUENCE",       Type="INTEGER", Hint="Erection sequence number" },
                new AttrEntry { Attr="SEQUENCE_NAME",  Type="STRING",  Hint="Sequence name" },
                new AttrEntry { Attr="ZONE",           Type="STRING",  Hint="Zone name" },
                new AttrEntry { Attr="LOT_NAME",       Type="STRING",  Hint="Lot name" },
                new AttrEntry { Attr="ERECTION_SEQ_NO",Type="STRING",  Hint="Erection sequence no" },
            }},
            new Category { Name = "Tracking", Items = new List<AttrEntry> {
                new AttrEntry { Attr="PRELIM_MARK",    Type="STRING", Hint="Preliminary mark" },
                new AttrEntry { Attr="EXISTING",       Type="STRING", Hint="Existing steel flag" },
                new AttrEntry { Attr="BOUGHT_ITEM",    Type="STRING", Hint="Bought item flag" },
                new AttrEntry { Attr="BOUGHT_ITEM_NAME",Type="STRING",Hint="Bought item description" },
                new AttrEntry { Attr="BARCODEMARK",    Type="STRING", Hint="Barcode mark" },
                new AttrEntry { Attr="DRAWING_STATUS", Type="STRING", Hint="Drawing status" },
            }},
            new Category { Name = "Fabrication", Items = new List<AttrEntry> {
                new AttrEntry { Attr="HEAT_NUMBER",    Type="STRING", Hint="Heat number" },
                new AttrEntry { Attr="BATCH_NUMBER",   Type="STRING", Hint="Batch number" },
                new AttrEntry { Attr="LOAD_NUMBER",    Type="STRING", Hint="Load number" },
                new AttrEntry { Attr="FAB_NAME",       Type="STRING", Hint="Fabricator name" },
                new AttrEntry { Attr="ASSEM_INST_STAT",Type="STRING", Hint="Assembly install status" },
                new AttrEntry { Attr="ASSEM_HOLD_STAT",Type="STRING", Hint="Assembly hold status" },
            }},
            new Category { Name = "Notes", Items = new List<AttrEntry> {
                new AttrEntry { Attr="NOTES",  Type="STRING", Hint="Notes 1" },
                new AttrEntry { Attr="NOTES2", Type="STRING", Hint="Notes 2" },
                new AttrEntry { Attr="NOTES3", Type="STRING", Hint="Notes 3" },
                new AttrEntry { Attr="NOTES4", Type="STRING", Hint="Notes 4" },
                new AttrEntry { Attr="NOTES5", Type="STRING", Hint="Notes 5" },
            }},
            new Category { Name = "Steligence (SS_)", Items = new List<AttrEntry> {
                new AttrEntry { Attr="SS_CVN",        Type="STRING", Hint="CVN requirement" },
                new AttrEntry { Attr="SS_BOUGHT_ITEM",Type="STRING", Hint="SS bought item flag" },
                new AttrEntry { Attr="SS_COMMENT",    Type="STRING", Hint="SS comment" },
                new AttrEntry { Attr="SS_PAINT",      Type="STRING", Hint="Paint spec" },
                new AttrEntry { Attr="SS_CLEAN",      Type="STRING", Hint="Clean spec" },
                new AttrEntry { Attr="SS_CAMBER_ROLL",Type="STRING", Hint="Camber/roll spec" },
                new AttrEntry { Attr="SS_LOT",        Type="STRING", Hint="SS lot" },
                new AttrEntry { Attr="SS_FACILITY",   Type="STRING", Hint="Fabrication facility" },
                new AttrEntry { Attr="SS_E_DWG",      Type="STRING", Hint="Erection drawing" },
                new AttrEntry { Attr="SS_D_DWG",      Type="STRING", Hint="Detail drawing" },
            }},
        };
    }

    // ════════════════════════════════════════════════════════════════════
    //   RPT GENERATOR
    // ════════════════════════════════════════════════════════════════════
    public static class RptGen
    {
        public static string EscapeRpt(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r\n", "\\n")
                    .Replace("\n", "\\n");
        }

        public static string BuildFormula(FieldDef f)
        {
            switch (f.FormulaMode)
            {
                case "length_smart":
                    return
                        "if GetValue(\"SS_BOUGHT_ITEM\") == \"Yes\" then\n" +
                        "  \"\"\n" +
                        "else\n" +
                        "  if GetValue(\"ADVANCED_OPTION.XS_IMPERIAL\") == \"TRUE\" then\n" +
                        "    if match(GetValue(\"PRODUCT_NAME\"), \"*GRTG*\") || match(GetValue(\"PRODUCT_NAME\"), \"*GRIP*\") then\n" +
                        "      format(GetValue(\"WIDTH\"), \"Length\", \"ft\", 4)\n" +
                        "    else\n" +
                        "      format(GetValue(\"LENGTH\"), \"Length\", \"ft\", 4)\n" +
                        "    endif\n" +
                        "  else\n" +
                        "    if match(GetValue(\"PRODUCT_NAME\"), \"*GRTG*\") || match(GetValue(\"PRODUCT_NAME\"), \"*GRIP*\") then\n" +
                        "      format(GetValue(\"WIDTH\"), \"Length\", \"mm\", 0)\n" +
                        "    else\n" +
                        "      format(GetValue(\"LENGTH\"), \"Length\", \"mm\", 0)\n" +
                        "    endif\n" +
                        "  endif\n" +
                        "endif";

                case "weight_smart":
                    return
                        "if GetValue(\"ADVANCED_OPTION.XS_IMPERIAL\") == \"TRUE\" then\n" +
                        "  format(GetValue(\"WEIGHT\"), \"Weight\", \"lbf\", 0)\n" +
                        "else\n" +
                        "  format(GetValue(\"WEIGHT\"), \"Weight\", \"kg\", 0)\n" +
                        "endif";

                case "custom":
                    return f.CustomFormula ?? "";

                default: // plain
                    return "GetValue(\"" + f.Attribute + "\")";
            }
        }

        public static string BuildFilterExpression(List<FilterRule> rules, string logicOp)
        {
            var excludeConds = new List<string>();
            var includeConds = new List<string>();
            string join = logicOp == "AND" ? " && " : " || ";

            foreach (var r in rules)
            {
                if (!r.Enabled || string.IsNullOrEmpty(r.Attribute)) continue;
                string cond;
                switch (r.Operator)
                {
                    case "match":  cond = "match(GetValue(\"" + r.Attribute + "\"), \"" + r.Value + "\")";  break;
                    case "!match": cond = "!match(GetValue(\"" + r.Attribute + "\"), \"" + r.Value + "\")"; break;
                    case "!=":     cond = "GetValue(\"" + r.Attribute + "\") != \"" + r.Value + "\""; break;
                    case ">":      cond = "GetValue(\"" + r.Attribute + "\") > \"" + r.Value + "\"";  break;
                    case "<":      cond = "GetValue(\"" + r.Attribute + "\") < \"" + r.Value + "\"";  break;
                    default:       cond = "GetValue(\"" + r.Attribute + "\") == \"" + r.Value + "\""; break;
                }
                if (r.Action == "Include") includeConds.Add(cond);
                else excludeConds.Add(cond);
            }

            if (excludeConds.Count == 0 && includeConds.Count == 0) return "";

            var sb = new StringBuilder();
            if (excludeConds.Count > 0 && includeConds.Count == 0)
            {
                sb.AppendLine("if " + string.Join(join, excludeConds.ToArray()) + " then");
                sb.AppendLine("  StepOver()");
                sb.AppendLine("else");
                sb.AppendLine("  Output()");
                sb.Append("endif");
            }
            else if (includeConds.Count > 0 && excludeConds.Count == 0)
            {
                sb.AppendLine("if " + string.Join(join, includeConds.ToArray()) + " then");
                sb.AppendLine("  Output()");
                sb.AppendLine("else");
                sb.AppendLine("  StepOver()");
                sb.Append("endif");
            }
            else
            {
                // Mixed: exclude takes priority
                sb.AppendLine("if " + string.Join(" || ", excludeConds.ToArray()) + " then");
                sb.AppendLine("  StepOver()");
                sb.AppendLine("else if " + string.Join(" || ", includeConds.ToArray()) + " then");
                sb.AppendLine("  Output()");
                sb.AppendLine("else");
                sb.AppendLine("  StepOver()");
                sb.Append("endif");
            }
            return sb.ToString();
        }

        public static string GenerateRpt(TemplateSettings s, List<FilterRule> filters, List<FieldDef> fields)
        {
            string internalName = string.IsNullOrEmpty(s.InternalName)
                ? s.TemplateName.ToLower().Replace(" ", "_")
                : s.InternalName;

            var sb = new StringBuilder();
            sb.AppendLine("version = 3.5");
            sb.AppendLine("type = " + s.Type);
            sb.AppendLine("template_name = \"" + EscapeRpt(s.TemplateName) + "\"");
            sb.AppendLine("internal_name = \"" + EscapeRpt(internalName) + "\"");
            sb.AppendLine("contenttype = \"" + s.ContentType + "\"");
            sb.AppendLine("width = " + s.Width);
            sb.AppendLine("maxheight = " + s.MaxHeight);
            sb.AppendLine("rowname = \"" + EscapeRpt(s.RowName) + "\"");
            sb.AppendLine("rowheight = " + s.RowHeight);
            sb.AppendLine("usecolumns = " + (s.UseColumns ? "true" : "false"));
            sb.AppendLine("sorttype = " + s.SortType);
            if (!string.IsNullOrEmpty(s.Notes))
                sb.AppendLine("notes = \"" + EscapeRpt(s.Notes) + "\"");
            sb.AppendLine();

            if (fields.Count > 0)
            {
                sb.AppendLine("datafields");
                foreach (var f in fields)
                {
                    sb.AppendLine("  field");
                    sb.AppendLine("    name = \"" + EscapeRpt(f.Name) + "\"");
                    sb.AppendLine("    datatype = " + f.Datatype);
                    sb.AppendLine("    length = " + f.Length);
                    sb.AppendLine("    decimals = " + f.Decimals);
                    sb.AppendLine("    justify = " + f.Justify);
                    sb.AppendLine("    class = \"" + f.FieldClass + "\"");
                    sb.AppendLine("    unit = \"" + f.Unit + "\"");
                    sb.AppendLine("    oncombine = " + f.OnCombine);
                    sb.AppendLine("    sortdirection = " + f.SortDirection);
                    sb.AppendLine("    fieldclass = \"\"");
                    sb.AppendLine("    fontname = \"" + f.FontName + "\"");
                    sb.AppendLine("    fontsize = " + f.FontSize);
                    sb.AppendLine("    fontcolor = " + f.FontColor);
                    sb.AppendLine("    fonttype = " + f.FontType);
                    sb.AppendLine("    fontratio = " + f.FontRatio);
                    sb.AppendLine("    pen = -1");
                    sb.AppendLine("    visibility = " + (f.Visible ? "true" : "false"));
                    sb.AppendLine("    formula = \"" + EscapeRpt(BuildFormula(f)) + "\"");
                    sb.AppendLine("  end_field");
                }
                sb.AppendLine("end_datafields");
                sb.AppendLine();
            }

            string filterExpr = BuildFilterExpression(filters, s.FilterLogicOp);
            if (!string.IsNullOrEmpty(filterExpr))
            {
                sb.AppendLine("filter");
                sb.AppendLine("  rule = \"" + EscapeRpt(filterExpr) + "\"");
                sb.AppendLine("end_filter");
            }

            return sb.ToString();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   PRESETS
    // ════════════════════════════════════════════════════════════════════
    public static class Presets
    {
        public static void LoadMaterialList(
            out TemplateSettings settings,
            out List<FilterRule>  filters,
            out List<FieldDef>    fields)
        {
            settings = new TemplateSettings
            {
                TemplateName  = "Material_List",
                InternalName  = "material_list",
                SortType      = "COMBINE",
                Width         = 130,
                SeparatorChar = ",",
                ContentType   = "PART",
                RowHeight     = 5,
                UseColumns    = true,
            };

            int id = 1;
            filters = new List<FilterRule>
            {
                new FilterRule { Id=id++, Enabled=true, Attribute="MATERIAL", Operator="match", Value="CONCRETE", Action="Exclude" },
                new FilterRule { Id=id++, Enabled=true, Attribute="MATERIAL", Operator="match", Value="GROUT",    Action="Exclude" },
                new FilterRule { Id=id++, Enabled=true, Attribute="NAME",     Operator="match", Value="JOIST*",   Action="Exclude" },
                new FilterRule { Id=id++, Enabled=true, Attribute="EXISTING", Operator="==",    Value="Yes",      Action="Exclude" },
            };

            id = 1;
            fields = new List<FieldDef>
            {
                new FieldDef { Id=id++, Name="Quantity",  Attribute="NUMBER",      FormulaMode="plain", Datatype="INTEGER", Length=10, Decimals=0, Justify="RIGHT", OnCombine="NONE", FieldClass="",       Unit=""  },
                new FieldDef { Id=id++, Name="Prelim_no", Attribute="PRELIM_MARK", FormulaMode="plain", Datatype="INTEGER", Length=10, Decimals=0, Justify="RIGHT", OnCombine="NONE", FieldClass="",       Unit=""  },
                new FieldDef { Id=id++, Name="Profile",   Attribute="PROFILE",     FormulaMode="plain", Datatype="STRING",  Length=20, Decimals=0, Justify="LEFT",  OnCombine="NONE", FieldClass="",       Unit=""  },
                new FieldDef { Id=id++, Name="Grade",     Attribute="MATERIAL",    FormulaMode="plain", Datatype="STRING",  Length=10, Decimals=0, Justify="LEFT",  OnCombine="NONE", FieldClass="",       Unit=""  },
                new FieldDef { Id=id++, Name="Length",    Attribute="LENGTH",      FormulaMode="plain", Datatype="DOUBLE",  Length=12, Decimals=0, Justify="RIGHT", OnCombine="SUM",  FieldClass="Length", Unit="mm"},
                new FieldDef { Id=id++, Name="Weight",    Attribute="WEIGHT",      FormulaMode="plain", Datatype="DOUBLE",  Length=12, Decimals=2, Justify="RIGHT", OnCombine="NONE", FieldClass="Weight", Unit=""  },
                new FieldDef { Id=id++, Name="Phase",     Attribute="PHASE",       FormulaMode="plain", Datatype="INTEGER", Length=6,  Decimals=0, Justify="RIGHT", OnCombine="SUM",  FieldClass="Weight", Unit="kg"},
                new FieldDef { Id=id++, Name="CVN",       Attribute="SS_CVN",      FormulaMode="plain", Datatype="STRING",  Length=10, Decimals=0, Justify="LEFT",  OnCombine="NONE", FieldClass="Area",   Unit="m2"},
            };
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //   MAIN FORM
    // ════════════════════════════════════════════════════════════════════
    public class RptBuilderForm : Form
    {
        // ── State ──────────────────────────────────────────────────────
        private TemplateSettings _settings = new TemplateSettings();
        private List<FilterRule>  _filters  = new List<FilterRule>();
        private List<FieldDef>    _fields   = new List<FieldDef>();
        private int  _nextId      = 1;
        private bool _ignoreEvents = false;

        // ── Settings tab ───────────────────────────────────────────────
        private TextBox        _tbTemplateName, _tbInternalName, _tbNotes, _tbSepChar;
        private ComboBox       _cbContentType, _cbSortType, _cbFilterLogic;
        private NumericUpDown  _nudWidth, _nudMaxHeight, _nudRowHeight;

        // ── Filters tab ────────────────────────────────────────────────
        private DataGridView _filterGrid;

        // ── Fields tab ─────────────────────────────────────────────────
        private ListBox       _fieldList;
        private Panel         _fieldEditorPanel;
        private TextBox       _tbFName, _tbFAttr, _tbFUnit, _tbFFontName, _tbFCustomFormula;
        private ComboBox      _cbFDatatype, _cbFJustify, _cbFSortDir, _cbFOnCombine, _cbFClass;
        private NumericUpDown _nudFLength, _nudFDecimals, _nudFFontSize, _nudFFontColor;
        private RadioButton   _rbPlain, _rbLengthSmart, _rbWeightSmart, _rbCustom;
        private CheckBox      _chkFVisible;

        // ── Preview tab ────────────────────────────────────────────────
        private TextBox _previewBox;

        // ── Attr trees ────────────────────────────────────────────────
        private TreeView _filterAttrTree, _fieldAttrTree;

        // ══════════════════════════════════════════════════════════════
        public RptBuilderForm()
        {
            Text = "Tekla .RPT Template Builder";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition   = FormStartPosition.CenterScreen;
            Size            = new Size(840, 700);
            MinimumSize     = new Size(700, 560);
            BuildUI();
            LoadPreset_MaterialList();
        }

        // ── Top-level layout ───────────────────────────────────────────
        private void BuildUI()
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 38 };

            var lblPreset = new Label { Text = "Preset:", AutoSize = true, Left = 6, Top = 11 };
            var cbPreset  = new ComboBox { Left = 56, Top = 7, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cbPreset.Items.Add("Material List");
            cbPreset.SelectedIndex = 0;
            var btnLoad = new Button { Text = "Load",      Left = 212, Top = 6, Width = 55, Height = 26 };
            var btnSave = new Button { Text = "Save .rpt…",Left = 276, Top = 6, Width = 90, Height = 26 };
            btnLoad.Click += (s, e) => LoadPreset_MaterialList();
            btnSave.Click += OnSaveRpt;
            toolbar.Controls.AddRange(new Control[] { lblPreset, cbPreset, btnLoad, btnSave });

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tp1 = new TabPage("Settings");
            var tp2 = new TabPage("Filters");
            var tp3 = new TabPage("Fields");
            var tp4 = new TabPage("Preview");
            var tp5 = new TabPage("Help");
            tabs.TabPages.AddRange(new[] { tp1, tp2, tp3, tp4, tp5 });
            tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedTab == tp4) RefreshPreview(); };

            BuildSettingsTab(tp1);
            BuildFiltersTab(tp2);
            BuildFieldsTab(tp3);
            BuildPreviewTab(tp4);
            BuildHelpTab(tp5);

            Controls.Add(tabs);
            Controls.Add(toolbar);
        }

        // ── Tab 1: Settings ────────────────────────────────────────────
        private void BuildSettingsTab(TabPage tp)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(10, 8, 10, 8),
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _tbTemplateName = new TextBox { Dock = DockStyle.Fill };
            _tbInternalName = new TextBox { Dock = DockStyle.Fill };
            _cbContentType  = MakeCombo("PART", "ASSEMBLY");
            _cbSortType     = MakeCombo("NONE", "ASCENDING", "DESCENDING", "COMBINE");
            _nudWidth       = MakeNud(1, 9999, 130);
            _nudMaxHeight   = MakeNud(0, 9999, 0);
            _nudRowHeight   = MakeNud(0, 999, 5);
            _tbSepChar      = new TextBox { Dock = DockStyle.Fill, MaxLength = 5 };
            _cbFilterLogic  = MakeCombo("OR", "AND");
            _tbNotes        = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };

            AddRow(tbl, "Template Name",  _tbTemplateName);
            AddRow(tbl, "Internal Name",  _tbInternalName);
            AddRow(tbl, "Content Type",   _cbContentType);
            AddRow(tbl, "Sort Type",      _cbSortType);
            AddRow(tbl, "Width",          _nudWidth);
            AddRow(tbl, "Max Height",     _nudMaxHeight);
            AddRow(tbl, "Row Height",     _nudRowHeight);
            AddRow(tbl, "Separator Char", _tbSepChar);
            AddRow(tbl, "Filter Logic",   _cbFilterLogic);
            AddRow(tbl, "Notes",          _tbNotes);

            _tbTemplateName.TextChanged  += (s, e) => { if (!_ignoreEvents) _settings.TemplateName = _tbTemplateName.Text; };
            _tbInternalName.TextChanged  += (s, e) => { if (!_ignoreEvents) _settings.InternalName = _tbInternalName.Text; };
            _cbContentType.SelectedIndexChanged  += (s, e) => { if (!_ignoreEvents && _cbContentType.SelectedItem != null) _settings.ContentType = _cbContentType.SelectedItem.ToString(); };
            _cbSortType.SelectedIndexChanged     += (s, e) => { if (!_ignoreEvents && _cbSortType.SelectedItem != null) _settings.SortType = _cbSortType.SelectedItem.ToString(); };
            _nudWidth.ValueChanged       += (s, e) => { if (!_ignoreEvents) _settings.Width = (int)_nudWidth.Value; };
            _nudMaxHeight.ValueChanged   += (s, e) => { if (!_ignoreEvents) _settings.MaxHeight = (int)_nudMaxHeight.Value; };
            _nudRowHeight.ValueChanged   += (s, e) => { if (!_ignoreEvents) _settings.RowHeight = (int)_nudRowHeight.Value; };
            _tbSepChar.TextChanged       += (s, e) => { if (!_ignoreEvents) _settings.SeparatorChar = _tbSepChar.Text; };
            _cbFilterLogic.SelectedIndexChanged  += (s, e) => { if (!_ignoreEvents && _cbFilterLogic.SelectedItem != null) _settings.FilterLogicOp = _cbFilterLogic.SelectedItem.ToString(); };
            _tbNotes.TextChanged         += (s, e) => { if (!_ignoreEvents) _settings.Notes = _tbNotes.Text; };

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(tbl);
            tp.Controls.Add(scroll);
        }

        // ── Tab 2: Filters ─────────────────────────────────────────────
        private void BuildFiltersTab(TabPage tp)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill };

            // Filter grid
            _filterGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                EditMode              = DataGridViewEditMode.EditOnEnter,
            };

            var colEn   = new DataGridViewCheckBoxColumn { Name="Enabled",   HeaderText="On",        Width=35 };
            var colAttr = new DataGridViewTextBoxColumn  { Name="Attribute",  HeaderText="Attribute", Width=150 };
            var colOp   = new DataGridViewComboBoxColumn { Name="Operator",   HeaderText="Op",        Width=80  };
            colOp.Items.AddRange("==", "!=", "match", "!match", ">", "<");
            var colVal  = new DataGridViewTextBoxColumn  { Name="Value",      HeaderText="Value",     Width=150 };
            var colAct  = new DataGridViewComboBoxColumn { Name="Action",     HeaderText="Action",    Width=80  };
            colAct.Items.AddRange("Exclude", "Include");
            _filterGrid.Columns.AddRange(new DataGridViewColumn[] { colEn, colAttr, colOp, colVal, colAct });

            _filterGrid.CellValueChanged += OnFilterCellChanged;
            _filterGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_filterGrid.IsCurrentCellDirty)
                    _filterGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            var btnAdd   = new Button { Text="Add Rule",  Left=4,   Top=5, Width=76,  Height=26 };
            var btnDel   = new Button { Text="Delete",    Left=84,  Top=5, Width=60,  Height=26 };
            var btnUp    = new Button { Text="↑",         Left=150, Top=5, Width=34,  Height=26 };
            var btnDown  = new Button { Text="↓",         Left=188, Top=5, Width=34,  Height=26 };
            var btnClear = new Button { Text="Clear All", Left=228, Top=5, Width=76,  Height=26 };
            btnAdd.Click   += OnFilterAdd;
            btnDel.Click   += OnFilterDelete;
            btnUp.Click    += (s, e) => MoveFilter(-1);
            btnDown.Click  += (s, e) => MoveFilter(+1);
            btnClear.Click += (s, e) => { _filters.Clear(); RefreshFilterGrid(); };
            btnPanel.Controls.AddRange(new Control[] { btnAdd, btnDel, btnUp, btnDown, btnClear });

            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(_filterGrid);
            leftPanel.Controls.Add(btnPanel);
            split.Panel1.Controls.Add(leftPanel);

            // Attribute browser (right)
            _filterAttrTree = BuildAttrTree();
            _filterAttrTree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node.Tag is string attr && _filterGrid.CurrentRow != null)
                    _filterGrid.CurrentRow.Cells["Attribute"].Value = attr;
            };
            var tvLabel = new Label { Text = "Double-click to insert attribute →", Dock = DockStyle.Top, Height = 18, Font = new Font(Font.FontFamily, 7f) };
            split.Panel2.Controls.Add(_filterAttrTree);
            split.Panel2.Controls.Add(tvLabel);

            split.SplitterDistance = 560;
            tp.Controls.Add(split);
        }

        private void OnFilterCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_ignoreEvents || e.RowIndex < 0 || e.RowIndex >= _filters.Count) return;
            var r   = _filters[e.RowIndex];
            var row = _filterGrid.Rows[e.RowIndex];
            r.Enabled   = row.Cells["Enabled"].Value   as bool?   ?? true;
            r.Attribute = row.Cells["Attribute"].Value as string  ?? "";
            r.Operator  = row.Cells["Operator"].Value  as string  ?? "==";
            r.Value     = row.Cells["Value"].Value     as string  ?? "";
            r.Action    = row.Cells["Action"].Value    as string  ?? "Exclude";
        }

        private void OnFilterAdd(object sender, EventArgs e)
        {
            _filters.Add(new FilterRule { Id = _nextId++ });
            RefreshFilterGrid();
            _filterGrid.ClearSelection();
            if (_filterGrid.Rows.Count > 0)
                _filterGrid.Rows[_filterGrid.Rows.Count - 1].Selected = true;
        }

        private void OnFilterDelete(object sender, EventArgs e)
        {
            if (_filterGrid.CurrentRow == null) return;
            int idx = _filterGrid.CurrentRow.Index;
            if (idx >= 0 && idx < _filters.Count) { _filters.RemoveAt(idx); RefreshFilterGrid(); }
        }

        private void MoveFilter(int dir)
        {
            if (_filterGrid.CurrentRow == null) return;
            int idx    = _filterGrid.CurrentRow.Index;
            int newIdx = idx + dir;
            if (newIdx < 0 || newIdx >= _filters.Count) return;
            var tmp = _filters[idx]; _filters[idx] = _filters[newIdx]; _filters[newIdx] = tmp;
            RefreshFilterGrid();
            _filterGrid.ClearSelection();
            _filterGrid.Rows[newIdx].Selected = true;
        }

        private void RefreshFilterGrid()
        {
            _ignoreEvents = true;
            _filterGrid.Rows.Clear();
            foreach (var r in _filters)
            {
                int i = _filterGrid.Rows.Add();
                var row = _filterGrid.Rows[i];
                row.Cells["Enabled"].Value   = r.Enabled;
                row.Cells["Attribute"].Value = r.Attribute;
                row.Cells["Operator"].Value  = r.Operator;
                row.Cells["Value"].Value     = r.Value;
                row.Cells["Action"].Value    = r.Action;
            }
            _ignoreEvents = false;
        }

        // ── Tab 3: Fields ──────────────────────────────────────────────
        private void BuildFieldsTab(TabPage tp)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill };

            // Left: list on top, editor below
            var leftSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 180,
            };

            // Field list
            _fieldList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _fieldList.SelectedIndexChanged += OnFieldSelected;

            var listBtnPanel = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            var btnFAdd  = new Button { Text="Add Field", Left=4,   Top=5, Width=76, Height=26 };
            var btnFDel  = new Button { Text="Delete",    Left=84,  Top=5, Width=60, Height=26 };
            var btnFUp   = new Button { Text="↑",         Left=150, Top=5, Width=34, Height=26 };
            var btnFDown = new Button { Text="↓",         Left=188, Top=5, Width=34, Height=26 };
            btnFAdd.Click  += OnFieldAdd;
            btnFDel.Click  += OnFieldDelete;
            btnFUp.Click   += (s, e) => MoveField(-1);
            btnFDown.Click += (s, e) => MoveField(+1);
            listBtnPanel.Controls.AddRange(new Control[] { btnFAdd, btnFDel, btnFUp, btnFDown });

            var listPanel = new Panel { Dock = DockStyle.Fill };
            listPanel.Controls.Add(_fieldList);
            listPanel.Controls.Add(listBtnPanel);
            leftSplit.Panel1.Controls.Add(listPanel);

            // Field editor
            _fieldEditorPanel = BuildFieldEditorPanel();
            _fieldEditorPanel.Enabled = false;
            leftSplit.Panel2.Controls.Add(_fieldEditorPanel);

            split.Panel1.Controls.Add(leftSplit);

            // Attribute browser (right)
            _fieldAttrTree = BuildAttrTree();
            _fieldAttrTree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node.Tag is string attr)
                {
                    _tbFAttr.Text = attr;
                    if (string.IsNullOrEmpty(_tbFName.Text)) _tbFName.Text = attr;
                }
            };
            var tvLabel2 = new Label { Text = "Double-click to set attribute →", Dock = DockStyle.Top, Height = 18, Font = new Font(Font.FontFamily, 7f) };
            split.Panel2.Controls.Add(_fieldAttrTree);
            split.Panel2.Controls.Add(tvLabel2);

            split.SplitterDistance = 540;
            tp.Controls.Add(split);
        }

        private Panel BuildFieldEditorPanel()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(6, 4, 6, 4),
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _tbFName     = new TextBox { Dock = DockStyle.Fill };
            _tbFAttr     = new TextBox { Dock = DockStyle.Fill };
            _cbFDatatype = MakeCombo("STRING", "INTEGER", "DOUBLE", "FLOAT");
            _nudFLength  = MakeNud(1, 999, 20);
            _nudFDecimals= MakeNud(0, 10, 0);
            _cbFJustify  = MakeCombo("LEFT", "RIGHT", "CENTER");
            _cbFSortDir  = MakeCombo("NONE", "ASCENDING", "DESCENDING");
            _cbFOnCombine= MakeCombo("NONE", "SUM", "AVERAGE", "MIN", "MAX");
            _cbFClass    = MakeCombo("", "Length", "Weight", "Area");
            _tbFUnit     = new TextBox { Dock = DockStyle.Fill };
            _chkFVisible = new CheckBox { Dock = DockStyle.Fill, Text = "Visible", Checked = true };
            _tbFFontName = new TextBox { Dock = DockStyle.Fill, Text = "Arial" };
            _nudFFontSize  = MakeNud(4, 72, 8);
            _nudFFontColor = MakeNud(0, 9999, 0);

            // Formula mode radio panel
            var radioPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Height = 26 };
            _rbPlain       = new RadioButton { Text = "Plain",        AutoSize = true, Checked = true };
            _rbLengthSmart = new RadioButton { Text = "Length Smart", AutoSize = true };
            _rbWeightSmart = new RadioButton { Text = "Weight Smart", AutoSize = true };
            _rbCustom      = new RadioButton { Text = "Custom",       AutoSize = true };
            radioPanel.Controls.AddRange(new Control[] { _rbPlain, _rbLengthSmart, _rbWeightSmart, _rbCustom });

            _tbFCustomFormula = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Height = 56,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Courier New", 8),
                Enabled = false,
            };

            AddRow(tbl, "Name",          _tbFName);
            AddRow(tbl, "Attribute",     _tbFAttr);
            AddRow(tbl, "Formula",       radioPanel);
            AddRow(tbl, "Custom Formula",_tbFCustomFormula);
            AddRow(tbl, "Datatype",      _cbFDatatype);
            AddRow(tbl, "Length",        _nudFLength);
            AddRow(tbl, "Decimals",      _nudFDecimals);
            AddRow(tbl, "Justify",       _cbFJustify);
            AddRow(tbl, "Sort Dir",      _cbFSortDir);
            AddRow(tbl, "On Combine",    _cbFOnCombine);
            AddRow(tbl, "Class",         _cbFClass);
            AddRow(tbl, "Unit",          _tbFUnit);
            AddRow(tbl, "Visible",       _chkFVisible);
            AddRow(tbl, "Font Name",     _tbFFontName);
            AddRow(tbl, "Font Size",     _nudFFontSize);
            AddRow(tbl, "Font Color",    _nudFFontColor);

            EventHandler changed = (s, e) => { if (!_ignoreEvents) SyncFieldFromEditor(); };
            _tbFName.TextChanged          += changed;
            _tbFAttr.TextChanged          += changed;
            _cbFDatatype.SelectedIndexChanged   += changed;
            _nudFLength.ValueChanged      += changed;
            _nudFDecimals.ValueChanged    += changed;
            _cbFJustify.SelectedIndexChanged    += changed;
            _cbFSortDir.SelectedIndexChanged    += changed;
            _cbFOnCombine.SelectedIndexChanged  += changed;
            _cbFClass.SelectedIndexChanged      += changed;
            _tbFUnit.TextChanged          += changed;
            _chkFVisible.CheckedChanged   += changed;
            _tbFFontName.TextChanged      += changed;
            _nudFFontSize.ValueChanged    += changed;
            _nudFFontColor.ValueChanged   += changed;
            _tbFCustomFormula.TextChanged += changed;

            EventHandler modeChanged = (s, e) =>
            {
                _tbFCustomFormula.Enabled = _rbCustom.Checked;
                if (!_ignoreEvents) SyncFieldFromEditor();
            };
            _rbPlain.CheckedChanged       += modeChanged;
            _rbLengthSmart.CheckedChanged += modeChanged;
            _rbWeightSmart.CheckedChanged += modeChanged;
            _rbCustom.CheckedChanged      += modeChanged;

            scroll.Controls.Add(tbl);
            return scroll;
        }

        private void OnFieldAdd(object sender, EventArgs e)
        {
            var f = new FieldDef { Id = _nextId++, Name = "Field" + _nextId };
            _fields.Add(f);
            RefreshFieldList(selectLast: true);
        }

        private void OnFieldDelete(object sender, EventArgs e)
        {
            int idx = _fieldList.SelectedIndex;
            if (idx < 0 || idx >= _fields.Count) return;
            _fields.RemoveAt(idx);
            RefreshFieldList();
            if (_fields.Count > 0)
                _fieldList.SelectedIndex = Math.Min(idx, _fields.Count - 1);
            else
                _fieldEditorPanel.Enabled = false;
        }

        private void MoveField(int dir)
        {
            int idx    = _fieldList.SelectedIndex;
            int newIdx = idx + dir;
            if (idx < 0 || newIdx < 0 || newIdx >= _fields.Count) return;
            var tmp = _fields[idx]; _fields[idx] = _fields[newIdx]; _fields[newIdx] = tmp;
            _ignoreEvents = true;
            RefreshFieldList();
            _fieldList.SelectedIndex = newIdx;
            _ignoreEvents = false;
        }

        private void RefreshFieldList(bool selectLast = false)
        {
            _ignoreEvents = true;
            int sel = selectLast ? _fields.Count - 1 : _fieldList.SelectedIndex;
            _fieldList.Items.Clear();
            foreach (var f in _fields) _fieldList.Items.Add(f.Name);
            if (sel >= 0 && sel < _fieldList.Items.Count) _fieldList.SelectedIndex = sel;
            _ignoreEvents = false;

            // Trigger editor sync after re-enable
            if (_fieldList.SelectedIndex >= 0)
            {
                _fieldEditorPanel.Enabled = true;
                LoadFieldToEditor(_fields[_fieldList.SelectedIndex]);
            }
        }

        private void OnFieldSelected(object sender, EventArgs e)
        {
            if (_ignoreEvents) return;
            int idx = _fieldList.SelectedIndex;
            if (idx < 0 || idx >= _fields.Count) { _fieldEditorPanel.Enabled = false; return; }
            _fieldEditorPanel.Enabled = true;
            LoadFieldToEditor(_fields[idx]);
        }

        private void LoadFieldToEditor(FieldDef f)
        {
            _ignoreEvents = true;
            _tbFName.Text           = f.Name;
            _tbFAttr.Text           = f.Attribute;
            SetCombo(_cbFDatatype,  f.Datatype);
            _nudFLength.Value       = Clamp(_nudFLength,  f.Length);
            _nudFDecimals.Value     = Clamp(_nudFDecimals,f.Decimals);
            SetCombo(_cbFJustify,   f.Justify);
            SetCombo(_cbFSortDir,   f.SortDirection);
            SetCombo(_cbFOnCombine, f.OnCombine);
            SetCombo(_cbFClass,     f.FieldClass);
            _tbFUnit.Text           = f.Unit;
            _chkFVisible.Checked    = f.Visible;
            _tbFFontName.Text       = f.FontName;
            _nudFFontSize.Value     = Clamp(_nudFFontSize,  f.FontSize);
            _nudFFontColor.Value    = Clamp(_nudFFontColor, f.FontColor);
            _tbFCustomFormula.Text  = f.CustomFormula;
            _rbPlain.Checked        = f.FormulaMode == "plain";
            _rbLengthSmart.Checked  = f.FormulaMode == "length_smart";
            _rbWeightSmart.Checked  = f.FormulaMode == "weight_smart";
            _rbCustom.Checked       = f.FormulaMode == "custom";
            _tbFCustomFormula.Enabled = f.FormulaMode == "custom";
            _ignoreEvents = false;
        }

        private void SyncFieldFromEditor()
        {
            int idx = _fieldList.SelectedIndex;
            if (idx < 0 || idx >= _fields.Count) return;
            var f = _fields[idx];
            f.Name          = _tbFName.Text;
            f.Attribute     = _tbFAttr.Text;
            f.Datatype      = _cbFDatatype.SelectedItem?.ToString()  ?? "STRING";
            f.Length        = (int)_nudFLength.Value;
            f.Decimals      = (int)_nudFDecimals.Value;
            f.Justify       = _cbFJustify.SelectedItem?.ToString()   ?? "LEFT";
            f.SortDirection = _cbFSortDir.SelectedItem?.ToString()   ?? "NONE";
            f.OnCombine     = _cbFOnCombine.SelectedItem?.ToString() ?? "NONE";
            f.FieldClass    = _cbFClass.SelectedItem?.ToString()     ?? "";
            f.Unit          = _tbFUnit.Text;
            f.Visible       = _chkFVisible.Checked;
            f.FontName      = _tbFFontName.Text;
            f.FontSize      = (int)_nudFFontSize.Value;
            f.FontColor     = (int)_nudFFontColor.Value;
            f.CustomFormula = _tbFCustomFormula.Text;
            f.FormulaMode   = _rbLengthSmart.Checked ? "length_smart"
                            : _rbWeightSmart.Checked ? "weight_smart"
                            : _rbCustom.Checked      ? "custom"
                            : "plain";
            // Update list display name without re-triggering selection
            _ignoreEvents = true;
            _fieldList.Items[idx] = f.Name;
            _ignoreEvents = false;
        }

        // ── Tab 4: Preview ─────────────────────────────────────────────
        private void BuildPreviewTab(TabPage tp)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420,
            };

            _previewBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Courier New", 8),
                WordWrap = false,
            };
            split.Panel1.Controls.Add(_previewBox);

            var infoBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "Live member simulation is not available in the macro version.\r\n\r\n" +
                       "The .rpt source above is your deliverable.\r\n\r\n" +
                       "To use:\r\n" +
                       "  1. Click 'Save .rpt…' in the toolbar.\r\n" +
                       "  2. Copy the file to your Tekla model's 'attributes' folder,\r\n" +
                       "     or the global templates folder:\r\n" +
                       "       %XSDATADIR%\\environments\\<env>\\reference\\\r\n" +
                       "  3. In Tekla Structures: Reports > Create Report.\r\n" +
                       "     Your template will appear in the list.",
            };
            split.Panel2.Controls.Add(infoBox);

            tp.Controls.Add(split);
        }

        private void RefreshPreview()
        {
            if (_previewBox != null)
                _previewBox.Text = RptGen.GenerateRpt(_settings, _filters, _fields);
        }

        // ── Tab 5: Help ────────────────────────────────────────────────
        private void BuildHelpTab(TabPage tp)
        {
            var rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Courier New", 8),
                BackColor = SystemColors.Window,
                Text = HELP_TEXT,
            };
            tp.Controls.Add(rtb);
        }

        // ── Preset loading ─────────────────────────────────────────────
        private void LoadPreset_MaterialList()
        {
            Presets.LoadMaterialList(out _settings, out _filters, out _fields);
            _nextId = 20;
            PushSettingsToUI();
            RefreshFilterGrid();
            _ignoreEvents = true;
            _fieldList.Items.Clear();
            foreach (var f in _fields) _fieldList.Items.Add(f.Name);
            _ignoreEvents = false;
            if (_fields.Count > 0)
            {
                _fieldList.SelectedIndex = 0;
                _fieldEditorPanel.Enabled = true;
                LoadFieldToEditor(_fields[0]);
            }
        }

        private void PushSettingsToUI()
        {
            _ignoreEvents = true;
            _tbTemplateName.Text = _settings.TemplateName;
            _tbInternalName.Text = _settings.InternalName;
            SetCombo(_cbContentType, _settings.ContentType);
            SetCombo(_cbSortType,    _settings.SortType);
            _nudWidth.Value     = Clamp(_nudWidth,     _settings.Width);
            _nudMaxHeight.Value = Clamp(_nudMaxHeight, _settings.MaxHeight);
            _nudRowHeight.Value = Clamp(_nudRowHeight, _settings.RowHeight);
            _tbSepChar.Text     = _settings.SeparatorChar;
            SetCombo(_cbFilterLogic, _settings.FilterLogicOp);
            _tbNotes.Text       = _settings.Notes;
            _ignoreEvents = false;
        }

        // ── Save ────────────────────────────────────────────────────────
        private void OnSaveRpt(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title    = "Save Tekla Report Template",
                Filter   = "Tekla Report Template (*.rpt)|*.rpt|All Files (*.*)|*.*",
                FileName = _settings.TemplateName.Replace(" ", "_") + ".rpt",
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string content = RptGen.GenerateRpt(_settings, _filters, _fields);
                File.WriteAllText(dlg.FileName, content, new UTF8Encoding(false));
                MessageBox.Show("Saved:\r\n" + dlg.FileName, "RPT Builder",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshPreview();
            }
        }

        // ── Attribute TreeView ─────────────────────────────────────────
        private TreeView BuildAttrTree()
        {
            var tv = new TreeView { Dock = DockStyle.Fill, ShowNodeToolTips = true };
            foreach (var cat in AttrLib.All)
            {
                var node = new TreeNode(cat.Name);
                foreach (var item in cat.Items)
                {
                    var child = new TreeNode(item.Attr + "  [" + item.Type + "]")
                    {
                        Tag = item.Attr,
                        ToolTipText = item.Hint,
                    };
                    node.Nodes.Add(child);
                }
                tv.Nodes.Add(node);
            }
            tv.ExpandAll();
            return tv;
        }

        // ── Helpers ────────────────────────────────────────────────────
        private static ComboBox MakeCombo(params string[] items)
        {
            var cb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var s in items) cb.Items.Add(s);
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            return cb;
        }

        private static NumericUpDown MakeNud(int min, int max, int val)
        {
            return new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, Value = val };
        }

        private static void AddRow(TableLayoutPanel tbl, string label, Control ctrl)
        {
            tbl.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill });
            tbl.Controls.Add(ctrl);
        }

        private static void SetCombo(ComboBox cb, string value)
        {
            int idx = cb.Items.IndexOf(value);
            cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
        }

        private static decimal Clamp(NumericUpDown nud, int value)
        {
            return Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
        }

        // ── Help text ──────────────────────────────────────────────────
        private const string HELP_TEXT =
@"═══════════════════════════════════════════════════════════════
  Tekla .RPT Template Builder — Help
═══════════════════════════════════════════════════════════════

INSTALLATION
────────────
1. Click 'Save .rpt…' and save the file.
2. Copy it to your Tekla model's attributes folder:
     <YourModel>\attributes\
   or to the global reference folder:
     %XSDATADIR%\environments\<env>\reference\
3. In Tekla Structures: Reports > Create Report.
   Your template appears in the list.

TABS
────
  Settings  — Template-level settings (sort type, width, etc.)
  Filters   — Rules to include / exclude parts from the report
  Fields    — Output columns and their value formulas
  Preview   — Live-generated .rpt source (updates on tab switch)
  Help      — This page

FILTER SYNTAX
─────────────
Each enabled filter rule contributes one condition.
Filter Logic = OR (default): if ANY exclude condition matches,
the part is excluded. AND: ALL exclude conditions must match.

Example rule output:
  if GetValue("MATERIAL") == "CONCRETE" ||
     match(GetValue("NAME"), "JOIST*")  ||
     GetValue("EXISTING") == "Yes" then
    StepOver()
  else
    Output()
  endif

Operators:
  ==      exact match
  !=      not equal
  match   wildcard  (* = any string)
  !match  wildcard not match
  >  <    numeric / string comparison

FORMULA MODES
─────────────
  Plain         GetValue("ATTR")
  Length Smart  Imperial/metric, grating-aware
  Weight Smart  Imperial/metric (lbf or kg)
  Custom        Write your own Tekla formula expression

Tekla formula functions:
  GetValue("ATTR")
  format(GetValue("WEIGHT"), "Weight", "kg", 2)
  match(GetValue("NAME"), "*GRTG*")
  if <cond> then <val> else <val> endif

CONFIRMED ATTRIBUTES  (from objects.inp)
────────────────────────────────────────
  PRELIM_MARK     EXISTING        BOUGHT_ITEM
  BOUGHT_ITEM_NAME NOTES2-5       ZONE
  SEQUENCE        SEQUENCE_NAME   LOT_NAME
  ERECTION_SEQ_NO HEAT_NUMBER     BATCH_NUMBER
  LOAD_NUMBER     BARCODEMARK     DRAWING_STATUS
  FAB_NAME        ASSEM_INST_STAT ASSEM_HOLD_STAT
  SS_CVN          SS_BOUGHT_ITEM  SS_COMMENT
  SS_PAINT        SS_CLEAN        SS_CAMBER_ROLL
  SS_LOT          SS_FACILITY     SS_E_DWG  SS_D_DWG

GUESSED / UNVERIFIED
────────────────────
  ASSEMBLY_POS, PART_POS        — assumed names
  oncombine AVERAGE / MIN / MAX  — not seen in samples
  GRAPHICAL template type        — only TEXTUAL confirmed
  Content types beyond PART      — ASSEMBLY etc. unverified

.RPT FORMAT CONSTANTS  (confirmed from two DTI sample files)
─────────────────────
  version  = 3.5
  fonttype = 2
  pen      = -1
";
    }
}
