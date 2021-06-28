using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Core.Helpers;
using HECSFramework.Network;
using HECSFramework.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    public class NetworkSyncEntitiesSystem : BaseSystem, INetworkSyncEntitiesSystem,
        IReactComponent, IReactEntity, IAfterEntityInit,
        IReactGlobalCommand<RequestSyncEntitiesNetworkCommand>,
        IReactGlobalCommand<DeltaSliceNetworkCommand>,
        IReactGlobalCommand<AddOrRemoveComponentToClientCommand>
    {
        private List<INetworkComponent> networkComponents = new List<INetworkComponent>(32);

        [Required] private ConnectionsHolderComponent connectionHolderComponent;
        
        private NetworkClientTagComponent networkClient;
        private ResolversMap resolversMap = new ResolversMap();
        private ConcurrencyList<IEntity> currentNetworkEntities = EntityManager.Filter(HMasks.GetMask<ReplicatedNetworkEntityComponent>());

        private HECSMask networkEntityTag = HMasks.GetMask<NetworkEntityTagComponent>();
        private IDataSenderSystem dataSenderSystem;

        public Guid ListenerGuid => SystemGuid;

        public override void InitSystem()
        {
            connectionHolderComponent = Owner.GetHECSComponent<ConnectionsHolderComponent>();

            foreach (var e in EntityManager.Worlds[0].Entities)
            {
                if (e.ContainsMask(ref networkEntityTag))
                {
                    foreach (var nc in e.GetComponentsByType<INetworkComponent>())
                        networkComponents.AddOrRemoveElement(nc, true);
                }
            }
        }

        public void AfterEntityInit()
        {
            Owner.TryGetSystem(out dataSenderSystem);
        }

        public void CommandGlobalReact(RequestSyncEntitiesNetworkCommand command)
        {
            if (command.Index == connectionHolderComponent.SyncIndex)
                return;

            if (Owner.TryGetSystem(out INetworkSystem networkSystem))
            {
                if (!networkSystem.IsReady)
                    return;
            }

            dataSenderSystem.SendCommandToServer(new SyncClientNetworkCommand { ClientGuid = networkClient.Owner.GUID, World = 0 });
        }

        public void ComponentReact(IComponent component, bool isAdded)
        {
            if (component is INetworkComponent netComponent && netComponent.Owner.ContainsMask(ref networkEntityTag))
            {
                networkComponents.AddOrRemoveElement(netComponent, isAdded);
            }
        }

        public void EntityReact(IEntity entity, bool isAdded)
        {
            if (!entity.ContainsMask(ref networkEntityTag)) return;

            foreach (var component in entity.GetComponentsByType<INetworkComponent>())
                    networkComponents.AddOrRemoveElement(component, isAdded);
        }

        public void SyncNetworkComponents()
        {
            foreach (var n in networkComponents)
            {
                if (!n.IsDirty)
                    continue;
                if (!n.Owner.IsInited)
                    continue;
                if (!n.Owner.IsAlive)
                    continue;
            
                dataSenderSystem.SendSyncComponentToServer(n);
                n.IsDirty = false;
            }
        }

        public void SyncNetworkEntities()
        {

        }

        public override void Dispose()
        {
            networkComponents.Clear();
            base.Dispose();
        }

        public bool Equals(ISystem other)
            => other is INetworkSyncEntitiesSystem;

        
        public void CommandGlobalReact(AddOrRemoveComponentToClientCommand command)
        {
            if (EntityManager.TryGetEntityByID(command.Entity, out var entity))
            {
                var component = resolversMap.GetComponentFromContainer(command.component);
                
                if (TypesMap.GetComponentInfo(command.component.TypeHashCode, out var mask))
                {
                    if (command.IsAdded)
                    {
                        if (entity.ContainsMask(ref mask.ComponentsMask))
                            resolversMap.ProcessResolverContainer(ref command.component, ref entity);
                        else
                            entity.AddHecsComponent(component);
                    }
                    else
                        entity.RemoveHecsComponent(mask.ComponentsMask);
                }
                Debug.Log($"получили компонент  c сервера {mask.ComponentName} для ентити {entity.GUID} {entity.ID}");
            }
        }

        public void CommandGlobalReact(DeltaSliceNetworkCommand command)
        {
            if (command.CurrentEntities == null)
                return;
            
            connectionHolderComponent.SyncIndex = command.CurrentSliceIndex;

            foreach (var e in currentNetworkEntities)
            {
                if (command.CurrentEntities.Contains(e.GUID))
                    continue;
                else
                    e.HecsDestroy();
            }

            foreach (var guid in command.CurrentEntities)
            {
                if (currentNetworkEntities.Any(x => x.GUID == guid))
                    continue;

                dataSenderSystem.SendCommandToServer(new RequestEntityFromServerNetworkCommand { ClientID = networkClient.ClientGuid, NeededEntity = guid });
            }
        }
    }

    public interface INetworkSyncEntitiesSystem : ISystem
    {
        void SyncNetworkComponents();
        void SyncNetworkEntities();
    }
}