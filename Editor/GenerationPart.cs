using HECSFramework.Core;
using HECSFramework.Core.Generator;
using System.Linq;

namespace HECSFramework.Unity.Generator
{
    public partial class UnityProcessGeneration
    {
        partial void GenerateNetworkStaff()
        {
            var codogen = new CodeGenerator();
            var commandGlobal = typeof(INetworkCommand);
            var networkCommands = CodeGenerator.Assembly.Where(p => commandGlobal.IsAssignableFrom(p) && !p.IsGenericType && !p.IsAbstract && !p.IsInterface).ToList();

            SaveToFile("CommandsMap.cs", codogen.GenerateNetworkCommandsMap(networkCommands));
        }
    }
}