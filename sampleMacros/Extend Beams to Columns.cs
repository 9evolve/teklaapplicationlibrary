using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using TSMUI = Tekla.Structures.Model.UI;
using T3D = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

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
                ExtendBeamsToColumns.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public class ExtendBeamsToColumns
    {
        public static void RunMacro(IScript akit)
        {
            string ExtendFilterName = "BEAM";
            double ExtendTolerance = 400;
            //string UDAName = "USER_FIELD_1";

            Model _model = new Model();
            TransformationPlane CurrPlane = _model.GetWorkPlaneHandler().GetCurrentTransformationPlane();
            _model.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());

            TSMUI.ModelObjectSelector Selector = new TSMUI.ModelObjectSelector();
            ModelObjectEnumerator Columns = Selector.GetSelectedObjects();

            TSM.Operations.Operation.DisplayPrompt("Beams are being extended or trimmed to the selected columns.");

            while (Columns.MoveNext())
            {
                TSM.Beam Column = Columns.Current as TSM.Beam;
                if (Column != null)
                {
                    GeometricPlane YPlane = new GeometricPlane(Column.GetCoordinateSystem().Origin, Column.GetCoordinateSystem().AxisX, Column.GetCoordinateSystem().AxisY);
                    GeometricPlane ZPlane = new GeometricPlane(Column.GetCoordinateSystem().Origin, Column.GetCoordinateSystem().AxisX, Column.GetCoordinateSystem().AxisX.Cross(Column.GetCoordinateSystem().AxisY));
                    T3D.Point ColumnXYPoint = new T3D.Point(Column.StartPoint.X, Column.StartPoint.Y);

                    T3D.Point MaxPoint = Column.GetSolid().MaximumPoint;
                    T3D.Point MinPoint = Column.GetSolid().MinimumPoint;

                    T3D.Point ColMinPoint = new T3D.Point(MinPoint.X - ExtendTolerance,
                        MinPoint.Y - ExtendTolerance,
                        MinPoint.Z - ExtendTolerance);

                    T3D.Point ColMaxPoint = new T3D.Point(MaxPoint.X + ExtendTolerance,
                        MaxPoint.Y + ExtendTolerance,
                        MaxPoint.Z + ExtendTolerance);

                    ModelObjectEnumerator RoundStuff = _model.GetModelObjectSelector().GetObjectsByBoundingBox(ColMinPoint, ColMaxPoint);
                    while (RoundStuff.MoveNext())
                    {
                        Beam ThisBeam = null;
                        try
                        {
                            ThisBeam = RoundStuff.Current as TSM.Beam;
                        }
                        catch
                        {

                        }
                        if (ThisBeam != null)
                        {
                            if (ThisBeam.Name == ExtendFilterName)
                            {
                                Line BeamLine = new Line(ThisBeam.StartPoint, ThisBeam.EndPoint);
                                T3D.Point YPoint = T3D.Intersection.LineToPlane(BeamLine, YPlane);
                                T3D.Point ZPoint = T3D.Intersection.LineToPlane(BeamLine, ZPlane);

                                double StartDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(ThisBeam.StartPoint.X, ThisBeam.StartPoint.Y));
                                double EndDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(ThisBeam.EndPoint.X, ThisBeam.EndPoint.Y));
                                if (StartDist < EndDist)
                                {
                                    if (YPoint == null && ZPoint != null)
                                    {
                                        ThisBeam.StartPoint = ZPoint;
                                        //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                        ThisBeam.Modify();
                                    }
                                    else if (ZPoint == null && YPoint != null)
                                    {
                                        ThisBeam.StartPoint = YPoint;
                                        //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                        ThisBeam.Modify();
                                    }
                                    else if (ZPoint != null && YPoint != null)
                                    {
                                        double YDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(YPoint.X, YPoint.Y));
                                        double ZDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(ZPoint.X, ZPoint.Y));
                                        if (YDist < EndDist)
                                        {
                                            ThisBeam.StartPoint = YPoint;
                                            //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                            ThisBeam.Modify();
                                        }
                                        else
                                        {
                                            ThisBeam.StartPoint = ZPoint;
                                            //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                            ThisBeam.Modify();
                                        }
                                    }

                                }
                                else
                                {
                                    if (YPoint == null && ZPoint != null)
                                    {
                                        ThisBeam.EndPoint = ZPoint;
                                        //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                        ThisBeam.Modify();
                                    }
                                    else if (ZPoint == null && YPoint != null)
                                    {
                                        ThisBeam.EndPoint = YPoint;
                                        //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                        ThisBeam.Modify();
                                    }
                                    else if (ZPoint != null && YPoint != null)
                                    {
                                        double YDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(YPoint.X, YPoint.Y));
                                        double ZDist = T3D.Distance.PointToPoint(ColumnXYPoint, new T3D.Point(ZPoint.X, ZPoint.Y));
                                        if (YDist < EndDist)
                                        {
                                            ThisBeam.EndPoint = YPoint;
                                            //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                            ThisBeam.Modify();
                                        }
                                        else
                                        {
                                            ThisBeam.EndPoint = ZPoint;
                                            //ThisBeam.SetUserProperty(UDAName, "FIXED");
                                            ThisBeam.Modify();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                _model.CommitChanges();
                TSM.Operations.Operation.DisplayPrompt("Beams have been extended to selected columns.");
                //TSMUI.ViewHandler.SetRepresentation("Extend_Fixed");
            }
        }
    }
}
