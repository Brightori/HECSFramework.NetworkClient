using HECSFramework.Core;
using System;
using HECSFramework.Unity;

namespace Components
{
    [Documentation("Network", "Компонент который вешает на одну из основных сущностей, и айди сущности будет являться айди этого клиента")]
    [Serializable, BluePrint]
    public class NetworkClientTagComponent : BaseComponent
    {
        public Guid ClientGuid => EntityManager.GetSingleComponent<PlayerTagComponent>().Owner.GUID;
        
        [Field(0)]
        public int WorldIndex;
    }
}