using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using System.Linq;
using System.Threading.Tasks;
using Systems;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HECSFramework.Unity.Helpers
{
    public static partial class SerializeExtentions
    {

        public static async Task<IActor> GetNetworkActorFromResolver(this EntityResolver entityResolver, bool needForceAdd = true, int worldIndex = 0)
        {
            var unpack = new UnPackEntityResolver(entityResolver);
            var actorID = unpack.Components.FirstOrDefault(x => x is ActorContainerID containerID);
            var customContainerComponent = unpack.Components.FirstOrDefault(x => x is CustomEntityContainerComponent containerID);

            if (actorID == null)
                return null;

            var container = actorID as ActorContainerID;

            if (customContainerComponent != null)
            {
                CustomEntityContainerComponent customContainer = customContainerComponent as CustomEntityContainerComponent;
                container.ID = customContainer.ContainerGUID;
            }

            var loaded = await Addressables.LoadAssetAsync<ScriptableObject>(container.ID).Task;
            var loadedContainer = loaded as EntityContainer;

            IActor actor = null;

            if (loadedContainer.IsHaveComponent<PoolableTagComponent>())
            {
                var viewRef = loadedContainer.GetComponent<ViewReferenceComponent>();
                actor = await EntityManager.GetSingleSystem<PoolingSystem>().GetActorFromPool(viewRef);
                loadedContainer.Init(actor);
            }
            else
                actor = await loadedContainer.GetActor();

            actor.LoadEntityFromResolver(entityResolver, needForceAdd);
            actor.SetGuid(entityResolver.Guid);
            return actor;
        }
        
        /// <summary>
        /// clean entity from server data
        /// </summary>
        /// <param name="entity"></param>
        public static void CleanClientEntity(this IEntity entity)
        {
            for (int i = 0; i < entity.GetAllComponents.Length; i++)
            {
                var c = entity.GetAllComponents[i];

                if (c is IServerSide)
                {
                    entity.RemoveHecsComponent(entity.GetAllComponents[i]);
                }
            }

            for (var i = 0; i < entity.GetAllSystems.Count; i++)
            {
                var system = entity.GetAllSystems[i];
                if (system is IServerSide) 
                    entity.GetAllSystems.RemoveAt(i--);
            }
        }
        
        public static void CleanReplicatedEntity(this IEntity entity)
        {
            for (int i = 0; i < entity.GetAllComponents.Length; i++)
            {
                var c = entity.GetAllComponents[i];

                if (c is INotReplicable)
                {
                    entity.RemoveHecsComponent(entity.GetAllComponents[i]);
                }
            }

            for (var i = 0; i < entity.GetAllSystems.Count; i++)
            {
                var system = entity.GetAllSystems[i];
                if (system is INotReplicable) 
                    entity.GetAllSystems.RemoveAt(i--);
            }
        }
    }
}