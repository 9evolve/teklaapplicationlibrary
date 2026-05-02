using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using TS3D = Tekla.Structures.Geometry3d;

//Keep: Tekla.Technology.Akit.UserScript
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
                Extend.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class Extend
    {
        public static void RunMacro(IScript akit)
        {
            string Prompt1 = "Pick the Primary Part";
            string Prompt2 = "Pick the Secondary Parts, then Press the Middle Mouse Button When Done";
            Model Model1 = new Model();
            Tekla.Structures.Model.UI.Picker Pick1 = new Tekla.Structures.Model.UI.Picker();
            bool keepgoing = true;
            while (keepgoing)
            {
                try
                {
                    Beam Beam1 = Pick1.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, Prompt1) as Beam;
                    TS3D.Point PointX = new TS3D.Point(Beam1.StartPoint.X, Beam1.StartPoint.Y, 0);
                    TS3D.Point PointY = new TS3D.Point(Beam1.EndPoint.X, Beam1.EndPoint.Y, 0);
                    double Xdist = Beam1.StartPoint.X - Beam1.EndPoint.X;
                    double Ydist = Beam1.StartPoint.Y - Beam1.EndPoint.Y;
                    TS3D.Vector Vector1 = new TS3D.Vector(Xdist, Ydist, 0);
                    GeometricPlane Plane1 = new GeometricPlane();
                    GeometricPlane Plane2 = new GeometricPlane();
                    //bool main_is_vertical = false;
                    if (TS3D.Distance.PointToPoint(PointX, PointY) < 2)
                    {
                        //main_is_vertical = true;
                        Plane1 = new GeometricPlane(Beam1.StartPoint, Beam1.GetCoordinateSystem().AxisX, Beam1.GetCoordinateSystem().AxisY);
                        Plane2 = new GeometricPlane(Beam1.StartPoint, Beam1.GetCoordinateSystem().AxisX, Beam1.GetCoordinateSystem().AxisY.Cross(Beam1.GetCoordinateSystem().AxisX));
                    }
                    else
                    {
                        Plane1 = new GeometricPlane(Beam1.StartPoint, Vector1, new TS3D.Vector(0, 0, 1));
                    }

                    ModelObjectEnumerator Beams = Pick1.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, Prompt2);

                    while (Beams.MoveNext())
                    {
                        Beam Beam2 = Beams.Current as Beam;
                        if (Beam2 != null)
                        {
                            TS3D.Point IntersectPoint = TS3D.Intersection.LineToPlane(new Line(Beam2.StartPoint, Beam2.EndPoint), Plane1);
                            if (IntersectPoint == null)
                            {
                                IntersectPoint = TS3D.Intersection.LineToPlane(new Line(Beam2.StartPoint, Beam2.EndPoint), Plane2);
                            }
                            if (TS3D.Distance.PointToPoint(Beam2.StartPoint, IntersectPoint) <= TS3D.Distance.PointToPoint(Beam2.EndPoint, IntersectPoint))
                            {
                                Beam2.StartPoint = IntersectPoint;
                                Beam2.Modify();
                            }
                            else
                            {
                                Beam2.EndPoint = IntersectPoint;
                                Beam2.Modify();
                            }
                        }
                    }
                    Model1.CommitChanges();
                }
                catch
                {
                    keepgoing = false;
                }
            }
        }
    }
}
