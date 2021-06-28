using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Unity;
using HECSFramework.Unity.Helpers;
using System;
using System.Linq;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    public class NetworkSpawnEntitiesSystem : BaseSystem, 
        INetworkSpawnEntitiesSystem, IReactEntity,
        IReactGlobalCommand<RemoveEntityFromClientCommand>,
        IReactGlobalCommand<SpawnCompleteCommand>,
        IReactGlobalCommand<SpawnEntityCommand>
    {
        public YieldInstruction Interval { get; } = new WaitForSeconds(2);

        private ConcurrencyList<Guid> alrdyHaveThisEntities = new ConcurrencyList<Guid>();
        private ConnectionsHolderComponent connectionsHolderComponent;
        private NetworkClientTagComponent networkClient;

        public override void InitSystem()
        {
            var mask = HMasks.GetMask<NetworkClientTagComponent>();
            if (EntityManager.TryGetEntityByComponents(out var networkClientEntity, ref mask))
            {
                networkClientEntity.TryGetHecsComponent(out networkClient);
            }

            Owner.TryGetHecsComponent(out connectionsHolderComponent);

        }

        public async void ProcessSpawnCommand(SpawnEntityCommand command)
        {
            if (command.IsNeedRecieveConfirm)
                EntityManager.GetSingleSystem<DataSenderSystem>().SendCommand(Guid.Empty, new ConfirmRecieveCommand { Index = command.Index });

            if (string.IsNullOrEmpty(command.ActorContainerID.ID))
                return;

            if (alrdyHaveThisEntities.Contains(command.CharacterGuid))
                return;

            var actor = await command.Entity.GetActorFromResolver();
            
            if (actor == null)
                alrdyHaveThisEntities.Add(command.CharacterGuid);
            else
                return;
            
            actor.GetOrAddComponent<ReplicatedNetworkEntityComponent>();
            actor.Init();
            
            //todo сюда дописать установку позиции и вращения, после того как будет понятно как разрулить трансформ компонент на сервере и клиенте
            //actor.GetHECSComponent<TransformComponent>().SetPosition()
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
            if (!isAdded)
            {
                alrdyHaveThisEntities.Remove(entity.GUID);
            }
        }
    }

    public interface INetworkSpawnEntitiesSystem : ISystem 
    {
    }
}