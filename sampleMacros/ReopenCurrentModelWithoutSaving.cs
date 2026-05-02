#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633 // Unrecognized #pragma directive

namespace UserMacros 
{
    using Tekla.Structures.Model;
    public sealed class Macro {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {
            var model = new Model();
            string path = model.GetInfo().ModelPath;
            var modelhandler = new Tekla.Structures.Model.ModelHandler();
            modelhandler.Open(path);
        }
    }
}
