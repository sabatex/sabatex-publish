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
            GlobalConfigManager.SetValue(parseResult.GetValue<string>("--NuGetDebugPackagePath"),
                                                                    parseResult.GetValue<string>("--NuGetKeyPath"),
                                                                    parseResult.GetValue<bool>("--shared"));
            return 0;
        }
    }
}
