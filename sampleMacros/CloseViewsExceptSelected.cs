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
            ModelViewEnumerator ViewSelectedEnum = ViewHandler.GetSelectedViews();
            ModelViewEnumerator ViewEnum = ViewHandler.GetVisibleViews();

            Tekla.Structures.Model.UI.View ViewSelected = new Tekla.Structures.Model.UI.View();

            while (ViewSelectedEnum.MoveNext())
            {
                ViewSelected = ViewSelectedEnum.Current;
            }

			while (ViewEnum.MoveNext())
			{
				Tekla.Structures.Model.UI.View ViewVisible = ViewEnum.Current;

                if (ViewVisible.Identifier.ID.ToString() != ViewSelected.Identifier.ID.ToString())
                    ViewHandler.HideView(ViewVisible);
			}
        }
    }
}
