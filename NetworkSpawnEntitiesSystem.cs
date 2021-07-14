using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Unity;
using HECSFramework.Unity.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    public class NetworkSpawnEntitiesSystem : BaseSystem,
        INetworkSpawnEntitiesSystem, IReactEntity, IAfterEntityInit,
        IReactGlobalCommand<RemoveEntityFromClientCommand>,
        IReactGlobalCommand<SpawnCompleteCommand>,
        IReactGlobalCommand<SpawnEntityCommand>,
        IReactGlobalCommand<ClientConnectSuccessCommand>
    {
        public YieldInstruction Interval { get; } = new WaitForSeconds(2);

        private HashSet<Guid> alrdyHaveThisEntities = new HashSet<Guid>();
        private ConnectionsHolderComponent connectionsHolderComponent;
        private IDataSenderSystem dataSenderSystem;
        private NetworkClientTagComponent networkClient;

        private bool isConnected;

        private HECSMask replicatedComponentMask = HMasks.GetMask<ReplicatedNetworkEntityComponent>();
        private HECSMask networkEntityTagMask = HMasks.GetMask<NetworkEntityTagComponent>();

        public override void InitSystem()
        {
            Owner.TryGetHecsComponent(out networkClient);
            Owner.TryGetHecsComponent(out connectionsHolderComponent);
        }

        public void AfterEntityInit()
        {
            Owner.TryGetSystem(out dataSenderSystem);
        }

        public async void ProcessSpawnCommand(SpawnEntityCommand command)
        {
            //todo сюда дописать установку позиции и вращения, после того как будет понятно как разрулить трансформ компонент на сервере и клиенте
            //actor.GetHECSComponent<TransformComponent>().SetPosition()
            await Task.Run(()=> SpawnNetworkEntity(command));
        }

        private async Task SpawnNetworkEntity(SpawnEntityCommand command)
        {
            if (command.IsNeedRecieveConfirm)
                EntityManager.GetSingleSystem<DataSenderSystem>().SendCommandToServer(new ConfirmRecieveCommand { Index = command.Index });

            if (alrdyHaveThisEntities.Contains(command.CharacterGuid))
                return;

            var resolver = command.Entity;
            var unpack = new UnPackEntityResolver(resolver);
            
            var actor = await resolver.GetNetworkActorFromResolver();
            if (actor == null)
            {
                Debug.LogAssertion("не смогли заспаунить актора " + command.CharacterGuid);
                return;
            }
            else
                alrdyHaveThisEntities.Add(command.CharacterGuid);

            actor.GetOrAddComponent<ReplicatedNetworkEntityComponent>();
            actor.Init();
        }


        public void CommandGlobalReact(RemoveEntityFromClientCommand command)
        {
            if (!EntityManager.TryGetEntityByID(command.EntityToRemove, out var entity))
                return;

            entity.HecsDestroy();
        }

        public void CommandGlobalReact(SpawnCompleteCommand command)
        {
            if (command.SyncIndex == connectionsHolderComponent.SyncIndex)
                return;

            if (command.WorldIndex != networkClient.WorldIndex)
                return;

            var currentNetworkEntities = EntityManager.Filter(HMasks.GetMask<ReplicatedNetworkEntityComponent>(), networkClient.WorldIndex);

            foreach (var e in currentNetworkEntities)
            {
                if (e.TryGetHecsComponent(out WorldSliceIndexComponent sliceIndex) &&
                    sliceIndex.Index > command.SyncIndex)
                    continue;

                if (command.SpawnEntities.Any(x => x.CharacterGuid == e.GUID))
                    continue;
                else
                    e.HecsDestroy();
            }

            foreach (var spawn in command.SpawnEntities)
            {
                if (EntityManager.TryGetEntityByID(spawn.CharacterGuid, out var entity))
                    continue;

                ProcessSpawnCommand(spawn);
            }

            connectionsHolderComponent.SyncIndex = command.SyncIndex;
        }

        public void CommandGlobalReact(SpawnEntityCommand command)
            => ProcessSpawnCommand(command);

        public void EntityReact(IEntity entity, bool isAdded)
        {
            if (isAdded)
            {
                if (entity.ContainsMask(ref networkEntityTagMask))
                {
                    if (isConnected)
                    {
                        dataSenderSystem.SendCommandToServer(new SpawnEntityCommand
                        {
                            CharacterGuid = entity.GUID,
                            ClientGuid = networkClient.ClientGuid,
                            Entity = EntityManager.ResolversMap.GetNetworkEntityResolver(entity),
                            Index = 0,
                            IsNeedRecieveConfirm = false,
                        });
                    }
                    else
                    {
                        alrdyHaveThisEntities.Add(entity.GUID);
                    }
                }
            }
            else
            {
                alrdyHaveThisEntities.Remove(entity.GUID);
            }
        }

        public void CommandGlobalReact(ClientConnectSuccessCommand command)
        {
            if (dataSenderSystem == null)
            {
                Owner.TryGetSystem(out dataSenderSystem);
            }

            foreach (var entityID in alrdyHaveThisEntities)
            {
                if (EntityManager.TryGetEntityByID(entityID, out var entity))
                {
                    if (entity.ContainsMask(ref replicatedComponentMask))
                        continue;

                    dataSenderSystem.SendCommandToServer(new RegisterClientEntityOnServerCommand
                    {
                        ClientGuid = networkClient.ClientGuid,
                        Entity = EntityManager.ResolversMap.GetNetworkEntityResolver(entity),
                    }, LiteNetLib.DeliveryMethod.ReliableUnordered);
                }
            }
        }
    }

    public interface INetworkSpawnEntitiesSystem : ISystem
    {
    }
}