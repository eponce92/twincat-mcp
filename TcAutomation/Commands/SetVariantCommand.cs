using System;
using System.Text.Json;
using TcAutomation.Core;
using TCatSysManagerLib;

namespace TcAutomation.Commands
{
    /// <summary>
    /// Gets or sets the TwinCAT Project Variant.
    /// Requires TwinCAT XAE 4024+ (TCatSysManagerLib V 3.3.0.0 or later).
    /// </summary>
    public static class SetVariantCommand
    {
        public static int Execute(string solutionPath, string? tcVersion, string? variantName, bool getOnly)
        {
            VisualStudioInstance? vsInstance = null;
            var result = new SetVariantResult();

            try
            {
                // Find TwinCAT project and version
                string tsprojPath = TcFileUtilities.FindTwinCATProjectFile(solutionPath);
                if (string.IsNullOrEmpty(tsprojPath))
                {
                    result.ErrorMessage = "Could not find TwinCAT project file (.tsproj) in solution";
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    return 1;
                }

                string projectTcVersion = TcFileUtilities.GetTcVersion(tsprojPath);

                // Load Visual Studio
                vsInstance = new VisualStudioInstance(solutionPath, projectTcVersion, tcVersion);
                vsInstance.Load();
                vsInstance.LoadSolution();

                var automation = new AutomationInterface(vsInstance);
                
                result.SolutionPath = solutionPath;

                // Cast to ITcSysManager14 for access to project variants
                // This requires TCatSysManagerLib V 3.3.0.0 or later (TwinCAT XAE 4024+)
                ITcSysManager14 sysManager14;
                try
                {
                    sysManager14 = (ITcSysManager14)automation.SystemManager;
                }
                catch (InvalidCastException)
                {
                    result.ErrorMessage = "Project variants require TwinCAT XAE 4024 or later";
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    return 1;
                }

                // Get current variant
                result.PreviousVariant = sysManager14.CurrentProjectVariant ?? "";

                if (getOnly || string.IsNullOrEmpty(variantName))
                {
                    // Just return current variant
                    result.CurrentVariant = result.PreviousVariant;
                    result.Success = true;
                    result.Message = string.IsNullOrEmpty(result.CurrentVariant) 
                        ? "No project variant set (using default)" 
                        : $"Current variant: {result.CurrentVariant}";
                }
                else
                {
                    // Set new variant
                    try
                    {
                        sysManager14.CurrentProjectVariant = variantName;
                        result.CurrentVariant = sysManager14.CurrentProjectVariant ?? "";
                        result.Success = true;
                        result.Message = $"Project variant changed from '{result.PreviousVariant}' to '{result.CurrentVariant}'";
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = $"Failed to set variant '{variantName}': {ex.Message}";
                        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                        return 1;
                    }
                }

                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                return 1;
            }
            finally
            {
                vsInstance?.Close();
            }
        }
    }

    public class SetVariantResult
    {
        public string SolutionPath { get; set; } = "";
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string PreviousVariant { get; set; } = "";
        public string CurrentVariant { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }
}
