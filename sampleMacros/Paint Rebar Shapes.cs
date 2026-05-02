using System;
using System.Diagnostics;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Tekla.Technology.Akit.UserScript
{
    /// <summary>
    /// Internal class for running logic
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Internal method run automatically by Tekla Structures if using as raw c# file
        /// </summary>
        /// <param name="akit">Passed argument automatically by core when using as macro</param>
        public static void Run(IScript akit)
        {
            try
            {
                PaintRebarShapes.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class PaintRebarShapes
    {
        public static void RunMacro(IScript akit)
        {
            var rebarObj = new Model().GetModelObjectSelector().GetAllObjectsWithType(new[] { (typeof(Reinforcement)) });
            while (rebarObj.MoveNext())
            {
                var rebar = rebarObj.Current as Reinforcement;
                if (rebar == null) continue;
                var firstBar = rebar.GetRebarGeometries(false)[0] as RebarGeometry;
                if (firstBar == null) continue;
                var center = firstBar.Shape.Points[0] as Point;
                if (center == null) continue;
                center.Translate(0, 70, 0);
                new Model().DrawText(rebar.GetShape(), center, new Color(1, 0, 0));
            }
        }
    }

    public static class PaintRebarShapesExtensions
    {
        /// <summary> Report property name </summary>
        public const string RebarShapeReportName = "SHAPE";

        /// <summary>
        /// Draws text in the model view
        /// </summary>
        /// <param name="model"></param>
        /// <param name="text">Text to draw</param>
        /// <param name="tPoint">Point to insert as base of text</param>
        /// <param name="tColor">Color to make text</param>
        public static void DrawText(this Model model, string text, Point tPoint, Color tColor)
        {
            DrawText(text, tPoint, tColor);
        }
        /// <summary>
        /// Draws text in the model view
        /// </summary>
        /// <param name="text">Text to draw</param>
        /// <param name="tPoint">Point to insert as base of text</param>
        /// <param name="tColor">Color to make text</param>
        public static void DrawText(string text, Point tPoint, Color tColor)
        {
            var gd = new GraphicsDrawer();
            gd.DrawText(tPoint, text, tColor);
        }

        /// <summary>
        /// Returns extneral shape string
        /// </summary>
        /// <param name="rebar"></param>
        /// <returns></returns>
        public static string GetShape(this Reinforcement rebar)
        {
            if (rebar == null) throw new ArgumentNullException();
            var result = String.Empty;
            rebar.GetReportProperty(RebarShapeReportName, ref result);
            return result;
        }
    }
}
