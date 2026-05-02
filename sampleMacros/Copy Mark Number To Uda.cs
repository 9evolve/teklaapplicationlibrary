using System;
using System.Diagnostics;
using Tekla.Structures.Model;

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
                CopyMarkNumberToUda.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Main logic of program to edit advanced options in the options.ini file of a model folder
    /// </summary>
    public static class CopyMarkNumberToUda
    {
        /// <summary>
        /// Exact Uda name to copy mark number into
        /// </summary>
        private const string UdaNameToCopyValueIn = "PRELIM_MARK";

        /// <summary>
        /// Main constructor class than instigates all logic for program
        /// </summary>
        public static void RunMacro(IScript akit)
        {
            Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Starting to copy marks from assemblies to uda fields.");

            //Get all assemblies
            var modelAssemblies = new Model().GetModelObjectSelector().GetAllObjectsWithType(new[] { (typeof(Assembly)) });
            var totalNumberOfAssemblies = modelAssemblies.GetSize();
            var currentAssemblyCount = 0;

            //For each assembly copy marks
            foreach (Assembly modelAss in modelAssemblies)
            {
                //Skip over if not specific type, E.g. Precast
                if (modelAss.GetAssemblyType() == Assembly.AssemblyTypeEnum.STEEL_ASSEMBLY)
                {
                    UpdateProgress(totalNumberOfAssemblies, ref currentAssemblyCount);
                    continue;
                }

                //Get current mark
                var currentMark = string.Empty;
                modelAss.GetReportProperty("ASSEMBLY_POS", ref currentMark);

                //Store current mark
                modelAss.SetUserProperty(UdaNameToCopyValueIn, currentMark);
                modelAss.GetMainPart().SetUserProperty(UdaNameToCopyValueIn, currentMark);

                //Inform user to progress
                UpdateProgress(totalNumberOfAssemblies, ref currentAssemblyCount);
            }

            //Inform user process is done
            Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Copying assembly marks to uda fields, done.");
            new Model().CommitChanges();
        }

        /// <summary>
        /// Updates progress to user
        /// </summary>
        /// <param name="totalNumberOfAssemblies">Total number started with</param>
        /// <param name="currentAssemblyCount">Current number cycle is on</param>
        private static void UpdateProgress(int totalNumberOfAssemblies, ref int currentAssemblyCount)
        {
            currentAssemblyCount++;
            var percentComplete = (Convert.ToDouble(currentAssemblyCount) / Convert.ToDouble(totalNumberOfAssemblies)) * 100;
            Tekla.Structures.Model.Operations.Operation.DisplayPrompt(string.Format("Copying marks: {0:0.##} % complete", percentComplete));
        }
    }
}
