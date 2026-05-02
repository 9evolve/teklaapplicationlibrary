using Tekla.Structures;
using Tekla.Structures.Model;
using System.Collections.Generic;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            Dictionary<string, string> localeLang = new Dictionary<string, string>
            {
                { "CZECH", "cs-CZ" },
                { "GERMAN", "de-DE" },
                { "ENGLISH", "en-US" },
                { "SPANISH", "es-ES" },
                { "FRENCH", "fr-FR" },
                { "HUNGARIAN", "hu-HU" },
                { "ITALIAN", "it-IT" },
                { "JAPANESE", "ja-JP" },
                { "KOREAN", "ko-KR" },
                { "DUTCH", "nl-NL" },
                { "POLISH", "pl-PL" },
                { "PORTUGUESE BRAZILIAN", "pt-BR" },
                { "PORTUGUESE", "pt-PT" },
                { "RUSSIAN", "ru-RU" },
                { "CHINESE SIMPLIFIED", "zh-Hans" },
                { "CHINESE TRADITIONAL", "zh-Hant" }
            };

            string XS_Variable = System.Environment.GetEnvironmentVariable("XSBIN");

            string XS_Language = System.Environment.GetEnvironmentVariable("XS_LANGUAGE");
            string Locale = "en-US";
            if(localeLang.ContainsKey(XS_Language))
            {
                Locale = localeLang[XS_Language];
            }

            string TS_Plugin = @"\applications\tekla\Model\ShapeCleaner\";
            string TS_Application = "ShapeCleaner.exe";

            if (System.IO.File.Exists(XS_Variable + TS_Plugin + TS_Application))
            {
                System.Diagnostics.Process Process = new System.Diagnostics.Process();
                Process.EnableRaisingEvents = false;
                Process.StartInfo.FileName = XS_Variable + TS_Plugin + TS_Application;

                Tekla.Structures.Model.Model MyModel = new Model();
                Tekla.Structures.Model.ModelInfo modelInfo = MyModel.GetInfo();
                var PathToShapeGeometries = "\"" + modelInfo.ModelPath + "\\ShapeGeometries" + "\"";

                Process.StartInfo.Arguments = string.Format("-d {0} -l {1}", PathToShapeGeometries, Locale);
                Process.Start();
                Process.Close();
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(TS_Application + " not found, application stopped!\n\nCheck the files in " + XS_Variable + TS_Plugin, "Tekla Structures", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}