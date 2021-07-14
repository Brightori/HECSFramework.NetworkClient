using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using System;

namespace Systems
{
    public class SyncComponentsClientSystem : BaseSystem, IReactComponent, IReactEntity
    {
        private ConcurrencyList<INetworkComponent> networkComponents = new ConcurrencyList<INetworkComponent>();
        private IDataSenderSystem dataSenderSystem;

        public Guid ListenerGuid { get; }

        public override void InitSystem()
        {
            Owner.TryGetSystem(out dataSenderSystem);
            var networkEntities = EntityManager.Filter(HMasks.NetworkEntityTagComponent);

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
            if (entity.ContainsMask(ref HMasks.NetworkEntityTagComponent))
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
                if (networkComponents[i].IsDirty && networkComponents[i].IsAlive)
                {
                    if (networkComponents[i] is IUnreliable)
                        dataSenderSystem.SendSyncComponentToServer(networkComponents[i], LiteNetLib.DeliveryMethod.Unreliable);
                    else
                        dataSenderSystem.SendSyncComponentToServer(networkComponents[i]);
                }
            }
        }
    }
}