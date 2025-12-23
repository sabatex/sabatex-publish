using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sabatex_publish
{
    internal sealed class SetAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            var nugetDebugPath = parseResult.GetValue<string>("--NuGetDebugPackagePath");
            var nugetKeyPath = parseResult.GetValue<string>("--NuGetKeyPath");
            var shared = parseResult.GetValue<bool>("--shared");

            //# Validate required parameters
            if (string.IsNullOrWhiteSpace(nugetDebugPath) && string.IsNullOrWhiteSpace(nugetKeyPath))
            {
                Console.WriteLine("Error: At least one parameter (--NuGetDebugPackagePath or --NuGetKeyPath) must be provided.");
                return 1;
            }

            GlobalConfigManager.SetValue(
                nugetDebugPath ?? string.Empty,
                nugetKeyPath ?? string.Empty,
                shared);
            
            return 0;
        }
    }
}
