using System.Threading.Tasks;
using System.IO;
using Tekla.Structures.Model.UI;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            ModelViewEnumerator AllViews = ViewHandler.GetAllViews();

            while (AllViews.MoveNext())
            {
                View OneView = AllViews.Current;
                OneView.Select();
                OneView.DisplayType = 0;  // Display View Plane
                OneView.Modify();
            }
        }
    }
}
