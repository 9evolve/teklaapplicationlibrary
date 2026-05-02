#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633 // Unrecognized #pragma directive

[assembly: Tekla.Technology.Scripting.Compiler.Reference("Tekla.Application.Library")]

namespace UserMacros
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Forms;
    using Tekla.Macros.Akit;
    using Tekla.Structures;
    using Tekla.Structures.Drawing;
    using Tekla.Structures.Model;
    using Tekla.Structures.Model.Operations;
    using ModelObject = Tekla.Structures.Drawing.ModelObject;
    using Part = Tekla.Structures.Model.Part;
    
    public sealed class MacroOpenShopDrawing
    {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
        {
            try
            {
                if(runtime == null)
                    throw new ArgumentNullException("runtime");
                Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf = runtime.Get<Tekla.Macros.Wpf.Runtime.IWpfMacroHost>();
                IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();

                OpenShopDrawing.RunMainLogic(akit, wpf);
            }
            catch(Exception ex)
            {
                LogException(ex);
            }
        }

        private static void LogException(Exception ex)
        {
            if(ex == null)
            {
                return;
            }

            string mName = ex.Source;
            string mInfo = string.Format("Macro Runtime Failure: {0} failed to run properly.", mName);

            string msg = string.Empty + Environment.NewLine;
            msg += "====>" + mInfo;
            msg += ex.Message + Environment.NewLine;
            msg += ex.InnerException + Environment.NewLine;
            msg += ex.StackTrace + Environment.NewLine;
            msg += Environment.NewLine;

            Tekla.Structures.ModelInternal.Operation.dotWriteToSessionLog(msg);
            Tekla.Structures.Model.Operations.Operation.DisplayPrompt(mInfo);
            Trace.WriteLine(msg);
        }

        public static class OpenShopDrawing
        {
            private static List<SmartDrawing> SmartDrawingCache { get; set; }

            private static DrawingHandler Handler
            {
                get { return new DrawingHandler(); }
            }

            public static void RunMainLogic(IAkitScriptHost akit, Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf)
            {
                bool drawingOpened = false;

                //Get object from user
                Part pickedPart = GetPartToSearchForDrawingOf();
                if(pickedPart == null)
                {
                    ShowMessage("Failed to find object to use for drawing search.");
                    return;
                }

                //Check if numbering is up to date
                if(!Operation.IsNumberingUpToDate(pickedPart))
                {
                    ShowMessage("Numbering is not up to date, run numbering first.");
                    return;
                }

                //Cache drawings from model
                if(!CacheDrawings())
                {
                    ShowMessage("Macro failed to cache drawings.");
                    return;
                }

                //Get assembly mark for selected model object
                string assemblyMark = GetAssemblyMark(pickedPart);
                if(string.IsNullOrEmpty(assemblyMark))
                {
                    ShowMessage(string.Format("Failed to get ASSEMBLY_POS for part {0}", pickedPart.Identifier.GUID));
                    return;
                }

                List<SmartDrawing> foundDrawings = SmartDrawingCache.Where(f => f.AssemblyMark == assemblyMark).ToList();
                int qtyDrawing = foundDrawings.Count;
                if(qtyDrawing < 1)
                {
                    ShowMessage(string.Format("No drawings found for mark: {0}", assemblyMark));
                    return;
                }
                if(qtyDrawing == 1)
                {
                    SmartDrawing sd = foundDrawings[0];
                    sd.UpdateOpen(wpf);
                    return;
                }

                if(qtyDrawing >= 2)
                {
                    SmartDrawing sd1 = foundDrawings[0];
                    SmartDrawing sd2 = foundDrawings[1];

                    DialogResult result = MessageBox.Show(
                        TeklaStructures.MainWindow,
                        string.Format(
                            "Multiple Drawings found, \nClick Yes to open drawing {0}\n Click No to open drawing {1}, \nclick Cancel to abort.",
                            sd1.FingerPrint, sd2.FingerPrint),
                        "Open Drawing Choice",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);

                    switch(result)
                    {
                        case DialogResult.Yes:
                            if(!sd1.UpdateOpen(wpf))
                            {
                                ShowMessage("Unable to update drawing, cannot open out of date drawing.");
                            }
                            break;
                        case DialogResult.No:
                            if(!sd2.UpdateOpen(wpf))
                            {
                                ShowMessage("Unable to update drawing, cannot open out of date drawing.");
                            }
                            break;
                        default:
                            Operation.DisplayPrompt(string.Format("Drawing open macro aborted by user"));
                            return;
                    }
                    return;
                }

                Trace.WriteLine("Unknown quantity/type of drawings found, open macro aborted.");
            }

            private static string GetAssemblyMark(Tekla.Structures.Model.ModelObject mo)
            {
                string assemblyMark = string.Empty;
                mo.GetReportProperty("ASSEMBLY_POS", ref assemblyMark);
                return assemblyMark;
            }

            private static Part GetPartToSearchForDrawingOf()
            {
                //Check if anything is selected first, if so use
                ModelObjectEnumerator selectedObjs = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
                if(selectedObjs != null && selectedObjs.GetSize() > 0)
                {
                    while(selectedObjs.MoveNext())
                    {
                        Tekla.Structures.Model.ModelObject modelObj = selectedObjs.Current;
                        if(modelObj is Part)
                        {
                            return (Part)modelObj;
                        }

                        if(modelObj is Assembly)
                        {
                            Assembly assm = (Assembly)modelObj;
                            return assm.GetMainPart() as Part;
                        }
                    }
                }

                //Nothing is selected ask user to pick part
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                Part pickedPart =
                    picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART) as Part;
                return pickedPart;
            }

            private static bool CacheDrawings()
            {
                //Clear old data
                SmartDrawingCache = new List<SmartDrawing>();

                //Get all drawings
                DrawingEnumerator drawingCollection = Handler.GetDrawings();
                if(drawingCollection.GetSize() < 1)
                {
                    Operation.DisplayPrompt("No drawings are created yet.");
                    return false;
                }

                //cache drawings locally
                while (drawingCollection.MoveNext())
                {
                    var dg = drawingCollection.Current;
                    SmartDrawingCache.Add(new SmartDrawing(dg));
                }
                return true;
            }

            private static void ShowMessage(string msg)
            {
                MessageBox.Show(TeklaStructures.MainWindow, msg, "Open Drawing Macro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Operation.DisplayPrompt(msg);
            }
        }

        public class SmartDrawing
        {
            public DwgTypeEnum DwgType { get; set; }

            public Identifier Id { get; set; }

            public string AssemblyMark { get; set; }

            public string DwgMark { get; set; }

            public string FingerPrint
            {
                get { return string.Format("Type: {0}, Mark: {1}", DwgType, DwgMark); }
            }

            public Tekla.Structures.Model.ModelObject ModelObj { get; set; }

            public DrawingUpToDateStatus DwgStatus { get; set; }

            private Drawing BaseDrawing { get; set; }

            public SmartDrawing(Drawing dg)
            {
                //Set drawing type and Identifier
                if(dg is CastUnitDrawing)
                {
                    Id = ((CastUnitDrawing)dg).CastUnitIdentifier;
                    DwgType = DwgTypeEnum.CastUnit;
                }
                else if(dg is AssemblyDrawing)
                {
                    Id = ((AssemblyDrawing)dg).AssemblyIdentifier;
                    DwgType = DwgTypeEnum.Assembly;
                }
                else if(dg is SinglePartDrawing)
                {
                    Id = ((SinglePartDrawing)dg).PartIdentifier;
                    DwgType = DwgTypeEnum.SinglePart;
                }
                else
                {
                    Id = new Identifier();
                    DwgType = DwgTypeEnum.GeneralArrangement;
                    ModelObj = null;
                }

                //Set Mark and Model object if fabrication type drawing
                if(DwgType != DwgTypeEnum.GeneralArrangement)
                {
                    string tempMark = string.Empty;
                    ModelObj = new Model().SelectModelObject(Id);
                    ModelObj.GetReportProperty("ASSEMBLY_POS", ref tempMark);
                    AssemblyMark = tempMark;
                }
                DwgStatus = dg.UpToDateStatus;
                DwgMark = dg.Mark;
                BaseDrawing = dg;
            }

            public bool UpdateOpen(Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf)
            {
                if(DwgType == DwgTypeEnum.GeneralArrangement)
                    return new DrawingHandler().SetActiveDrawing(BaseDrawing);

                if (DwgStatus == DrawingUpToDateStatus.DrawingIsUpToDate ||
                    DwgStatus == DrawingUpToDateStatus.DrawingIsUpToDateButMayNeedChecking)
                {
                    return new DrawingHandler().SetActiveDrawing(BaseDrawing);
                }

                //Select related object in model for drawing
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var thisAssembly = new Model().SelectModelObject(Id);
                selector.Select(new ArrayList { thisAssembly });

                //Filter drawing list by selected, select 1st drawing, press update
                {
                    wpf.InvokeCommand("CommandRepository", "Drawing.DrawingList");
                    wpf.View("DocumentManager.MainWindow").Find("AID_DOCMAN_ButtonSelectDrawings").As.Button.Invoke();
                    wpf.View("DocumentManager.MainWindow").Find("AID_DOCMAN_ButtonUpdateDrawing").As.Button.Invoke();
                    wpf.View("DocumentManager.MainWindow").Find("AID_DOCMAN_ButtonOpen").As.Button.Invoke();
                }

                return new DrawingHandler().GetActiveDrawing() != null;
            }

            public enum DwgTypeEnum
            {
                CastUnit,
                Assembly,
                SinglePart,
                GeneralArrangement
            }
        }
    }
}