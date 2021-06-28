using HECSFramework.Core;
using HECSFramework.Network;
using LiteNetLib;

namespace Systems
{
    public partial class DataSenderSystem
    {
        public void SendCommandToServer<T>(T networkCommand, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkCommand
        {
            SendCommand(connectionsHolder.serverPeer, SystemGuid, networkCommand);
        }

        public void SendSyncComponentToServer<T>(T component, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkComponent
        {
            var container = EntityManager.ResolversMap.GetComponentContainer(component);
            connectionsHolder.serverPeer.Send(PackResolverContainer(container), deliveryMethod);
        }
    }

    public partial interface IDataSenderSystem : ISystem
    {
        void SendCommandToServer<T>(T networkCommand, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkCommand;
        void SendSyncComponentToServer<T>(T component, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered) where T : INetworkComponent;
    }
}