using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks;
using TcAutomation.Commands;

namespace TcAutomation
{
    /// <summary>
    /// TcAutomation CLI - TwinCAT Automation Interface wrapper
    /// 
    /// This tool provides command-line access to TwinCAT automation features
    /// with JSON output for easy integration with MCP servers and other tools.
    /// 
    /// Usage:
    ///   TcAutomation.exe build --solution "C:\path\to\solution.sln"
    ///   TcAutomation.exe info --solution "C:\path\to\solution.sln"
    /// </summary>
    class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [STAThread] // Required for COM STA thread
        static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task<int> MainAsync(string[] args)
        {
            // Root command
            var rootCommand = new RootCommand("TwinCAT Automation CLI - Build and manage TwinCAT projects");

            // === BUILD COMMAND ===
            var buildCommand = new Command("build", "Build a TwinCAT solution and return errors/warnings");
            
            var solutionOption = new Option<string>(
                aliases: new[] { "--solution", "-s" },
                description: "Path to the TwinCAT solution file (.sln)");
            solutionOption.IsRequired = true;
            
            var cleanOption = new Option<bool>(
                aliases: new[] { "--clean", "-c" },
                description: "Clean solution before building",
                getDefaultValue: () => true);
            
            var tcVersionOption = new Option<string>(
                aliases: new[] { "--tcversion", "-v" },
                description: "Force specific TwinCAT version (e.g., '3.1.4026.17')");
            
            buildCommand.AddOption(solutionOption);
            buildCommand.AddOption(cleanOption);
            buildCommand.AddOption(tcVersionOption);
            
            buildCommand.SetHandler(async (string solution, bool clean, string tcVersion) =>
            {
                var result = await BuildCommand.ExecuteAsync(solution, clean, tcVersion);
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            }, solutionOption, cleanOption, tcVersionOption);

            // === INFO COMMAND ===
            var infoCommand = new Command("info", "Get information about a TwinCAT solution");
            
            var infoSolutionOption = new Option<string>(
                aliases: new[] { "--solution", "-s" },
                description: "Path to the TwinCAT solution file (.sln)");
            infoSolutionOption.IsRequired = true;
            
            infoCommand.AddOption(infoSolutionOption);
            
            infoCommand.SetHandler(async (string solution) =>
            {
                var result = await InfoCommand.ExecuteAsync(solution);
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            }, infoSolutionOption);

            // === ADD COMMANDS TO ROOT ===
            rootCommand.AddCommand(buildCommand);
            rootCommand.AddCommand(infoCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}
