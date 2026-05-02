using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Geometry3d;

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
                ShowRebarCenterOfGravity.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class ShowRebarCenterOfGravity
    {
        private static CenterOfMass _centerOfMass;

        public static CenterOfMass Calculator
        {
            get
            {
                if (_centerOfMass == null) _centerOfMass = new CenterOfMass();
                return _centerOfMass;
            }
        }

        public static void RunMacro(IScript akit)
        {
            var selectedReinforcement = GetSelectedObjects();
            if (selectedReinforcement == null) return;

            var allObjectCenters = GetMassesForReinforcements(selectedReinforcement);
            var overallCenter = Calculator.GetCenter(allObjectCenters);

            //Show results
            //CreatePoint(overallCenter);
            var beam = CreatetDummyObject(overallCenter);
            PaintCenterPoint(overallCenter);

            //Select results
            if (beam == null) return;
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            selector.Select(new ArrayList { beam });
            Tekla.Structures.ModelInternal.Operation.dotStartAction("ZoomToSelected", null);
        }

        private static void PaintCenterPoint(Pointmass overallCenter)
        {
            var gd = new Tekla.Structures.Model.UI.GraphicsDrawer();
            var pt2 = new Point(overallCenter.Point);
            pt2.Translate(0, 0, 10);
            var ls = new LineSegment(overallCenter.Point, pt2);
            gd.DrawLineSegment(ls, new Tekla.Structures.Model.UI.Color(1.0, 0.875, 0.795));
        }

        private static Beam CreatetDummyObject(Pointmass overallCenter)
        {
            const double height = 6.0;
            var endPoint = new Point(overallCenter.Point);
            var startPoint = new Point(overallCenter.Point);
            endPoint.Translate(0, 0, height);
            startPoint.Translate(0, 0, -height);

            var bm = new Beam(Beam.BeamTypeEnum.COLUMN)
                         {
                             Class = "817",
                             StartPoint = overallCenter.Point,
                             EndPoint = endPoint,
                             Material = { MaterialString = "Steel_Undefined" },
                             Name = "DUMMY_POINT",
                             PartNumber = { Prefix = "DPS", StartNumber = -666 },
                             AssemblyNumber = { Prefix = "dps", StartNumber = -666 },
                             Position =
                                 {
                                     Depth = Position.DepthEnum.MIDDLE,
                                     Plane = Position.PlaneEnum.MIDDLE,
                                     Rotation = Position.RotationEnum.FRONT
                                 },
                             Profile = { ProfileString = height + "*" + height }
                         };
            bm.Insert();
            bm.SetUserProperty("CustomType", "RebarCenterOfGravity");
            bm.SetUserProperty("ShowInDrawings", "false");
            bm.SetUserProperty("ShowInReports", "false");
            new Model().CommitChanges();
            return bm;
        }

        private static List<Pointmass> GetMassesForReinforcements(IEnumerable<Reinforcement> rebars)
        {
            var massList = new List<Pointmass>();
            foreach (var rebar in rebars)
            {
                var rebarGeometries = rebar.GetRebarGeometries(true);
                massList.Add(GetCenterMass(rebarGeometries));
            }
            return massList;
        }

        private static Pointmass GetCenterMass(IEnumerable rebarGeometries)
        {
            var barCenters = new List<Pointmass>();
            foreach (RebarGeometry rg in rebarGeometries)
            {
                var realPoints = rg.Shape.Points;
                var centerThisBar = Calculator.GetCenter(realPoints.Cast<Point>().ToList());
                barCenters.Add(centerThisBar);
            }
            return Calculator.GetCenter(barCenters);
        }

        private static IEnumerable<Reinforcement> GetSelectedObjects()
        {
            var results = new List<Reinforcement>();
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            var selectedStuff = selector.GetSelectedObjects();
            foreach (ModelObject mo in selectedStuff)
            {
                if (!(mo is Reinforcement)) continue;
                results.Add(mo as Reinforcement);
            }
            return results;
        }
    }

    public class Pointmass
    {
        public Pointmass()
        {
            Point = new Point();
            Mass = 0;
        }

        public Point Point { get; set; }
        public double Mass { get; set; }

        public override string ToString()
        {
            return "Point = " + Point + "\n" + "Mass = " + Mass;
        }
    }

    /// <summary>
    /// Center of Mass.
    /// </summary>
    interface ICenterOfMass
    {
        /// <summary>
        /// Calculates the center of mass of given (rebar) polygon.
        /// <note>
        /// Assumptions: Rebars are uniform and of constant density.
        /// </note>
        /// </summary>
        /// <param name="polyline">List of rebar polygon points</param>
        /// <returns>Center point mass of given segments</returns>
        Pointmass GetCenter(IList<Point> polyline);

        Pointmass GetCenter(params Point[] points);

        /// <summary>
        /// Point mass.
        /// </summary>
        /// <param name="pointmasses">Point masses</param>
        /// <returns>Center point mass</returns>
        Pointmass GetCenter(IList<Pointmass> pointmasses);

        Pointmass GetCenter(params Pointmass[] pointmasses);
    }

    internal static class ShowRebarCenterOfGravityExtensions
    {
        public static Point GetCenter(this LineSegment segment)
        {
            if (segment == null) throw new ArgumentNullException();
            return new Point((segment.Point1.X + segment.Point2.X) / 2,
                             (segment.Point1.Y + segment.Point2.Y) / 2,
                             (segment.Point1.Z + segment.Point2.Z) / 2);
        }
    }

    internal class SegmentBar
    {
        private SegmentBar()
        { }

        private static SegmentBar FromSegment(LineSegment segment)
        {
            return new SegmentBar
            {
                Mass = segment.Length(),
                Segment = segment
            };
        }

        public static SegmentBar FromTwoPoints(Point point1, Point point2)
        {
            return FromSegment(new LineSegment(point1, point2));
        }

        public static IEnumerable<SegmentBar> FromListOfPoints(IList<Point> points)
        {
            if (points.Count < 2) throw new ArgumentException("Must give at least two points");

            for (var i = 1; i < points.Count; i++)
            {
                var segment = new LineSegment(points[i - 1], points[i]);
                yield return new SegmentBar { Mass = segment.Length(), Segment = segment };
            }
        }

        public Pointmass GetPointmass()
        {
            return new Pointmass { Mass = Mass, Point = Segment.GetCenter() };
        }

        private double Mass { get; set; }
        private LineSegment Segment { get; set; }
    }

    public class CenterOfMass: ICenterOfMass
    {
        public Pointmass GetCenter(IList<Point> polyline)
        {
            if (polyline == null) throw new ArgumentNullException();

            var bars = SegmentBar.FromListOfPoints(polyline);
            var total = bars.Select(segmentBar => segmentBar.GetPointmass()).ToList();
            return GetCenter(total);
        }

        public Pointmass GetCenter(params Point[] points)
        {
            if (points == null) throw new ArgumentNullException();
            return GetCenter(points.ToList());
        }

        public Pointmass GetCenter(IList<Pointmass> pointmasses)
        {
            if (pointmasses == null) throw new ArgumentNullException();

            var total = new Pointmass();
            foreach (var pointmass in pointmasses)total = Combine(total, pointmass);
            return total;
        }

        public Pointmass GetCenter(params Pointmass[] pointmasses)
        {
            if (pointmasses == null) throw new ArgumentNullException();
            return GetCenter(pointmasses.ToList());
        }

        private static Pointmass Combine(Pointmass pointmass1, Pointmass pointmass2)
        {
            if (pointmass1 == null || pointmass2 == null) throw new ArgumentNullException();

            var mass = pointmass1.Mass + pointmass2.Mass;
            if (Math.Abs(mass - 0) < 0.1) return new Pointmass();
            var xmass = (pointmass1.Point.X * pointmass1.Mass + pointmass2.Point.X * pointmass2.Mass) / mass;
            var ymass = (pointmass1.Point.Y * pointmass1.Mass + pointmass2.Point.Y * pointmass2.Mass) / mass;
            var zmass = (pointmass1.Point.Z * pointmass1.Mass + pointmass2.Point.Z * pointmass2.Mass) / mass;
            return new Pointmass { Mass = mass, Point = new Point(xmass, ymass, zmass) };
        }
    }
}
