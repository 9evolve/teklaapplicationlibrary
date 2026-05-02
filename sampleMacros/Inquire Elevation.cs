using System;
using System.Diagnostics;
using Tekla.Structures;
using Tekla.Structures.Datatype;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;
using System.Xml;
[assembly: Tekla.Technology.Scripting.Compiler.ReferenceAttribute("Tekla.Application.Library")]
[assembly: Tekla.Technology.Scripting.Compiler.ReferenceAttribute("Tekla.Structures.Datatype")]
[assembly: Tekla.Technology.Scripting.Compiler.Reference("System.Xml")]
namespace Tekla.Technology.Akit.UserScript
{
    /// <summary>
    /// Internal class for running logic
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Internal method run automatically by Tekla Structures if using as raw c# file
        /// </summary>
        /// <param name="akit">Passed argument automatically by core when using as macro</param>
        public static void Run(IScript akit)
        {
            try
            {
                InquireElevation.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class InquireElevation
    {
        /// <summary> Prompt given to user for picking point </summary>
        public const string PickPrompt = "Pick point to get elevation of";

        public static void RunMacro(IScript akit)
        {
            var picker = new Picker();
            try
            {
                var preString = "Global Elevation: ";
                var pt = picker.PickPoint(PickPrompt);
                if (pt == null) return;
                TeklaStructures.Connect();
                if (TeklaStructures.Environment.UseUSImperialUnitsInInput)
                {
                    Distance.CurrentUnitType = Distance.UnitType.Foot;
                    var dist = new Distance(pt.Z + GetGlobalZOffset());
                    Operation.DisplayPrompt(preString + dist.ToFractionalFeetAndInchesString());
                }
                else
                {
                    Distance.CurrentUnitType = Distance.UnitType.Millimeter;
                    var dist = new Distance(pt.Z + GetGlobalZOffset());
                    Operation.DisplayPrompt(preString + dist.Millimeters.ToString("# mm"));
                }
            }
            catch (Exception)
            {
                //Needed for picker interrupt
            }
            finally
            {
                TeklaStructures.Disconnect();
            }
        }

        private static double GetGlobalZOffset()
        {
            var tempValue = 0.0;
            new Model().GetProjectInfo().GetUserProperty("PROJ_DATUM_Z", ref tempValue);
            return tempValue;
        }
    }
}
