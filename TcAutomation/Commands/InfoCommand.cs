using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TcAutomation.Core;
using TcAutomation.Models;

namespace TcAutomation.Commands
{
    /// <summary>
    /// Get information about a TwinCAT solution.
    /// </summary>
    public static class InfoCommand
    {
        public static async Task<ProjectInfo> ExecuteAsync(string solutionPath)
        {
            var result = new ProjectInfo
            {
                SolutionPath = solutionPath
            };

            // Validate input
            if (!File.Exists(solutionPath))
            {
                result.ErrorMessage = $"Solution file not found: {solutionPath}";
                return result;
            }

            VisualStudioInstance? vsInstance = null;

            try
            {
                // Get basic info without loading VS (fast path)
                var tcProjectPath = TcFileUtilities.FindTwinCATProjectFile(solutionPath);
                if (string.IsNullOrEmpty(tcProjectPath))
                {
                    result.ErrorMessage = "No TwinCAT project (.tsproj) found in solution";
                    return result;
                }

                result.TcVersion = TcFileUtilities.GetTcVersion(tcProjectPath);
                result.TcVersionPinned = TcFileUtilities.IsTwinCATProjectPinned(tcProjectPath);
                result.VisualStudioVersion = TcFileUtilities.GetVisualStudioVersion(solutionPath) ?? "Unknown";

                // For detailed info (PLCs, etc.), we need to load VS
                MessageFilter.Register();

                vsInstance = new VisualStudioInstance(solutionPath, result.TcVersion);
                vsInstance.Load();
                vsInstance.LoadSolution();

                var automation = new AutomationInterface(vsInstance);
                result.TargetPlatform = automation.ActiveTargetPlatform;

                // Get PLC projects
                var plcProjects = automation.GetPlcProjects();
                result.PlcProjects = plcProjects.Select(p => new Models.PlcInfo
                {
                    Name = p.Name,
                    AmsPort = p.AmsPort
                }).ToList();
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Failed to get project info: {ex.Message}";
            }
            finally
            {
                vsInstance?.Close();
                MessageFilter.Revoke();
            }

            return await Task.FromResult(result);
        }
    }
}
