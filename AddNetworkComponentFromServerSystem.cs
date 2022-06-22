using Commands;
using HECSFramework.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems
{
    [Serializable]
    [Documentation("Network", "Client", "Система которая получает и добавляет компоненты с сервера")]
    public class AddNetworkComponentFromServerSystem : BaseSystem, IAddNetworkComponentFromServerSystem,
        IReactGlobalCommand<AddedComponentOnServerCommand>,
        IReactGlobalCommand<RemovedComponentOnServerCommand>, 
        IUpdatable
    {
        private const float Timeout = 5;
        
        private List<(float waitEndTime, AddedComponentOnServerCommand command)> addCommandsQueue = new List<(float waitEndTime, AddedComponentOnServerCommand command)>();
        private List<(float waitEndTime, RemovedComponentOnServerCommand command)> removeCommandsQueue = new List<(float waitEndTime, RemovedComponentOnServerCommand command)>();

        public override void InitSystem()
        {
        }

        public void CommandGlobalReact(AddedComponentOnServerCommand command)
        {
            if (!EntityManager.TryGetEntityByID(command.Entity, out var entity))
            {
                addCommandsQueue.Add((Time.time + Timeout, command));
                return;
            }

            EntityManager.ResolversMap.LoadComponentFromContainer(ref command.component, ref entity, true);
        }

        public void CommandGlobalReact(RemovedComponentOnServerCommand command)
        {
            if (!EntityManager.TryGetEntityByID(command.Entity, out var entity))
            {
                removeCommandsQueue.Add((Time.time + Timeout, command));
                return;
            }

            RemoveComponent(command.TypeIndex, entity);
        }

        private static void RemoveComponent(int index, IEntity entity)
        {
            if (!TypesMap.GetComponentInfo(index, out var maskInfo))
            {
                HECSDebug.LogWarning($"Component not found: {index}");
                return;
            }

            entity.RemoveHecsComponent(maskInfo.ComponentsMask);
        }

        public void UpdateLocal()
        {
            ProcessAddQueue();
            ProcessRemoveQueue();
        }

        private void ProcessAddQueue()
        {
            for (var i = 0; i < addCommandsQueue.Count; i++)
            {
                var awaitingStartTime = addCommandsQueue[i].Item1;
                var awaitingCommand = addCommandsQueue[i].Item2;

                if (EntityManager.TryGetEntityByID(awaitingCommand.Entity, out var entity))
                {
                    EntityManager.ResolversMap.LoadComponentFromContainer(ref awaitingCommand.component, ref entity, true);
                    addCommandsQueue.RemoveAt(i--);
                    continue;
                }

                if (Time.time - awaitingStartTime > 0)
                {
                    HECSDebug.LogWarning($"Entity to add component not found: {awaitingCommand.Entity}");
                    addCommandsQueue.RemoveAt(i--);
                }
            }
        }

        private void ProcessRemoveQueue()
        {
            for (var i = 0; i < removeCommandsQueue.Count; i++)
            {
                var awaitingStartTime = removeCommandsQueue[i].Item1;
                var awaitingCommand = removeCommandsQueue[i].Item2;

                if (EntityManager.TryGetEntityByID(awaitingCommand.Entity, out var entity))
                {
                    RemoveComponent(awaitingCommand.TypeIndex, entity);
                    removeCommandsQueue.RemoveAt(i--);
                    continue;
                }

                if (Time.time - awaitingStartTime > 0)
                {
                    HECSDebug.LogWarning($"Entity to remove component not found: {awaitingCommand.Entity}");
                    removeCommandsQueue.RemoveAt(i--);
                }
            }
        }
    }

    public interface IAddNetworkComponentFromServerSystem : ISystem { }
}