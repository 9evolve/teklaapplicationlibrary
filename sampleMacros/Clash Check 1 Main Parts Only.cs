using System.Windows.Forms;
using Tekla.Structures;

[assembly: Tekla.Technology.Scripting.Compiler.Reference("Tekla.Application.Library")]
namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(IScript akit)
        {
            akit.ValueChange("main_frame", "sel_all", "1");
            akit.ValueChange("main_frame", "sel_joints", "0");
            akit.ValueChange("main_frame", "sel_screws", "0");
            akit.ValueChange("main_frame", "select_objects_in_assemblies", "0");
            akit.ValueChange("main_frame", "select_assemblies", "0");
            akit.ValueChange("main_frame", "sel_objects_in_joints", "0");
            akit.ValueChange("main_frame", "sel_custom_objects", "0");
            if (MessageBox.Show(
                "Do you want to Run a Clash Check from Selected Parts?\n\nIf 'Yes', Select the Parts in the Model, and then press 'Yes'.\nPressing 'No' will run a clash on all parts in the model.",
                "Clash Check Selection Message", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                akit.Callback("acmdOpenClashCheckManager", "/runClashCheck", "main_frame");
            }
            else
            {
                akit.Callback("acmdSelectAll", "", "main_frame");
                akit.Callback("acmdOpenClashCheckManager", "/runClashCheck", "main_frame");
            }
        }
    }
}
