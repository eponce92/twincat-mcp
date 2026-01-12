using System.Collections.Generic;

namespace TcAutomation.Models
{
    /// <summary>
    /// Information about a TwinCAT solution.
    /// </summary>
    public class ProjectInfo
    {
        public string SolutionPath { get; set; } = "";
        public string TcVersion { get; set; } = "";
        public bool TcVersionPinned { get; set; }
        public string VisualStudioVersion { get; set; } = "";
        public string TargetPlatform { get; set; } = "";
        public List<PlcInfo> PlcProjects { get; set; } = new List<PlcInfo>();
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Information about a PLC project.
    /// </summary>
    public class PlcInfo
    {
        public string Name { get; set; } = "";
        public int AmsPort { get; set; }
    }
}
