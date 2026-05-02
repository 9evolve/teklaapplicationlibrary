#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Akit5"
#pragma reference "System.Xml"
#pragma reference "Fusion"
#pragma reference "Tekla.Structures"
#pragma reference "Tekla.Structures.Model"
#pragma warning restore 1633 // Unrecognized #pragma directive

using Fusion;
using Fusion.Formatting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures;
using Tekla.Structures.Model.Operations;
using Vector = Tekla.Structures.Geometry3d.Vector;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(IScript akit)
        {
            try
            {
                CreateDimensionSet.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class CreateDimensionSet
    {
        //User prompt string for picking points
        private const string PickPrompt = "Pick points to dimension, middle mouse button to end.";
        private const string PickLeaderPosition = "Pick point to place dimension.";

        /// <summary> Color for Dimension lines in model </summary>
        private static readonly Color DimColor = new Color(1, 0, 1);

        /// <summary> Color for Dimension text in model </summary>
        private static readonly Color TextColor = new Color(0, 0, 0);

        /// <summary>
        /// Call to run create dimension logic
        /// </summary>
        /// <param name="akit">Akit for scripting if needed</param>
        public static void RunMacro(IScript akit)
        {
            //Prompt user for points to dimension
            var pts = PickerService.PickAPolygon(PickPrompt);
            if (pts == null || pts.Points.Count < 2) return;

            //Prompt user for point to place dimension
            var leaderPt = PickerService.PickAPoint(PickLeaderPosition);
            if (leaderPt == null) return;

            //Derive dimension coordinte system
            var point1 = pts.Points[0] as Point;
            var lastPt = pts.Points[pts.Points.Count - 1] as Point;
            if (point1 == null || lastPt == null) return;
            var yAxis = new Vector(0, 1, 0);
            var xAxis = new Vector(1, 0, 0);
            if (leaderPt.X < point1.X) xAxis *= 1;   //elkl: what does this do? anything times 1 is the same?
            var wp = new CoordinateSystem(point1, xAxis, yAxis);

            //Call to create dimension graphics in model
            var modelDim = new DimensionSet(pts.Points.Cast<Point>().ToList(), leaderPt, wp, DimColor, TextColor);
            if (modelDim.Create()) return;

            //If failed to create, inform user
            Trace.WriteLine("Unable to create model dimension set.");
            Operation.DisplayPrompt("Unable to create model dimension set.");
        }
    }

    /// <summary>
    /// Model graphic dimensioning tool set
    /// </summary>
    public class DimensionSet
    {
        private readonly List<Point> _pts;
        private readonly Color _primaryColor;
        private readonly Color _secondaryColor;
        private readonly Point _leaderPoint;
        private readonly CoordinateSystem _coordinateSystem;
        private const double DefaultOffsetDistance = 500.0;

        private const double TickLength = 100.0;
        private const double TickAngle = 45.0;
        private const double LeaderExtension = 141.42;

        /// <summary>
        /// New dimension set with property sets filled
        /// </summary>
        /// <param name="pts">Points to dimension</param>
        /// <param name="leaderPt">Point to position dimension leader line</param>
        /// <param name="cs">Dimension coordinate sytem</param>
        /// <param name="dimColor">Color of dimension lines</param>
        /// <param name="txtColor">Color of text</param>
        public DimensionSet(List<Point> pts, Point leaderPt, CoordinateSystem cs, Color dimColor, Color txtColor)
        {
            if (pts == null) throw new ArgumentNullException("pts");
            if (leaderPt == null) throw new ArgumentNullException("leaderPt");
            if (cs == null) throw new ArgumentNullException("cs");
            if (dimColor == null) throw new ArgumentNullException("dimColor");
            if (txtColor == null) throw new ArgumentNullException("txtColor");

            _pts = pts;
            _primaryColor = dimColor;
            _secondaryColor = txtColor;
            _leaderPoint = leaderPt;
            _coordinateSystem = cs;
        }

        /// <summary>
        /// Draws temporary graphics in model
        /// </summary>
        /// <returns>True if created</returns>
        public bool Create()
        {
            return DrawYdimInModel();
        }

        /// <summary>
        /// Draws temporary graphics in model
        /// </summary>
        /// <returns>True if created</returns>
        private bool DrawYdimInModel()
        {
            if (_pts == null || _pts.Count < 2) return false;
            if (_leaderPoint == null) return false;
            if (_primaryColor == null || _secondaryColor == null) return false;
            
            var offsetDist = _leaderPoint == null ? DefaultOffsetDistance : _pts[0].X - _leaderPoint.X;
            var ptForY = new Point(_leaderPoint.X, _pts[0].Y, _pts[0].Z);
            LineSegment ls;
            if (_leaderPoint.X > _pts[0].X) ls = new LineSegment(ptForY, _pts[0]); else ls = new LineSegment(_pts[0], ptForY);
            var dimCoordSys = new CoordinateSystem(_pts[0], _coordinateSystem.AxisY, ls.GetDirectionVector());

            Point lastPoint = null;
            var alignDist = 0.0;
            foreach (var pt in _pts)
            {
               if (lastPoint != null) DrawDimensionInModel(lastPoint, pt, offsetDist - alignDist, dimCoordSys, _primaryColor, _secondaryColor);
                lastPoint = pt;
                alignDist = _pts[0].X - lastPoint.X;
            }
            return true;
        }

        /// <summary>
        /// Creates 2 point X dimension in model
        /// </summary>
        /// <param name="p1">Point 1</param>
        /// <param name="p2">Point 2</param>
        /// <param name="offsetDist">Distance from first point to line offset</param>
        /// <param name="dimCs">Coordinate system for dimension line (x axis = leader line, y axis = offset line direction)</param>
        /// <param name="dimColor">Dimension color</param>
        /// <param name="txtColor">Text color</param>
        private static void DrawDimensionInModel(Point p1, Point p2, double offsetDist, CoordinateSystem dimCs, Color dimColor, Color txtColor)
        {
            if (p1 == null) throw new ArgumentNullException("p1");
            if (p2 == null) throw new ArgumentNullException("p2");
            if (dimCs == null) throw new ArgumentNullException("dimCs");
            if (dimColor == null) throw new ArgumentNullException("dimColor");
            if (txtColor == null) throw new ArgumentNullException("txtColor");

            

            // Get distance between x coordinates
            var distance = MacroExtensions.GetYdistPointToPoint(p1, p2);
            var leg1 = new LineSegment(p1, p1.GetTranslated(dimCs.AxisY.GetNormal() * offsetDist));
            var leg2 = new LineSegment(leg1.Point2, leg1.Point2.GetTranslated(dimCs.AxisX.GetNormal() * distance));
            var leg3 = new LineSegment(leg2.Point2, p2);
            leg1.PaintLine(dimColor);
            leg2.PaintLine(dimColor);
            leg3.PaintLine(dimColor);

            //Get angle dimensions for tick marks
            var dx = Math.Cos(TickAngle * Math.PI / 180) * TickLength * 0.5;
            var dy = Math.Sin(TickAngle * Math.PI / 180) * TickLength * 0.5;

            var tick1 = new LineSegment(new Point(leg1.Point2), new Point(leg1.Point2));
            tick1.Point1.Translate(leg2.GetDirectionVector() * -dx);
            tick1.Point1.Translate(leg1.GetDirectionVector() * -dy);
            tick1.Point2.Translate(leg2.GetDirectionVector() * dx);
            tick1.Point2.Translate(leg1.GetDirectionVector() * dy);
            tick1.PaintLine(dimColor);

            var tick2 = new LineSegment(new Point(leg2.Point2), new Point(leg2.Point2));
            tick2.Point1.Translate(leg2.GetDirectionVector() * -dx);
            tick2.Point1.Translate(leg3.GetDirectionVector() * dy);
            tick2.Point2.Translate(leg2.GetDirectionVector() * dx);
            tick2.Point2.Translate(leg3.GetDirectionVector() * -dy);
            tick2.PaintLine(dimColor);

            var startEndExtHoriz = new LineSegment(new Point(leg1.Point2), new Point(leg1.Point2));
            startEndExtHoriz.Point1.Translate(leg2.GetDirectionVector() * -LeaderExtension);
            startEndExtHoriz.PaintLine(dimColor);

            var finEndExtHoriz = new LineSegment(new Point(leg2.Point2), new Point(leg2.Point2));
            finEndExtHoriz.Point2.Translate(leg2.GetDirectionVector() * LeaderExtension);
            finEndExtHoriz.PaintLine(dimColor);

            var startEndExtVert = new LineSegment(new Point(leg1.Point2), new Point(leg1.Point2));
            startEndExtVert.Point1.Translate(leg1.GetDirectionVector() * LeaderExtension);
            startEndExtVert.PaintLine(dimColor);

            var finEndExtVert = new LineSegment(new Point(leg2.Point2), new Point(leg2.Point2));
            finEndExtVert.Point2.Translate(leg3.GetDirectionVector() * -LeaderExtension);
            finEndExtVert.PaintLine(dimColor);

            MacroExtensions.DrawText(new Model(), new Length(Math.Abs(distance)).ToCurrentUnits(), leg2.GetMidPoint(), txtColor);
        }

        /// <summary>
        /// Draws line segment in model with temporary graphics
        /// </summary>
        /// <param name="ls">Line segment</param>
        /// <param name="color">Color to use</param>
        /// <returns>True if successful</returns>
        protected static bool DrawLineSegment(LineSegment ls, Color color)
        {
            if (ls == null) throw new ArgumentNullException("ls");
            if (color == null) throw new ArgumentNullException("color");
            var gd = new GraphicsDrawer();
            return gd.DrawLineSegment(ls.Point1, ls.Point2, color);
        }
    }

    public static class MacroExtensions
    {
        /// <summary>
        /// Gets new point translated from current
        /// </summary>
        /// <param name="pt">Point of origin</param>
        /// <param name="v">Vector translation to move copy of point</param>
        /// <returns>New translated point</returns>
        public static Point GetTranslated(this Point pt, Vector v)
        {
            if (pt == null) throw new ArgumentNullException("pt");
            if (v == null) throw new ArgumentNullException("v");
            var tempPt = new Point(pt);
            tempPt.Translate(v.X, v.Y, v.Z);
            return tempPt;
        }

        /// <summary>
        /// Paints temporary line in the model, Debug model only
        /// </summary>
        /// <param name="ls">Line segment to paint</param>
        /// <param name="col">Color to use, red if skipped</param>
        public static void PaintLine(this LineSegment ls, Color col = null)
        {
            if (col == null) col = new Color(1, 0, 0);
            var gd = new GraphicsDrawer();
            gd.DrawLineSegment(ls.Point1, ls.Point2, col);
        }

        /// <summary>
        /// Translates point vector direcion and distance
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="tVector"></param>
        public static void Translate(this Point pt, Vector tVector)
        {
            pt.Translate(tVector.X, tVector.Y, tVector.Z);
        }

        /// <summary>
        /// Check if is the same direction to other vector
        /// </summary>
        /// <param name="v1">Vector 1</param>
        /// <param name="v2">Vector 2</param>
        /// <param name="angleTolerance">Angle tolerance (radians)</param>
        /// <returns>True if angle is close to 0</returns>
        public static bool IsSameDirection(this Vector v1, Vector v2, double angleTolerance = GeometryConstants.ANGULAR_EPSILON)
        {
            if (v2 == null) return false;
            var angle = v1.GetAngleBetween(v2) * 180 / Math.PI;
            return Math.Abs(angle) < angleTolerance;
        }

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
        private static void DrawText(string text, Point tPoint, Color tColor)
        {
            var gd = new GraphicsDrawer();
            gd.DrawText(tPoint, text, tColor);
        }

        /// <summary>
        /// Gets midpoint of a line segment
        /// </summary>
        /// <param name="ls">Tekla line segment</param>
        /// <returns>New 3d point at midpoint</returns>
        public static Point GetMidPoint(this LineSegment ls)
        {
            if (ls == null) throw new ApplicationException();
            var startPoint = new Point(ls.Point1);
            var displacement = ls.GetDirectionVector().GetNormal() * ls.Length() * 0.5;
            startPoint.Translate(displacement.X, displacement.Y, displacement.Z);
            return startPoint;
        }

        public static double GetXdistPointToPoint(Point p1, Point p2)
        {
            double xDist;
            xDist = p2.X - p1.X;
            return xDist;
        }

        public static double GetYdistPointToPoint(Point p1, Point p2)
        {
            double yDist;
            yDist = p2.Y - p1.Y;
            return yDist;
        }
    }

    /// <summary>
    /// Extensions class for Tekla.BIM.Quantities class
    /// </summary>
    public static class TsLength
    {
        /// <summary>
        /// Returns ft-fraction inch string rounded to 1/16 if XS_IMPERIAL=TRUE, mm to to decimal places otherwise
        /// </summary>
        public static string ToCurrentUnits(this Length ln)
        {
            //Create new formatter based on current unit model flag
            LengthFormatter myFormatter;
            if (new Model().IsImperial())
            {
                myFormatter = new LengthFormatter(LengthUnit.Foot, "1/16", null);
                var unitSym = new UnitSymbols();
                unitSym.InchSymbolPlacement = InchSymbolPlacement.AfterFractions;
                unitSym.SeparatorBetweenFeetAndInches = "-";
            }
            else
            {
                myFormatter = new LengthFormatter(LengthUnit.Millimeter, "0.00", null);
            }

            //Return using correct formatter the string value
            return myFormatter.ToString(ln);
        }

        /// <summary>
        /// Uses BIM Length.Parse to convert string to Length and returns Millimeter value
        /// Uses XS_IMPERIAL to decide if input is Foot versus millimeter value in string format
        /// May not work if input is other than foot or mm and flag not set
        /// </summary>
        /// <param name="str">Numberic value in string format</param>
        /// <returns>Double millimeter value</returns>
        public static double FromCurrrentUnits(this string str)
        {
            //Create new formatter based on current unit model flag
            LengthFormatter myFormatter;
            if (new Model().IsImperial())
            {
                myFormatter = new LengthFormatter(LengthUnit.Foot, "1/16", null);
                var unitSym = new UnitSymbols();
                unitSym.InchSymbolPlacement = InchSymbolPlacement.AfterFractions;
                unitSym.SeparatorBetweenFeetAndInches = "-";
            }
            else
            {
                myFormatter = new LengthFormatter(LengthUnit.Millimeter, "0.00", null);
            }

            //Parse value using formatter and return double mm
            Length parsedLength;
            myFormatter.TryParse(str, out parsedLength);
            return parsedLength.Millimeters;
        }

        /// <summary>
        /// Checks model flag for XS_IMPERIAL to see unit base
        /// </summary>
        /// <param name="model">New model connection</param>
        /// <returns>True if Imperial environment set, False otherwise</returns>
        public static bool IsImperial(this Model model)
        {
            var stringTemp = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XS_IMPERIAL", ref stringTemp);
            if (!string.IsNullOrEmpty(stringTemp)) return true;
            return string.CompareOrdinal(stringTemp, "1") == 0;
        }
    }

    public static class PickerService
    {
        /// <summary>Tekla Structures Model Internal Picker </summary>
        public static readonly Picker ModelPicker = new Picker();

        /// <summary>
        /// Picks a polygon
        /// </summary>
        /// <param name="prompt">
        /// The prompt
        /// </param>
        /// <returns>
        /// A Polygon of the picked points
        /// </returns>
        public static Polygon PickAPolygon(string prompt = null)
        {
            ArrayList pick = null;
            Polygon polygon = null;
            try
            {
                while (pick == null)
                {
                    if (prompt == null) pick = ModelPicker.PickPoints(Picker.PickPointEnum.PICK_POLYGON);
                    else pick = ModelPicker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, prompt);
                }

                if (pick.Count >= 2)
                {
                    polygon = new Polygon();
                    polygon.Points.AddRange(pick);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("interrupt")) { }
                else Trace.WriteLine(ex.Message);
            }
            return polygon;
        }

        /// <summary>
        /// Picks a point
        /// </summary>
        /// <param name="prompt">
        /// The prompt
        /// </param>
        /// <returns>
        /// The picked point
        /// </returns>
        public static Point PickAPoint(string prompt = null)
        {
            Point pick = null;
            try
            {
                pick = (prompt == null) ? ModelPicker.PickPoint() : ModelPicker.PickPoint(prompt);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("interrupt")) { }
                else Trace.WriteLine(ex.Message);
            }
            return pick;
        }
    }
}
