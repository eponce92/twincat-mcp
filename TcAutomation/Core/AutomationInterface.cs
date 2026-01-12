using System;
using System.Collections.Generic;
using TCatSysManagerLib;

namespace TcAutomation.Core
{
    /// <summary>
    /// Wrapper for TwinCAT Automation Interface.
    /// Provides simplified access to TwinCAT System Manager functionality.
    /// </summary>
    public class AutomationInterface
    {
        private readonly ITcSysManager10 _sysManager;
        private readonly ITcConfigManager _configManager;
        private readonly ITcSmTreeItem _plcTreeItem;

        public AutomationInterface(EnvDTE.Project project)
        {
            if (project.Object == null)
                throw new InvalidOperationException($"Project '{project.Name}' has no Object property.");

            _sysManager = (ITcSysManager10)project.Object;
            _configManager = _sysManager.ConfigurationManager;
            _plcTreeItem = _sysManager.LookupTreeItem("TIPC"); // PLC Configuration
        }

        public AutomationInterface(VisualStudioInstance vsInstance)
            : this(vsInstance.GetProject())
        { }

        /// <summary>
        /// Get the TwinCAT System Manager interface.
        /// </summary>
        public ITcSysManager10 SystemManager => _sysManager;

        /// <summary>
        /// Get the PLC Configuration tree item.
        /// </summary>
        public ITcSmTreeItem PlcTreeItem => _plcTreeItem;

        /// <summary>
        /// Get or set the target AMS Net ID.
        /// </summary>
        public string TargetNetId
        {
            get => _sysManager.GetTargetNetId();
            set => _sysManager.SetTargetNetId(value);
        }

        /// <summary>
        /// Get or set the active target platform.
        /// </summary>
        public string ActiveTargetPlatform
        {
            get => _configManager.ActiveTargetPlatform;
            set => _configManager.ActiveTargetPlatform = value;
        }

        /// <summary>
        /// Activate the configuration on the target.
        /// </summary>
        public void ActivateConfiguration()
        {
            _sysManager.ActivateConfiguration();
        }

        /// <summary>
        /// Start or restart TwinCAT on the target.
        /// </summary>
        public void StartRestartTwinCAT()
        {
            _sysManager.StartRestartTwinCAT();
        }

        /// <summary>
        /// Get list of PLC projects with their info.
        /// </summary>
        public List<PlcProjectInfo> GetPlcProjects()
        {
            var projects = new List<PlcProjectInfo>();
            
            for (int i = 1; i <= _plcTreeItem.ChildCount; i++)
            {
                var plcProject = _plcTreeItem.Child[i];
                var info = new PlcProjectInfo
                {
                    Name = plcProject.Name,
                    Index = i
                };

                // Try to get AMS port from XML
                try
                {
                    var xml = plcProject.ProduceXml();
                    // Parse AMS port from XML if needed
                    // For now, use default port calculation
                    info.AmsPort = 850 + i;
                }
                catch { }

                projects.Add(info);
            }

            return projects;
        }
    }

    public class PlcProjectInfo
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public int AmsPort { get; set; }
    }
}
