//
// ###########################################################################################
// ### Name          : Load ShipLadder Steps
// ### Version       : 1.0 for V18.0
// ###               : Visual C# / Visual Studio 2010
// ### Created       : February 2012
// ### Modified      : 
// ### Author        : Charles Pool
// ### Released      : 
// ### Comment       :
// ### Description   : Launches external excutable file
// ###                 
// ###                 
// ###########################################################################################
//

using Tekla.Structures;
using System.IO;
using System.Diagnostics;

namespace Tekla.Technology.Akit.UserScript
{
    /// <summary>
    /// Tekla Structures required class
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Name of external model application to launch
        /// </summary>
        const string ApplicationName = "Steps.exe";

        /// <summary>
        /// Main code to launch external application
        /// </summary>
        /// <param name="akit">Internal dialog kit</param>
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            new LoadExternalApplication(ApplicationName);
        }

        /// <summary>
        /// Internal method for debugging in console application
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            new LoadExternalApplication(ApplicationName);
        }
    }

    public class LoadExternalApplication
    {
        /// <summary>
        /// Subfolder to find executable in
        /// </summary>
        private const string ApplicationSubPath = "applications\\Tekla\\Model\\Steps";

        /// <summary>
        /// Main code that launches external application
        /// </summary>
        /// <param name="applicationName">Exact name of application file</param>
        public LoadExternalApplication(string applicationName)
        {
            //Get root path for Tekla Structures bin folder
            var xsbin = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XSBIN", ref xsbin);
            if (string.IsNullOrEmpty(xsbin))
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                    "XSBIN Variable not found, failed to launch application.");
                return;
            }

            //Check that application directory root exists
            var applicationDirectory = System.IO.Path.Combine(xsbin, ApplicationSubPath);
            if (!Directory.Exists(applicationDirectory))
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(applicationDirectory
                    + " directory does not exist, failed to launch application");
                return;
            }

            //Check that file exists before starting
            var applicationPath = System.IO.Path.Combine(applicationDirectory, applicationName);
            if (!File.Exists(applicationPath))
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(applicationPath
                    + " file does not exist, failed to launch application");
                return;
            }

            //Start external application
            var externalApplication = new Process { StartInfo = { FileName = applicationPath } };
            if (externalApplication.Start())
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(applicationName + " successfully started.");
            else
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(applicationName + " unable to be started.");
        }
    }
}