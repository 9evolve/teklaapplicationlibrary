using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Tekla.Structures;
using Tekla.Structures.Model;
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
               CreatePlateFromSurface.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class CreatePlateFromSurface
    {
        #region User Options

        private const string SidePlateSettings = "Plate";
        private const string TopPlateSettings = "Plate";
        private const string BottomPlateSettings = "Plate";
        private const string CustomTypeTag = "PLATEWORK";

        #endregion

        private static Tekla.Structures.Model.UI.Picker ModelPicker
        {
            get { return new Tekla.Structures.Model.UI.Picker(); }
        }

        public static void RunMacro(IScript akit)
        {
            const string prompt = "Pick face to create plate from";
            var platesCreated = new List<ContourPlate>();

            while (true)
            {
                //Get Input from user prompt
                IEnumerator thisEnumerator;
                try
                {
                    var thisFace = ModelPicker.PickFace(prompt);
                    thisEnumerator = thisFace.GetEnumerator();
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("user")) return;
                    throw;
                }

                //Enumerate faces from user input
                while (thisEnumerator.MoveNext())
                {
                    var thisItem = thisEnumerator.Current as InputItem;
                    if (thisItem == null || thisItem.GetInputType() != InputItem.InputTypeEnum.INPUT_POLYGON) continue;

                    var points = thisItem.GetData() as ArrayList;
                    if (points == null || points.Count < 3) continue;
                    CreatePlateFromSurfaceMacro(points, ref platesCreated);
                }
            }
        }

        private static void CreatePlateFromSurfaceMacro(ArrayList originalPoints, ref List<ContourPlate> platesCreated)
        {
            const string fileSuffix = "cpl";
            var originalPlane = new Model().GetWorkPlaneHandler().GetCurrentTransformationPlane();
            try
            {
                //Copy local point and globalize
                var globalPoints = new List<Point>();
                var localPoints = new List<Point>();
                foreach (Point cp in originalPoints)
                {
                    globalPoints.Add(cp);
                    localPoints.Add(cp);
                }
                TransformPointsToGlobal(ref globalPoints);

                //Derive plate vectors from points
                var firstPoint = (Point)originalPoints[0];
                var secondPoint = (Point)originalPoints[1];
                var xAxis = new Vector(secondPoint.X - firstPoint.X, secondPoint.Y - firstPoint.Y,
                                       secondPoint.Z - firstPoint.Z);

                //Move current workplane
                var face = new UsableFace(originalPoints);
                new Model().GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());
                new Model().GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane(firstPoint, xAxis, xAxis.Cross(face.Normal)));
                TransformPointsToLocal(ref localPoints);

                //Check direction of local versus global up direction
                var globalZ = new Vector(0, 0, 1);
                var localZ = (TransformVector(globalZ)).GetNormal();
                var angle = Math.Abs(localZ.GetAngleBetween(globalZ));

                //Check if should create this face plate
                var isTopPlate = false;
                var isBottomPlate = false;
                var isSidePlate = false;
                const double tenDegrees = 0.1744; //radian value
                if (angle < tenDegrees)
                    isTopPlate = true;

                //Check if plate is on bottom
                if ((Math.Abs(Math.PI - angle) < 0.1))
                    isBottomPlate = true;

                //Check if side panels
                if (!isBottomPlate && !isTopPlate)
                    isSidePlate = true;

                //Create new plate in memory
                var plate = new ContourPlate { Contour = { ContourPoints = GetContourPoints(localPoints) } };
                if (isTopPlate)
                    SaveFileService.LoadPartSettingsFromFile(ref plate, TopPlateSettings, fileSuffix);
                else if (isBottomPlate)
                    SaveFileService.LoadPartSettingsFromFile(ref plate, BottomPlateSettings, fileSuffix);
                else if (isSidePlate) //Might do more specific planes later, e.g. inside vs. outside
                    SaveFileService.LoadPartSettingsFromFile(ref plate, SidePlateSettings, fileSuffix);

                //Insert new plate into model
                if (!plate.Insert())
                {
                    var msg = string.Format("Unable to insert plate to face {0}.", face.Normal);
                    Trace.WriteLine(msg);
                    return;
                }
                platesCreated.Add(plate);
                plate.SetUserProperty("CustomType", CustomTypeTag);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
            finally
            {
                new Model().GetWorkPlaneHandler().SetCurrentTransformationPlane(originalPlane);
                new Model().CommitChanges();
            }
        }

        private static Vector TransformVector(Vector tVector)
        {
            if (tVector == null)
                throw new ArgumentNullException();

            var p1 = new Point();
            var p2 = new Point(tVector);
            var transMatrix = new Model().GetWorkPlaneHandler().GetCurrentTransformationPlane().TransformationMatrixToLocal;
            var p1Moved = transMatrix.Transform(p1);
            var p2Moved = transMatrix.Transform(p2);
            return new Vector(p1Moved - p2Moved);
        }

        private static void TransformPointsToGlobal(ref List<Point> points)
        {
            var result = new List<Point>();
            var transMatrix = new Model().GetWorkPlaneHandler().GetCurrentTransformationPlane().TransformationMatrixToGlobal;
            foreach (var cp in points)
            {
                var movedPoint = transMatrix.Transform(cp);
                result.Add(new Point(movedPoint));
            }
            points = result;
        }

        private static void TransformPointsToLocal(ref List<Point> points)
        {
            var result = new List<Point>();
            var transMatrix = new Model().GetWorkPlaneHandler().GetCurrentTransformationPlane().TransformationMatrixToLocal;
            foreach (var cp in points)
            {
                var movedPoint = transMatrix.Transform(cp);
                result.Add(new Point(movedPoint));
            }
            points = result;
        }

        private static ArrayList GetContourPoints(IEnumerable<Point> originalPoints)
        {
            var result = new ArrayList();
            foreach (Point pt in originalPoints)
                result.Add(new ContourPoint(pt, new Chamfer()));
            return result;
        }

        public static class SaveFileService
        {
            /// <summary>
            /// This sets the properties of a referenced contour plate based on a saved away attribute file.
            /// </summary>
            /// <param name="fileName">Saved attribute file to load properties from.</param>
            /// <param name="thisContourPlate">Beam object that properties will be changed for.</param>
            /// <param name="fileSuffix"> </param>
            internal static void LoadPartSettingsFromFile(ref ContourPlate thisContourPlate, string fileName, string fileSuffix)
            {
                if (string.IsNullOrEmpty(fileName)) return;
                var saveFile = FormTools.GetFileFromTeklaSettings(fileName + "." + fileSuffix);
                if (saveFile == null || !saveFile.Exists)
                {
                    Trace.WriteLine(string.Format("Saved file {0} does not exist, no settings loaded.", fileName));
                    if (fileName == "standard") //Give up and set minimum properties to create plate
                    {
                        thisContourPlate.Profile.ProfileString = "25.4";
                        return;
                    }

                    //Try to load standard file
                    LoadPartSettingsFromFile(ref thisContourPlate, "standard", fileSuffix);
                }

                if (saveFile == null) return;
                var partAttributes = GetPartAttributes(saveFile);
                SetPartValuesFromCache(thisContourPlate, partAttributes);
            }

            private static void SetPartValuesFromCache(Part thisContourPlate, IDictionary<string, PartAttribute> partAttributes)
            {
                if (partAttributes.Count < 1) return;

                if (partAttributes.ContainsKey("profile"))
                    thisContourPlate.Profile.ProfileString = (string)partAttributes["profile"].AttributeValue;

                if (partAttributes.ContainsKey("assembly_number_prefix"))
                    thisContourPlate.AssemblyNumber.Prefix =
                        (string)partAttributes["assembly_number_prefix"].AttributeValue;

                if (partAttributes.ContainsKey("assembly_number_start_no") && partAttributes["assembly_number_start_no"].AttributeValue.ToString().Length > 0)
                    thisContourPlate.AssemblyNumber.StartNumber =
                        (int)partAttributes["assembly_number_start_no"].AttributeValue;

                if (partAttributes.ContainsKey("part_group"))
                    thisContourPlate.Class = (string)partAttributes["part_group"].AttributeValue;

                if (partAttributes.ContainsKey("material"))
                    thisContourPlate.Material.MaterialString = (string)partAttributes["material"].AttributeValue;

                if (partAttributes.ContainsKey("name"))
                    thisContourPlate.Name = (string)partAttributes["name"].AttributeValue;

                if (partAttributes.ContainsKey("part_number_prefix"))
                    thisContourPlate.PartNumber.Prefix =
                        (string)partAttributes["part_number_prefix"].AttributeValue;

                if (partAttributes.ContainsKey("part_number_start_no") && partAttributes["part_number_start_no"].AttributeValue.ToString().Length > 0)
                    thisContourPlate.PartNumber.StartNumber =
                        (int)partAttributes["part_number_start_no"].AttributeValue;

                if (partAttributes.ContainsKey("finish"))
                    thisContourPlate.Finish = (string)partAttributes["finish"].AttributeValue;

                if (partAttributes.ContainsKey("value_position_depth") && partAttributes["value_position_depth"].AttributeValue.ToString().Length > 0)
                    thisContourPlate.Position.DepthOffset =
                        (double)partAttributes["value_position_depth"].AttributeValue;

                if (partAttributes.ContainsKey("position_depth") && partAttributes["position_depth"].AttributeValue.ToString().Length > 0)
                {
                    var depthPosition = (int)partAttributes["position_depth"].AttributeValue;
                    switch (depthPosition)
                    {
                        case 0:
                            thisContourPlate.Position.Depth = Position.DepthEnum.MIDDLE;
                            break;
                        case 1:
                            thisContourPlate.Position.Depth = Position.DepthEnum.FRONT;
                            break;
                        case 2:
                            thisContourPlate.Position.Depth = Position.DepthEnum.BEHIND;
                            break;
                    }
                }
            }

            /// <summary>
            /// Returns a dictionary collection of Part Attributes from a saved attribute file.
            /// </summary>
            /// <param name="savedFile"> </param>
            private static Dictionary<string, PartAttribute> GetPartAttributes(FileSystemInfo savedFile)
            {
                if (savedFile == null)
                    throw new ArgumentNullException();

                const string prefixName = "dia_part_attr.";
                var result = new Dictionary<string, PartAttribute>();

                var sr1 = new StreamReader(savedFile.FullName);
                while (!sr1.EndOfStream)
                {
                    var readLine = sr1.ReadLine();
                    if (readLine == null) continue;

                    var indexSpace = readLine.IndexOf(' ');
                    var name = Left(readLine, indexSpace);
                    var value = Right(readLine, indexSpace);
                    name = name.Replace(prefixName, "");
                    value = value.Trim();

                    var thisAttribute = new PartAttribute { AttributeName = name };
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                        thisAttribute.AttributeValue = value.Replace("\"", "");
                    else
                    {
                        if (value.Contains("."))
                        {
                            var doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            thisAttribute.AttributeValue = doubleValue;
                        }
                        else
                        {
                            try
                            {
                                var intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                                thisAttribute.AttributeValue = intValue;
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
                                thisAttribute.AttributeValue = value;
                            }
                        }
                    }
                    result.Add(thisAttribute.AttributeName, thisAttribute);
                    Trace.WriteLine(string.Format("{0} : {1}", thisAttribute.AttributeName, thisAttribute.AttributeValue));
                }
                sr1.Close();
                return result;
            }
            private static string Right(string tempString, int index)
            {
                return tempString.Substring(index + 1, tempString.Length - index - 1);
            }
            private static string Left(string tempString, int index)
            {
                return tempString.Substring(0, index);
            }
        }

        public class PartAttribute
        {
            public enum AttributeDataType { Double, String, Int };
            public string AttributeName;
            public object AttributeValue;
        }

        public static class FormTools
        {
            private static List<string> _mPropertyFileDirectories = GetStandardPropertyFileDirectories();

            /// <summary>
            /// The directories where to look for property files.
            /// </summary>
            public static List<string> PropertyFileDirectories
            {
                get { return _mPropertyFileDirectories; }
                set { _mPropertyFileDirectories = value; }
            }

            public static FileInfo GetFileFromTeklaSettings(string fileName)
            {
                if (string.IsNullOrEmpty(fileName))
                    throw new ArgumentNullException();

                FileInfo saveAsFile = null;
                if (fileName.Contains("."))
                    saveAsFile = GetAttributeFile(fileName);
                return saveAsFile;
            }

            /// <summary>
            /// Gets a file info representing the first match in the standard property file directories.
            /// </summary>
            /// <param name="fileName">The name of the file including the file extension.</param>
            /// <returns>A file info for the first match in the directory list. Null if no match was found.</returns>
            public static FileInfo GetAttributeFile(string fileName)
            {
                return GetAttributeFile(PropertyFileDirectories, fileName);
            }

            /// <summary>
            /// Gets a file info representing the first match in the search directories.
            /// </summary>
            /// <param name="searchDirectories">The list of directories to be used for searching for the file.</param>
            /// <param name="fileName">The name of the file including the file extension.</param>
            /// <returns>A file info for the first match in the directory list. Null if no match was found.</returns>
            private static FileInfo GetAttributeFile(IEnumerable<string> searchDirectories, string fileName)
            {
                try
                {
                    foreach (var di in searchDirectories)
                    {
                        if (File.Exists(di + fileName))
                            return new FileInfo(di + fileName);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception:\n{0}\n{1}", e.Message, e.StackTrace);
                }
                return null;
            }

            /// <summary>
            /// Gets the paths where to look for the property files.
            /// </summary>
            /// <returns>The paths where to look for the property files.</returns>
            private static List<string> GetStandardPropertyFileDirectories()
            {
                var fileDirectories = new List<string>();
                try
                {
                    // First attempt to add the model/attributes/ directory
                    var model = new Tekla.Structures.Model.Model();

                    var modelPath = model.GetInfo().ModelPath;
                    // Check first for an "./attributes/" directory. If one is not found use the local directory.
                    if (IsValidDirectory(modelPath + @"/attributes/"))
                        fileDirectories.Add(modelPath + @"/attributes/");
                    else if (IsValidDirectory(modelPath + @"/"))
                        fileDirectories.Add(modelPath + @"/");

                    // Now add any Tekla Structures standard environment directories
                    AddPaths(fileDirectories, "XS_PROJECT");
                    AddPaths(fileDirectories, "XS_FIRM");
                    AddPaths(fileDirectories, "XS_SYSTEM");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception:\n{0}\n{1}", e.Message, e.StackTrace);
                }
                return fileDirectories;
            }

            private static void AddPaths(ICollection<string> fileDirectories, string environmentVariableName)
            {
                var semiColon = new[] { ';' };
                var environmentVariable = GetEnvironmentVariable(environmentVariableName);
                if (String.IsNullOrEmpty(environmentVariable)) return;
                var xsProject = environmentVariable.Split(semiColon);
                foreach (var path in xsProject)
                {
                    var cleanPath = path.Replace(@"\\\\", @"\\\\");
                    if (IsValidDirectory(cleanPath))
                        fileDirectories.Add(cleanPath);
                }
            }

            private static string GetEnvironmentVariable(string environmentVariableName)
            {
                var tempValue = string.Empty;
                TeklaStructuresSettings.GetAdvancedOption(environmentVariableName, ref tempValue);
                return tempValue;
            }

            /// <summary>
            /// Checks if a directory is valid.
            /// </summary>
            /// <param name="directory">The directory to be checked.</param>
            /// <returns>True if the directory is valid.</returns>
            public static bool IsValidDirectory(string directory)
            {
                if (string.IsNullOrEmpty(directory))
                    return false;
                return Directory.Exists(directory);
            }
        }

        /// <summary>
        /// Creates usable face from points since API gives only points from Face picker
        /// </summary>
        public class UsableFace
        {
            #region Public Properties

            /// <summary>
            /// Polygon list of points
            /// </summary>
            public List<Tekla.Structures.Geometry3d.Point> Points { get; set; }


            /// <summary>
            /// Normal vector for face
            /// </summary>
            public Tekla.Structures.Geometry3d.Vector Normal { get; set; }

            #endregion

            #region Private Properties

            private Tekla.Structures.Geometry3d.Vector XVector
            {
                get { return new Vector(SecondPoint.X - FirstPoint.X, SecondPoint.Y - FirstPoint.Y, SecondPoint.Z - FirstPoint.Z).GetNormal(); }
            }

            private Tekla.Structures.Geometry3d.Point FirstPoint { get { return Points[0]; } }

            private Tekla.Structures.Geometry3d.Point SecondPoint { get { return Points[1]; } }

            private Tekla.Structures.Geometry3d.Point LastPoint { get { return Points[Points.Count - 1]; } }

            #endregion

            #region Constructors

            /// <summary>
            /// Polygon face that knows Normal vector automatically
            /// </summary>
            /// <param name="originalPoints">Tekla.Structures.Geometry3d Points</param>
            public UsableFace(ArrayList originalPoints)
            {
                if (originalPoints == null || originalPoints.Count < 3)
                    throw new ArgumentNullException();

                //Populate list of points
                Points = new List<Point>();
                foreach (var tObject in originalPoints)
                {
                    var pt = tObject as Tekla.Structures.Geometry3d.Point;
                    if (pt == null) throw new ApplicationException("Input was not of type Tekla.Structures.Geometry3d.Point in ArrayList argument.");
                    Points.Add(pt);
                }

                //Calculate normal vector
                Normal = CalculateSurfaceNormal(Points);
            }

            /// <summary>
            /// Polygon face that knows Normal vector automatically
            /// </summary>
            /// <param name="originalPoints">Tekla.Structures.Geometry3d Points</param>
            public UsableFace(List<Tekla.Structures.Geometry3d.Point> originalPoints)
            {
                //Populate list of points
                Points = new List<Point>();
                Points = originalPoints;

                //Calculate normal vector
                Normal = CalculateSurfaceNormal(Points);
            }

            #endregion

            /// <summary>
            /// Newell's Method
            /// You can use a Newell's method for an arbitrary 3D polygon
            /// </summary>
            /// <param name="originalPoints"></param>
            /// <returns></returns>
            private Vector CalculateSurfaceNormal(IList originalPoints)
            {
                var result = new Vector(0, 0, 0);
                for (var i = 0; i < Points.Count; i++)
                {
                    var current = Points[i];
                    var next = Points[(i + 1) % Points.Count];
                    result.X += (current.Y - next.Y) * (current.Z + next.Z);
                    result.Y += (current.Z - next.Z) * (current.X + next.X);
                    result.Z += (current.X - next.X) * (current.Y + next.Y);
                }
                return result.GetNormal();
            }
        }
    }
}
