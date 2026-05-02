using Tekla.Structures.Model;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

// Macro to open the folder leading to User settings from inside Tekla structures... 
// If it fails to find the correct folder location it simply opens the documents folder instead. 

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            try
            {
                string MacroDirectory = System.Environment.GetEnvironmentVariable("XSUSERDATADIR");

                System.Diagnostics.Process Explorer = new System.Diagnostics.Process();
                Explorer.EnableRaisingEvents = false;
                Explorer.StartInfo.FileName = "explorer";
                Explorer.StartInfo.Arguments = @MacroDirectory;
                Explorer.Start();
             
            }
            catch
            {
                MessageBox.Show("Error - Cannot open Folder...");
            }
        }
    }
}