using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

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
                ClearControlNumbers.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class ClearControlNumbers
    {
        public static void RunMacro(IScript akit)
        {
            int counter = 0;

            //Get selected objects and put them in an enumerator/container
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            var myEnum = selector.GetSelectedObjects();

            //Cycle through selected objects
            while (myEnum.MoveNext())
            {
                //Cast part or assembly control number
                if (myEnum.Current is Assembly)
                {
                    if (ClearControlNumber(myEnum.Current)) counter++;
                }
                else
                {
                    if (!(myEnum.Current is Part)) continue;
                    if (ClearControlNumber(myEnum.Current)) counter++;
                }
            }
            Operation.DisplayPrompt(string.Format("{0} Control Numbers cleared.", counter));
        }

        private static bool ClearControlNumber(ModelObject myBeam)
        {
            //Local constant values
            if (myBeam == null) return false;
            const string controlNumberUdaName = "ACN";
            const int nullIntegerValue = -2147483648;

            //Get existing value
            var oldValue = nullIntegerValue;
            myBeam.GetUserProperty(controlNumberUdaName, ref oldValue);

            //If existing value is not null, set to null int value
            if (oldValue < 1) return false;
            return myBeam.SetUserProperty(controlNumberUdaName, nullIntegerValue);
        }
    }
}
