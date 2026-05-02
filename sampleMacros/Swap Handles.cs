using System;
using System.Collections;
using System.Diagnostics;
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
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            try
            {
                new SwapHandles();
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
                new SwapHandles();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message + ex.StackTrace);
            }
        }
    }

    public class SwapHandles
    {
        public SwapHandles()
        {
            //Get selected objects and put them in an enumerator/container
			WorkPlaneHandler wph = new Model().GetWorkPlaneHandler();
			TransformationPlane originalPlane = wph.GetCurrentTransformationPlane();
			wph.SetCurrentTransformationPlane(new TransformationPlane());
            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            var myEnum = selector.GetSelectedObjects();

            //Cycle through selected objects
            while (myEnum.MoveNext())
            {
                //Cast beam
                if (myEnum.Current is Beam)
                {
                    var myBeam = myEnum.Current as Beam;

                    // Get part current handles
                    var startPoint = myBeam.StartPoint;
                    var endPoint = myBeam.EndPoint;

                    myBeam.Position.PlaneOffset = -1 * myBeam.Position.PlaneOffset;
                    myBeam.Position.RotationOffset = -1 * myBeam.Position.RotationOffset;
                  
                    // Columns need to handle Depth
                    if (Math.Abs(startPoint.X - endPoint.X) < 1 && Math.Abs(startPoint.Y - endPoint.Y) < 1)
                    {
                        if (myBeam.Position.Depth == Position.DepthEnum.FRONT)
                            myBeam.Position.Depth = Position.DepthEnum.BEHIND;
                        else if (myBeam.Position.Depth == Position.DepthEnum.BEHIND)
                            myBeam.Position.Depth = Position.DepthEnum.FRONT;
                    }

                    //DX
                    var ST_DXoffsetValue = myBeam.StartPointOffset.Dx;
                    var END_DXoffsetValue = myBeam.EndPointOffset.Dx;
                    //DY
                    var ST_DYoffsetValue = myBeam.StartPointOffset.Dy;
                    var END_DYoffsetValue = myBeam.EndPointOffset.Dy;
                    //DZ
                    var ST_DZoffsetValue = myBeam.StartPointOffset.Dz;
                    var END_DZoffsetValue = myBeam.EndPointOffset.Dz;

                    // Switch part handles
                    myBeam.StartPoint = endPoint;
                    myBeam.EndPoint = startPoint;
					
                    myBeam.StartPointOffset.Dx = -1 * END_DXoffsetValue;
                    myBeam.EndPointOffset.Dx = -1 * ST_DXoffsetValue;
                    myBeam.StartPointOffset.Dy = -1 * END_DYoffsetValue;
                    myBeam.EndPointOffset.Dy = -1 * ST_DYoffsetValue;
                    myBeam.StartPointOffset.Dz = END_DZoffsetValue;
                    myBeam.EndPointOffset.Dz = ST_DZoffsetValue;

                    //Swap uda's for design forces
                    SwapEndForces(myBeam);

                    // modify beam and refresh model + undo 
                    myBeam.Modify();
                }
                else if(myEnum.Current is PolyBeam)
                {
                    var myBeam = myEnum.Current as PolyBeam;

                    // Get part current handles
                    var newPoints = new ArrayList();
                    var oldPoints = myBeam.Contour.ContourPoints;

                    //Copy points to new seperate list first
                    foreach (var cp in oldPoints)
                        newPoints.Add(cp);
                    newPoints.Reverse();

                    //Swap uda's for design forces
                    SwapEndForces(myBeam);

                    // modify beam and refresh model + undo 
                    myBeam.Contour.ContourPoints = newPoints;
                    myBeam.Modify();
                }
                else if (myEnum.Current is BoltGroup)
                {
                    var myBolt = myEnum.Current as BoltGroup;

                    // Get bolt current handles
                    var startPoint = myBolt.FirstPosition;
                    var endPoint = myBolt.SecondPosition;

                    // Switch bolt handles
                    myBolt.FirstPosition = endPoint;
                    myBolt.SecondPosition = startPoint;

                    // modify bolt and refresh model + undo
                    myBolt.Modify();
                }
			}
				
            //Update model with changes
			wph.SetCurrentTransformationPlane(originalPlane);
            new Model().CommitChanges();
        }

        private static void SwapEndForces(ModelObject myBeam)
        {
            var originalEnd1 = string.Empty;
            var originalEnd2 = string.Empty;
            myBeam.GetUserProperty("BM_FORCE1", ref originalEnd1);
            myBeam.GetUserProperty("BM_FORCE2", ref originalEnd2);
            myBeam.SetUserProperty("BM_FORCE1", originalEnd2);
            myBeam.SetUserProperty("BM_FORCE2", originalEnd1);
        }
    }
}
