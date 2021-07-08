using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using System.Linq;
using System.Threading.Tasks;
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

            if (actorID == null)
                return null;

            var container = actorID as ActorContainerID;

            var loaded = await Addressables.LoadAssetAsync<ScriptableObject>(container.ID).Task;
            var loadedContainer = loaded as EntityContainer;
            var actor = await loadedContainer.GetActor();
            actor.LoadEntityFromResolver(entityResolver, needForceAdd);

            for (int i = 0; i < actor.GetAllComponents.Length; i++)
            {
                if (actor.GetAllComponents[i] is INotReplicable)
                    actor.GetAllComponents[i] = null;
            }

            var systems = actor.GetAllSystems.ToArray();

            foreach (var system in systems)
            {
                if (system is INotReplicable)
                {
                    actor.GetAllSystems.Remove(system);
                }
            }

            actor.SetGuid(entityResolver.Guid);
            return actor;
        }
    }
}
