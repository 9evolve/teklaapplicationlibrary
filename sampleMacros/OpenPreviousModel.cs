//by mihu
#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633 // Unrecognized #pragma directive

namespace UserMacros 
{
    using System;
    using System.IO;
    using Tekla.Structures;
    using Tekla.Structures.Model;

    public sealed class Macro {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {
            var model = new Model();
            var appdatafolder = Tekla.Structures.TeklaStructuresInfo.GetLocalAppDataFolder();
            var file = Path.Combine(appdatafolder, "UI", "Recent.xml");

            if (File.Exists(file))
            {
                string path = string.Empty;

                using (StreamReader reader = new StreamReader(file))
                {
                    string line;
                    bool thisone = false;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line.Trim();
                        if (line.Contains("<Entry>"))
                        {
                            if (!thisone)
                            {
                                thisone = true;
                            }
                            else
                            {
                                path = line.Replace("<Entry>", string.Empty).Replace("</Entry>", string.Empty).Trim();
                                break;
                            }

                        }
                    }
                }

                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var modelhandler = new Tekla.Structures.Model.ModelHandler();
                    modelhandler.Open(path);
                }
            }
        }
    }
}
