using HECSFramework.Core;
using HECSFramework.Unity;
using System;

namespace Components
{
    [Serializable, BluePrint]
    [Documentation("Tag", "Компонент метка для сущности которая отвечает за все сетевые дела")]
    [Documentation("Network")]
    public class NetworkManagerTagComponent : BaseComponent
    {
    }
}