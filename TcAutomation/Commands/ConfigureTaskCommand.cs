using System;
using System.Text.Json;
using System.Xml;
using TcAutomation.Core;
using TCatSysManagerLib;

namespace TcAutomation.Commands
{
    /// <summary>
    /// Configures a real-time task (enable/disable, autostart).
    /// Uses native TwinCAT Automation Interface API where possible:
    /// - ITcSmTreeItem.Disabled for enable/disable (native API)
    /// - XML for AutoStart (no native API available)
    /// </summary>
    public static class ConfigureTaskCommand
    {
        private const string REAL_TIME_TASKS_SHORTCUT = "TIRT";

        public static int Execute(string solutionPath, string taskName, bool? enable, bool? autoStart, string? tcVersion)
        {
            VisualStudioInstance? vsInstance = null;
            var result = new ConfigureTaskResult();

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
                result.TaskName = taskName;

                // Get the real-time tasks tree item
                ITcSmTreeItem tasksTreeItem;
                try
                {
                    tasksTreeItem = automation.SystemManager.LookupTreeItem(REAL_TIME_TASKS_SHORTCUT);
                }
                catch
                {
                    result.ErrorMessage = "Real-time tasks tree not found in project";
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    return 1;
                }

                // Find the specified task
                ITcSmTreeItem? targetTask = null;
                for (int i = 1; i <= tasksTreeItem.ChildCount; i++)
                {
                    var taskItem = tasksTreeItem.Child[i];
                    string xml = taskItem.ProduceXml();
                    string itemName = GetItemNameFromXml(xml);
                    
                    if (taskItem.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase) ||
                        itemName.Equals(taskName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetTask = taskItem;
                        break;
                    }
                }

                if (targetTask == null)
                {
                    result.ErrorMessage = $"Task '{taskName}' not found. Available tasks: ";
                    for (int i = 1; i <= tasksTreeItem.ChildCount; i++)
                    {
                        result.ErrorMessage += tasksTreeItem.Child[i].Name;
                        if (i < tasksTreeItem.ChildCount) result.ErrorMessage += ", ";
                    }
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    return 1;
                }

                // Read current XML for AutoStart (no native property available)
                string currentXml = targetTask.ProduceXml();
                
                // Get previous state - Disabled from native property, AutoStart from XML
                bool wasDisabled = (targetTask.Disabled != DISABLED_STATE.SMDS_NOT_DISABLED);
                bool wasAutoStart = GetAutoStartFromXml(currentXml);
                result.PreviousDisabled = wasDisabled;
                result.PreviousAutoStart = wasAutoStart;

                // Apply changes
                bool newDisabled = enable.HasValue ? !enable.Value : wasDisabled;
                bool newAutoStart = autoStart.HasValue ? autoStart.Value : wasAutoStart;

                // Use native API for Disabled property
                targetTask.Disabled = newDisabled ? DISABLED_STATE.SMDS_DISABLED : DISABLED_STATE.SMDS_NOT_DISABLED;
                
                // Use XML only for AutoStart (no native API)
                if (newAutoStart != wasAutoStart)
                {
                    string newXml = SetAutoStartInXml(currentXml, newAutoStart);
                    if (!string.IsNullOrEmpty(newXml))
                    {
                        targetTask.ConsumeXml(newXml);
                    }
                }
                
                // Wait a moment for changes to take effect
                System.Threading.Thread.Sleep(1000);

                result.NewDisabled = newDisabled;
                result.NewAutoStart = newAutoStart;
                result.Success = true;
                result.Message = $"Task '{taskName}' configured: Disabled={newDisabled}, AutoStart={newAutoStart}";

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

        private static string GetItemNameFromXml(string xml)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            var itemNameNode = xmlDoc.SelectSingleNode("/TreeItem/ItemName");
            return itemNameNode?.InnerText ?? "";
        }

        private static bool GetAutoStartFromXml(string xml)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var autoStartNode = xmlDoc.SelectSingleNode("/TreeItem/TaskDef/AutoStart");
            if (autoStartNode != null)
            {
                return autoStartNode.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static string SetAutoStartInXml(string xml, bool autoStart)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var autoStartNode = xmlDoc.SelectSingleNode("/TreeItem/TaskDef/AutoStart");
            if (autoStartNode != null)
            {
                autoStartNode.InnerText = autoStart.ToString().ToLower();
                return xmlDoc.OuterXml;
            }

            return "";
        }
    }

    public class ConfigureTaskResult
    {
        public string SolutionPath { get; set; } = "";
        public string TaskName { get; set; } = "";
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool PreviousDisabled { get; set; }
        public bool PreviousAutoStart { get; set; }
        public bool NewDisabled { get; set; }
        public bool NewAutoStart { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
