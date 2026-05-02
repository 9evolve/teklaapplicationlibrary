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
                ShowCoordinateSystem.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class ShowCoordinateSystem
    {
        /// <summary>
        /// Length of each axis vector
        /// </summary>
        private const int VectorLength = 500;

        /// <summary>
        /// Method that paints current coordinate system
        /// </summary>
        public static void RunMacro(IScript akit)
        {
            //Get object from user
            var pickedObject = PickObjectFromModel();
            if (pickedObject == null) return;
            //pickedObject.Select();

            //Setup coordinate system from object
            var coordSys = pickedObject.GetCoordinateSystem();
            var xAxisEndPt = new Point(coordSys.Origin);
            var yAxisEndPt = new Point(coordSys.Origin);
            var zAxisEndPt = new Point(coordSys.Origin);
            xAxisEndPt.Translate(RoundVector(coordSys.AxisX.GetNormal()) * VectorLength);
            yAxisEndPt.Translate(RoundVector(coordSys.AxisY.GetNormal()) * VectorLength);
            zAxisEndPt.Translate(RoundVector(Vector.Cross(coordSys.AxisX, coordSys.AxisY).GetNormal()) * VectorLength);

            //Paint axis of coordinate sytem
            var gd = new GraphicsDrawer();
            gd.DrawLineSegment(coordSys.Origin, xAxisEndPt, Red);
            gd.DrawLineSegment(coordSys.Origin, yAxisEndPt, Green);
            gd.DrawLineSegment(coordSys.Origin, zAxisEndPt, Blue);
        }

        /// <summary>
        /// Gets single model object from model safely
        /// </summary>
        /// <param name="prompt">Prompt to show user</param>
        /// <returns>Null if user interrupted</returns>
        private static ModelObject PickObjectFromModel(string prompt = "Pick object from model")
        {
            try
            {
                var picker = new Picker();
                return picker.PickObject(Picker.PickObjectEnum.PICK_ONE_OBJECT, prompt);
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("interrupt")) return null;
                throw;
            }
        }

        private static void Translate(this Point pt, Vector v)
        {
            pt.Translate(v.X, v.Y, v.Z);
        }

        public static Color Red { get { return new Color(1.0, 0.0, 0.0); } }
        public static Color Blue { get { return new Color(0.0, 0.0, 1.0); } }
        public static Color Green { get { return new Color(0.0, 1.0, 0.0); } }

        /// <summary>
        /// Rounds vector to five decimal places for each leg
        /// </summary>
        /// <param name="vector">Vector that needs rounding</param>
        /// <returns>Resulting rounded vector</returns>
        private static Vector RoundVector(Point vector)
        {
            var result = new Vector
            {
                X = Math.Round(vector.X, 5),
                Y = Math.Round(vector.Y, 5),
                Z = Math.Round(vector.Z, 5)
            };
            return result;
        }
    }
}
