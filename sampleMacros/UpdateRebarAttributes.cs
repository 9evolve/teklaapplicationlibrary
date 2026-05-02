using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Tekla.Structures;
using Tekla.Structures.Model;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public const string ApplicationName = "UpdateRebarAttributes.exe";

        public static void Run(Tekla.Technology.Akit.IScript akit)
        {

            var xsbin = string.Empty;
            var model = new Tekla.Structures.Model.Model();
            TeklaStructuresSettings.GetAdvancedOption("XSBIN", ref xsbin);
            var enginePath = Path.Combine(xsbin, "applications\\tekla\\Model\\RebarCouplers\\" + ApplicationName);
            var newProcess = new Process();

            if (File.Exists(enginePath))
            {
                newProcess.StartInfo.FileName = enginePath;

                try
                {
                    newProcess.Start();
                }
                catch
                {
                    MessageBox.Show("Starting " + ApplicationName + " failed.");
                }
            }
            else
            {
                MessageBox.Show(ApplicationName + " not found.");
            }
        }
    }
}