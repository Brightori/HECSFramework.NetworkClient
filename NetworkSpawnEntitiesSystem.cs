using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Unity;
using HECSFramework.Unity.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HECSFramework.Network;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    public partial class NetworkSpawnEntitiesSystem : BaseSystem, IUpdatable,
        INetworkSpawnEntitiesSystem, IReactEntity,
        IReactGlobalCommand<RemoveEntityFromClientCommand>,
        IReactGlobalCommand<SpawnEntityCommand>,
        IReactGlobalCommand<ClientConnectSuccessCommand>,
        IReactGlobalCommand<SyncEntitiesStartedNetworkCommand>
    {
        private const float EntityRemovalTimeout = 5;
        private const float SyncTimeout = 15;

        private List<(Guid, float)> awaitingDeletionEntities = new List<(Guid, float)>();
        private List<Guid> awaitingSpawnEntities = new List<Guid>();
        private HashSet<Guid> alreadyHaveTheseEntities = new HashSet<Guid>();
        private float startSyncTime;
        
        public override void InitSystem()
        {
        }
        
        public void UpdateLocal()
        {
            for (var i = 0; i < awaitingDeletionEntities.Count; i++)
            {
                var awaitingEntity = awaitingDeletionEntities[i].Item1;
                var awaitingStartTime = awaitingDeletionEntities[i].Item2;

                if (EntityManager.TryGetEntityByID(awaitingEntity, out var entity))
                {
                    EntityManager.Command(new DestroyEntityWorldCommand { Entity = entity });
                    awaitingDeletionEntities.RemoveAt(i--);
                    continue;
                }

                if (Time.time - awaitingStartTime > EntityRemovalTimeout)
                {
                    HECSDebug.LogWarning($"Cannot remove nonexistant entity: {awaitingEntity}");
                    awaitingDeletionEntities.RemoveAt(i--);
                }
            }

            if (awaitingSpawnEntities.Count != 0 && Time.time - startSyncTime > SyncTimeout)
            {
                HECSDebug.LogError($"Sync entities timeout! Entities unable to sync: {string.Join(", ", awaitingSpawnEntities)}");
                awaitingSpawnEntities.Clear();
                TrySendSyncEndedCommand();
            }
        }
        
        public void CommandGlobalReact(RemoveEntityFromClientCommand command)
        {
            if (!EntityManager.TryGetEntityByID(command.EntityToRemove, out var entity) || !entity.IsAlive)
            {
                awaitingDeletionEntities.Add((command.EntityToRemove,Time.time));
                return;
            }

            EntityManager.Command(new DestroyEntityWorldCommand { Entity = entity });
        }

        public async void CommandGlobalReact(SpawnEntityCommand command)
        {
            HECSDebug.LogDebug($"Spawning network entity: {command.CharacterGuid}");
            if (alreadyHaveTheseEntities.Contains(command.CharacterGuid))
            {
                HECSDebug.LogWarning($"Cannot spawn existing entity: {command.CharacterGuid}");
                return;
            }
            
            alreadyHaveTheseEntities.Add(command.CharacterGuid);

            var resolver = command.Entity;
            var actor = await resolver.GetNetworkActorFromResolver();
            
            if (actor == null)
            {
                HECSDebug.LogWarning($"Cannot spawn entity: {command.CharacterGuid}");
                return;
            }
            
            actor.GetOrAddComponent<ReplicatedNetworkEntityComponent>();
            HECSDebug.LogDebug($"Network entity spawn complete: {command.CharacterGuid}");

            foreach (var c in actor.GetAllComponents)
                if (c != null && c is IAfterInitSync initSync)
                    initSync.AfterInitSync();
        }
        
        public void EntityReact(IEntity entity, bool isAdded)
        {
            if (!isAdded)
                alreadyHaveTheseEntities.Remove(entity.GUID);
            else if (entity.TryGetHecsComponent(out NetworkEntityTagComponent _)) 
                alreadyHaveTheseEntities.Add(entity.GUID);
            
            if (awaitingSpawnEntities.Count == 0)
                return;
            
            awaitingSpawnEntities.Remove(entity.GUID);
            TrySendSyncEndedCommand();
        }

        public void CommandGlobalReact(ClientConnectSuccessCommand command)
        {
            foreach (var entityID in alreadyHaveTheseEntities)
            {
                if (!EntityManager.TryGetEntityByID(entityID, out var entity)) 
                    continue;
                if (entity.ContainsMask(ref HMasks.ReplicatedNetworkEntityComponent))
                    continue;

                EntityManager.GetSingleSystem<DataSenderSystem>().SendCommandToServer(new RegisterClientEntityOnServerCommand
                {
                    ClientGuid = Owner.GetNetworkClientTagComponent().ClientGuid,
                    Entity = EntityManager.ResolversMap.GetNetworkEntityResolver(entity),
                });
            }
        }

        public void CommandGlobalReact(SyncEntitiesStartedNetworkCommand command)
        {
            startSyncTime = Time.time;
            awaitingSpawnEntities = new List<Guid>(command.Entities);
            HECSDebug.LogDebug("Network entities spawning is started.");

            for (var i = 0; i < awaitingSpawnEntities.Count; i++)
            {
                var entity = awaitingSpawnEntities[i];
                if (EntityManager.TryGetEntityByID(entity, out _))
                {
                    awaitingSpawnEntities.Remove(entity);
                    i--;
                }
            }

            TrySendSyncEndedCommand();
        }

        private void TrySendSyncEndedCommand()
        {
            if (awaitingSpawnEntities.Count != 0) return;

            HECSDebug.LogDebug("Network entities spawning is complete.");
            EntityManager.TryGetEntityByComponents(out var player, ref HMasks.PlayerTagComponent);
            EntityManager.GetSingleSystem<DataSenderSystem>().SendCommandToServer(new SyncEntitiesEndedNetworkCommand { Client = player.GUID });
        }
    }

    public interface INetworkSpawnEntitiesSystem : ISystem
    {
    }
}