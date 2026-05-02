using System;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

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
                DeleteAllClipPlanes.RunMacro(akit);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.InnerException + ex.Message + ex.StackTrace);
            }
        }
    }

    public static class DeleteAllClipPlanes
    {
        public static void RunMacro(IScript akit)
        {
            //Get visible views and enumerator
            var visibleViews = ViewHandler.GetVisibleViews();
            while (visibleViews.MoveNext())
            {
                var currentView = visibleViews.Current;
                var clipPlanes = currentView.GetClipPlanes();
                foreach (ClipPlane clipPlane in clipPlanes) clipPlane.Delete();
            }

            //Update model with changes
            new Model().CommitChanges();
        }
    }
}
