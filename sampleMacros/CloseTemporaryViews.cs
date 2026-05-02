using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
			Model myModel = new Model();
			ModelViewEnumerator ViewEnum = ViewHandler.GetTemporaryViews();
			while (ViewEnum.MoveNext())
			{
				Tekla.Structures.Model.UI.View View = ViewEnum.Current;
				ViewHandler.HideView(View);
			}
        }
    }
}
