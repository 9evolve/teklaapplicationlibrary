using System;
using System.Diagnostics;
using System.Windows.Forms;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model.UI;
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
                ConvergeHandles.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public class ConvergeHandles
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

                    T3D.Point ConvergePoint = _Picker.PickPoint();

                    //string Test = ConvergePoint.ToString();

                    //MessageBox.Show(Test); 

                    TSMUI.ModelObjectSelector Selector = new TSMUI.ModelObjectSelector();
                    ModelObjectEnumerator SelectedStuff = Selector.GetSelectedObjects();

                    TSM.Operations.Operation.DisplayPrompt("Beams are being extended or trimmed to the picked vertical plane.");

                    while (SelectedStuff.MoveNext())
                    {
                        Beam CurrentItem = SelectedStuff.Current as Beam;
                        if (CurrentItem != null)
                        {

                            //MessageBox.Show(CurrentItem.StartPoint.X.ToString());

                            string SX = CurrentItem.StartPoint.X.ToString("F4");
                            string SY = CurrentItem.StartPoint.Y.ToString("F4");
                            string EX = CurrentItem.EndPoint.X.ToString("F4");
                            string EY = CurrentItem.EndPoint.Y.ToString("F4");

                            //MessageBox.Show(SX);

                            double SCheckX = Convert.ToDouble(SX);
                            double SCheckY = Convert.ToDouble(SY);
                            double ECheckX = Convert.ToDouble(EX);
                            double ECheckY = Convert.ToDouble(EY);

                            double StartDistance = T3D.Distance.PointToPoint(ConvergePoint, CurrentItem.StartPoint);
                            double EndDistance = T3D.Distance.PointToPoint(ConvergePoint, CurrentItem.EndPoint);

                            //Identify Column
                            if (SCheckX == ECheckX & SCheckY == ECheckY)
                            {
                                CurrentItem.StartPoint.X = ConvergePoint.X;
                                CurrentItem.StartPoint.Y = ConvergePoint.Y;
                                CurrentItem.EndPoint.X = ConvergePoint.X;
                                CurrentItem.EndPoint.Y = ConvergePoint.Y;
                            }

                            if (StartDistance <= EndDistance)
                            {
                                CurrentItem.StartPoint.X = ConvergePoint.X;
                                CurrentItem.StartPoint.Y = ConvergePoint.Y;

                            }
                            else
                            {
                                CurrentItem.EndPoint.X = ConvergePoint.X;
                                CurrentItem.EndPoint.Y = ConvergePoint.Y;
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
