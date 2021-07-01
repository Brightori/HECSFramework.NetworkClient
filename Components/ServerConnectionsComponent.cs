using HECSFramework.Core;
using HECSFramework.Unity;
using System;
using UnityEngine;

namespace Components
{
    [Serializable, BluePrint]
    [Documentation("Network", "��������� ������� ������� ������ ��������, ������������ � Network system ��� ����������� ")]
    [Documentation("Client")]
    public class ServerConnectionsComponent : BaseComponent
    {
        public ServerConnectionBluePrint[] ServerConnectionBluePrints;
    }
}