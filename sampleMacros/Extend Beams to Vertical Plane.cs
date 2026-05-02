using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model.UI;
using TSMUI = Tekla.Structures.Model.UI;
using T3D = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using System.Collections;

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
                ExtendBeamsToVerticalPlane.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class ExtendBeamsToVerticalPlane
    {
        public static void RunMacro(IScript akit)
        {
            Model _Model = new Model();

            TSMUI.Picker _Picker = new Picker();

            bool keepgoing = true;
            while (keepgoing)
            {
                try
                {
                    ArrayList PointsPicked = _Picker.PickPoints(Picker.PickPointEnum.PICK_TWO_POINTS);
                    T3D.Point FirstPoint = (T3D.Point)PointsPicked[0];
                    T3D.Point SecondPoint = (T3D.Point)PointsPicked[1];

                    double Xdist = SecondPoint.X - FirstPoint.X;
                    double Ydist = SecondPoint.Y - FirstPoint.Y;
                    Vector Xvector = new Vector(Xdist, Ydist, 0);
                    Vector Yvector = new Vector(0, 0, 1);
                    T3D.GeometricPlane IntersectionPlane = new GeometricPlane(FirstPoint, Xvector, Yvector);

                    TSMUI.ModelObjectSelector Selector = new TSMUI.ModelObjectSelector();
                    ModelObjectEnumerator SelectedStuff = Selector.GetSelectedObjects();

                    TSM.Operations.Operation.DisplayPrompt("Beams are being extended or trimmed to the picked vertical plane.");

                    while (SelectedStuff.MoveNext())
                    {
                        Beam CurrentItem = SelectedStuff.Current as Beam;
                        if (CurrentItem != null)
                        {
                            T3D.Line ThisBeamLine = new Line(CurrentItem.StartPoint, CurrentItem.EndPoint);
                            T3D.Point IntersectPoint = T3D.Intersection.LineToPlane(ThisBeamLine, IntersectionPlane);

                            double StartDistance = T3D.Distance.PointToPoint(IntersectPoint, CurrentItem.StartPoint);
                            double EndDistance = T3D.Distance.PointToPoint(IntersectPoint, CurrentItem.EndPoint);

                            if (StartDistance <= EndDistance)
                            {
                                CurrentItem.StartPoint = IntersectPoint;

                            }
                            else
                            {
                                CurrentItem.EndPoint = IntersectPoint;
                            }

                            CurrentItem.Modify();

                        }
                    }

                    _Model.CommitChanges();
                    TSM.Operations.Operation.DisplayPrompt("Beams have been extended or trimmed to the picked vertical plane.");
                }
                catch
                {
                    keepgoing = false;
                }
            }
            
        }
    }
}
