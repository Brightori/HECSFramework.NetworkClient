using HECSFramework.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HECSFramework.Unity
{
    public abstract partial class BaseGameController
    {
        [PropertyOrder(100)]
        [SerializeField] private ActorContainer networkManagerContainer;

        private Entity networkManager;

        partial void NetworkAwake()
        {
            networkManager = new Entity("NetworkManager");
            networkManagerContainer.Init(networkManager);
        }

        partial void InitNetWorkEntities()
        {
            networkManager.Init();
        }
    }
}