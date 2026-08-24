using System;
using System.Collections.Generic;
using Rhino;

namespace WireDrop
{
    /// <summary>
    /// Failures here must never surface as an exception inside Grasshopper's message
    /// loop, so everything is caught and reported once. A repeated failure goes quiet.
    /// </summary>
    internal static class Log
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Once(string key, string message)
        {
            if (!Reported.Add(key)) return;
            try { RhinoApp.WriteLine("WireDrop: " + message); } catch { }
        }

        public static void Error(string key, Exception ex)
        {
            Once(key, key + " failed — " + ex.Message + ". The feature is disabled for this session.");
        }

        /// <summary>Runs an action, swallowing and reporting anything it throws.</summary>
        public static void Guard(string key, Action action)
        {
            try { action(); }
            catch (Exception ex) { Error(key, ex); }
        }
    }
}
