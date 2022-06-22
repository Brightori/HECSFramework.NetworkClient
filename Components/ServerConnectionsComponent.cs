using HECSFramework.Core;
using HECSFramework.Unity;
using System;
using System.Linq;

namespace Components
{
    [Serializable, BluePrint]
    [Documentation("Network", "компонент который содежит адреса серверов, используется в Network system при подключении ")]
    [Documentation("Client")]
    public class ServerConnectionsComponent : BaseComponent
    {
        public ServerConnectionBluePrint[] ServerConnectionBluePrints;

        public ServerConnectionBluePrint GetConnection()
        {
            if (TryGetOverrideConnection(out var connection)) return connection;
            return ServerConnectionBluePrints.First(x => x.IsActive);
        }
        
        private bool TryGetOverrideConnection(out ServerConnectionBluePrint bluePrint)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;
                
                var argument = arg.Substring(1, arg.Length - 1);
                var connection = ServerConnectionBluePrints.FirstOrDefault(a => a.name.Equals(argument));
                if (connection == null)
                    continue;

                bluePrint = connection;
                return true;
            }

            bluePrint = null;
            return false;
        }
    }
}