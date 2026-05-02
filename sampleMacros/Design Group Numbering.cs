using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Tekla.Structures;
using TSM=Tekla.Structures.Model;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            string xsbin = "";
            TSM.Model CurrentModel = new TSM.Model();
            TeklaStructuresSettings.GetAdvancedOption("XSBIN", ref xsbin);
            string EnginePath = xsbin + "\\applications\\tekla\\Model\\DesignGroupNumbering\\DesignGroupNumbering.exe";

            Process NewProcess = new Process();

            if (File.Exists(EnginePath))
            {
                NewProcess.StartInfo.FileName = EnginePath;

                try
                {
                    NewProcess.Start();
                }
                catch
                {
                    MessageBox.Show("Starting of Design Group Numbering failed.");
                }
            }
            else
            {
                MessageBox.Show("Design Group Numbering not found.");
            }
        }
    }
}
