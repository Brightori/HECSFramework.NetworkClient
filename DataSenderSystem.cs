using HECSFramework.Core;
using HECSFramework.Network;
using LiteNetLib;
using System;

namespace Systems
{
    public partial class DataSenderSystem
    {
        public void SendCommandToServer<T>(T networkCommand, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkCommand, IData
        {
            SendCommand(connectionsHolder.serverPeer, Guid.Empty, networkCommand);
        }    
        
        public void SendCommandToServer<T>(T networkCommand, Guid address, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkCommand, IData
        {
            SendCommand(connectionsHolder.serverPeer, address, networkCommand);
        }

        public void SendSyncComponentToServer<T>(T component, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkComponent 
        {
            var container = EntityManager.ResolversMap.GetComponentContainer(component);
            connectionsHolder.serverPeer.Send(PackResolverContainer(container), deliveryMethod);
        }
    }
}