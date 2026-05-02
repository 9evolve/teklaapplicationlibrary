using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

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
                TraceRebar.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }
    public static class TraceRebar
    {
        private static bool FollowCurves = false;
        private static string RebarProfilePrefix = "D";
        private static string RebarMaterial = "Steel_Undefined";
        private static int RebarClass = 13;

        public static void RunMacro(IScript akit)
        {
            Structures.Model.Operations.Operation.DisplayPrompt("Trace Rebar started.");

            //Get selected objects
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            var selectedModelObjects = selector.GetSelectedObjects();

            //Cycle through selected objects
            while (selectedModelObjects.MoveNext())
            {
                //Cast beam
                if (!(selectedModelObjects.Current is Reinforcement)) continue;
                var reinforcement = (Reinforcement)selectedModelObjects.Current;
                var bar = reinforcement.GetSingleRebar(0, true);

                //Get list of points
                var pts = bar.Shape.Points;
                var ptsList = (from object pt in pts select pt as Point).ToList();

                //Get list of bending radii
                var radii = bar.BendingRadiuses;
                var radList = radii.Cast<double>().ToList();

                CreatePolybeam(bar, radList, ptsList);
            }

            Structures.Model.Operations.Operation.DisplayPrompt("Trace Rebar finished.");
            new Model().CommitChanges();
        }

        /// <summary>
        /// Creates polybeam based on single rebar geometry
        /// </summary>
        /// <param name="bar">The single rebar.</param>
        /// <param name="radiiList">The list of bending radii.</param>
        /// <param name="pointsList">The list of points.</param>
        /// <returns>True if polybeam created; false otherwise.</returns>
        public static bool CreatePolybeam(RebarGeometry bar, List<double> radiiList, List<Point> pointsList)
        {
            if (pointsList.Count < 3) return false;  //need different method to create two point beam

            var pbeam = new PolyBeam
            {
                Profile = { ProfileString = RebarProfilePrefix + bar.Diameter },
                Material = { MaterialString = RebarMaterial },
                Position = { Depth = Position.DepthEnum.MIDDLE,
                             Plane = Position.PlaneEnum.MIDDLE },
                Class = RebarClass.ToString(),
            };

            pbeam.AddContourPoint(new ContourPoint(pointsList[0], null));
            Chamfer bend = null;
            for (var i = 1; i < pointsList.Count - 1; i++)
            {
                if (i - 1 < radiiList.Count)
                {
                    bend = FollowCurves ? new Chamfer(radiiList[i - 1] + bar.Diameter * 0.5, 0.0, Chamfer.ChamferTypeEnum.CHAMFER_ROUNDING) :
                        new Chamfer(radiiList[i - 1] + bar.Diameter * 0.5, 0.0, Chamfer.ChamferTypeEnum.CHAMFER_NONE);
                }
                pbeam.AddContourPoint(new ContourPoint(pointsList[i], bend));
                bend = null;
            }
            pbeam.AddContourPoint(new ContourPoint(pointsList[pointsList.Count - 1], null));

            return pbeam.Insert();
        }
    }
}
