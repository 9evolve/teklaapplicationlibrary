//
// ###########################################################################################
// ### Name          : Tekla Structures Macro
// ### Version       : 1.0 for V18.0
// ###               : Visual C# / Visual Studio 2010
// ### Created       : January 2012
// ### Modified      : 
// ### Author        : 
// ### Released      : 
// ### Comment       :
// ### Description   : 
// ### Copyright    : Tekla Corporation              
// ###                 
// ###########################################################################################
//

using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using TS3D = Tekla.Structures.Geometry3d;

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
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            try
            {
                new ExtendAdjustElevations(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message + ex.StackTrace);
            }
        }

        /// <summary>
        /// Internal method for debugging in console application
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                new ExtendAdjustElevations(null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message + ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Main logic of program to edit advanced options in the options.ini file of a model folder
    /// </summary>
    public class ExtendAdjustElevations
    {
        /// <summary>
        /// Main constructor class than instigates all logic for program
        /// </summary>
        public ExtendAdjustElevations(Tekla.Technology.Akit.IScript akit)
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
                    double Xdist = Beam1.StartPoint.X - Beam1.EndPoint.X;
                    double Ydist = Beam1.StartPoint.Y - Beam1.EndPoint.Y;
                    TS3D.Vector Vector1 = new TS3D.Vector(Xdist, Ydist, 0);

                    TS3D.GeometricPlane Plane1 = new GeometricPlane(Beam1.StartPoint, Vector1, new TS3D.Vector(0, 0, 1));

                    ModelObjectEnumerator Beams = Pick1.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, Prompt2);

                    while (Beams.MoveNext())
                    {
                        Beam Beam2 = Beams.Current as Beam;
                        if (Beam2 != null)
                        {

                            TS3D.Point IntersectPoint = TS3D.Intersection.LineToPlane(new Line(Beam2.StartPoint, Beam2.EndPoint), Plane1);
                            TS3D.LineSegment Lineseg1 = TS3D.Intersection.LineToLine(new Line(IntersectPoint, new TS3D.Point(IntersectPoint.X, IntersectPoint.Y, IntersectPoint.Z + 100)), new Line(Beam1.StartPoint, Beam1.EndPoint));
                            if (TS3D.Distance.PointToPoint(Beam2.StartPoint, Lineseg1.Point1) <= TS3D.Distance.PointToPoint(Beam2.EndPoint, Lineseg1.Point1))
                            {
                                Beam2.StartPoint = Lineseg1.Point1;
                                Beam2.Modify();
                            }
                            else
                            {
                                Beam2.EndPoint = Lineseg1.Point1;
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
