using HECSFramework.Core;
using HECSFramework.Unity;
using System;
using UnityEngine;

namespace Components
{
    [Serializable, BluePrint]
    [Documentation("Network", "компонент который содежит адреса серверов, используется в Network system при подключении ")]
    [Documentation("Client")]
    public class ServerConnectionsComponent : BaseComponent
    {
        public ServerConnectionBluePrint[] ServerConnectionBluePrints;
    }
}