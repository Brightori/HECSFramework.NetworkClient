using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using System;

namespace Systems
{
    public class SyncComponentsClientSystem : BaseSystem, IReactComponent, IReactEntity
    {
        private ConcurrencyList<INetworkComponent> networkComponents = new ConcurrencyList<INetworkComponent>();
        private DataSenderSystem dataSenderSystem;

        public Guid ListenerGuid { get; }
        private HECSMask networkEntityTagComponent = HMasks.GetMask<NetworkEntityTagComponent>();


        public override void InitSystem()
        {
            Owner.TryGetSystem(out dataSenderSystem);
            var networkEntities = EntityManager.Filter(networkEntityTagComponent);

            foreach (var networkEntity in networkEntities)
            {
                foreach (var nc in networkEntity.GetComponentsByType<INetworkComponent>())
                {
                    networkComponents.Add(nc);
                }
            }
        }

        public void ComponentReact(IComponent component, bool isAdded)
        {
            if (component is INetworkComponent networkComponent)
            {
                if (isAdded)
                {
                    if (!networkComponents.Contains(networkComponent))
                        networkComponents.Add(networkComponent);
                }
                else
                    networkComponents.Remove(networkComponent);
            }
        }

        public void EntityReact(IEntity entity, bool isAdded)
        {
            if (entity.ContainsMask(ref networkEntityTagComponent))
            {
                foreach (var nc in entity.GetComponentsByType<INetworkComponent>())
                {
                    if (isAdded)
                    {
                        if (!networkComponents.Contains(nc))
                            networkComponents.Add(nc);
                    }
                    else
                        networkComponents.Remove(nc);
                }
            }
        }

        public void SyncComponents()
        { 
            var currentCount = networkComponents.Count;

            for (int i = 0; i < currentCount; i++)
            {
                if (networkComponents.Data[i].IsDirty && networkComponents.Data[i].IsAlive)
                {
                    if (networkComponents.Data[i] is IUnreliable)
                        dataSenderSystem.SendSyncComponentToServer(networkComponents.Data[i], LiteNetLib.DeliveryMethod.Unreliable);
                    else
                        dataSenderSystem.SendSyncComponentToServer(networkComponents.Data[i]);
                }

                networkComponents.Data[i].IsDirty = false;
                networkComponents.Data[i].Version++;
            }
        }
    }
}