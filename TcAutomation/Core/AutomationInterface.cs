using System;
using System.Collections.Generic;
using System.Xml.Linq;
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
        private readonly ITcSmTreeItem _realTimeConfigTreeItem;

        // Tree item shortcuts
        private const string PLC_CONFIGURATION_SHORTCUT = "TIPC";
        private const string IO_DEVICES_SHORTCUT = "TIID";
        private const string REAL_TIME_CONFIGURATION_SETTINGS = "TIRS";

        public AutomationInterface(EnvDTE.Project project)
        {
            if (project.Object == null)
                throw new InvalidOperationException($"Project '{project.Name}' has no Object property.");

            _sysManager = (ITcSysManager10)project.Object;
            _configManager = _sysManager.ConfigurationManager;
            _plcTreeItem = _sysManager.LookupTreeItem(PLC_CONFIGURATION_SHORTCUT);
            _realTimeConfigTreeItem = _sysManager.LookupTreeItem(REAL_TIME_CONFIGURATION_SETTINGS);
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

        /// <summary>
        /// Disables all top-level I/O devices in the TwinCAT configuration.
        /// This is useful when running unit tests on a different machine than the target PLC,
        /// where the physical I/O hardware is not present and would cause errors during activation.
        /// 
        /// Uses the documented TwinCAT Automation Interface:
        /// - ITcSysManager.LookupTreeItem("TIID") to get the I/O devices root
        /// - ITcSmTreeItem.ChildCount / Child(n) to enumerate top-level devices
        /// - ITcSmTreeItem.Disabled with DISABLED_STATE.SMDS_DISABLED to disable each device
        /// </summary>
        /// <param name="disableIoDevices">If true, disable all I/O devices; if false, skip entirely</param>
        /// <returns>The number of I/O devices that were disabled</returns>
        public int DisableAllIoDevices(bool disableIoDevices = false)
        {
            if (!disableIoDevices)
            {
                return 0;
            }

            int disabledCount = 0;

            try
            {
                // Get the I/O devices root node using the documented "TIID" shortcut
                ITcSmTreeItem ioDevicesRoot = _sysManager.LookupTreeItem(IO_DEVICES_SHORTCUT);
                
                int childCount = ioDevicesRoot.ChildCount;
                Console.Error.WriteLine($"[PROGRESS] io: Found {childCount} top-level I/O device(s)");

                if (childCount == 0)
                {
                    return 0;
                }

                // Iterate through all top-level I/O devices (EtherCAT masters, EtherNet/IP, PROFINET, etc.)
                // ChildCount only counts main children (the top-level devices), not process images
                for (int i = 1; i <= childCount; i++)
                {
                    ITcSmTreeItem device = ioDevicesRoot.Child[i];
                    string deviceName = device.Name;

                    // Check current state before disabling
                    DISABLED_STATE currentState = device.Disabled;
                    
                    if (currentState == DISABLED_STATE.SMDS_DISABLED)
                    {
                        Console.Error.WriteLine($"[PROGRESS] io: Device '{deviceName}' is already disabled, skipping");
                        continue;
                    }
                    else if (currentState == DISABLED_STATE.SMDS_PARENT_DISABLED)
                    {
                        Console.Error.WriteLine($"[PROGRESS] io: Device '{deviceName}' is disabled via parent, skipping");
                        continue;
                    }

                    // Disable the device using the documented DISABLED_STATE enum
                    device.Disabled = DISABLED_STATE.SMDS_DISABLED;
                    disabledCount++;
                    Console.Error.WriteLine($"[PROGRESS] io: Disabled I/O device: '{deviceName}'");
                }

                Console.Error.WriteLine($"[PROGRESS] io: Successfully disabled {disabledCount} I/O device(s)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PROGRESS] io: Could not disable I/O devices: {ex.Message}");
                Console.Error.WriteLine("[PROGRESS] io: Continuing without disabling I/O devices...");
            }

            return disabledCount;
        }

        /// <summary>
        /// Auto-configures the real-time CPU settings for portability across different IPCs.
        /// Uses the documented TwinCAT Automation Interface via TIRS tree item.
        /// 
        /// For CI/CD test execution, we configure a safe single-core setup that works
        /// on any IPC regardless of the project's saved configuration.
        /// </summary>
        /// <param name="skipCoreAssignment">If true, skip configuration entirely</param>
        public void AssignCPUCores(bool skipCoreAssignment = false)
        {
            if (skipCoreAssignment)
            {
                Console.Error.WriteLine("[PROGRESS] config: Skipping CPU core configuration");
                return;
            }

            try
            {
                // Read the current real-time configuration using documented API:
                // LookupTreeItem("TIRS") -> ProduceXml() returns RTimeSetDef XML
                var xml = _realTimeConfigTreeItem.ProduceXml();
                var doc = XDocument.Parse(xml);

                // Navigate into the RTimeSetDef (documented structure)
                var rtimeSetDef = doc.Root?.Element("RTimeSetDef");
                if (rtimeSetDef == null)
                {
                    Console.Error.WriteLine("[PROGRESS] config: Could not read RTimeSetDef from real-time configuration");
                    return;
                }

                // Log current configuration
                var currentMaxCpus = rtimeSetDef.Element("MaxCPUs")?.Value ?? "unknown";
                var currentAffinity = rtimeSetDef.Element("Affinity")?.Value ?? "unknown";
                Console.Error.WriteLine($"[PROGRESS] config: Current RT config: MaxCPUs={currentMaxCpus}, Affinity={currentAffinity}");

                // Get system CPU count
                int systemCpuCount = Environment.ProcessorCount;
                Console.Error.WriteLine($"[PROGRESS] config: System has {systemCpuCount} logical processors");

                // Configure for portable CI/CD execution:
                // Use 1 shared RT core on CPU 0 with 80% load limit
                // This is the safest configuration that works on any IPC
                
                // Set MaxCPUs to 1 (single RT core)
                var maxCpusElement = rtimeSetDef.Element("MaxCPUs");
                if (maxCpusElement != null)
                {
                    maxCpusElement.Value = "1";
                    // Remove NonWindowsCPUs attribute if present (we want shared core)
                    maxCpusElement.RemoveAttributes();
                }
                else
                {
                    rtimeSetDef.Add(new XElement("MaxCPUs", "1"));
                }

                // Set Affinity to CPU 0 only (#x0000000000000001)
                var affinityElement = rtimeSetDef.Element("Affinity");
                if (affinityElement != null)
                {
                    affinityElement.Value = "#x0000000000000001";
                }
                else
                {
                    rtimeSetDef.Add(new XElement("Affinity", "#x0000000000000001"));
                }

                // Configure CPU 0 settings
                var cpusElement = rtimeSetDef.Element("CPUs");
                if (cpusElement == null)
                {
                    cpusElement = new XElement("CPUs");
                    rtimeSetDef.Add(cpusElement);
                }

                // Clear existing CPU elements and add just CPU 0
                cpusElement.RemoveAll();
                cpusElement.Add(new XElement("CPU",
                    new XAttribute("id", "0"),
                    new XElement("LoadLimit", "80"),        // 80% for RT, 20% for Windows
                    new XElement("BaseTime", "10000"),      // 10ms base cycle (safe default)
                    new XElement("LatencyWarning", "500")   // 500µs latency warning
                ));

                // Apply the configuration using documented ConsumeXml API
                _realTimeConfigTreeItem.ConsumeXml(doc.ToString());
                
                Console.Error.WriteLine("[PROGRESS] config: Configured portable RT settings: 1 shared core on CPU 0, 80% load limit, 10ms base time");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PROGRESS] config: Could not configure real-time settings: {ex.Message}");
                Console.Error.WriteLine("[PROGRESS] config: Continuing with existing configuration...");
            }
        }
    }

    public class PlcProjectInfo
    {
        public string Name { get; set; } = "";
        public int Index { get; set; }
        public int AmsPort { get; set; }
    }
}
