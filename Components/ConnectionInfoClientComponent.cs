using System;
using HECSFramework.Core;
using UnityEngine;

namespace Components
{
    [Serializable, Documentation(Doc.Network, Doc.Client, ("holds info about count of connection attempts, address and port of current server, room index and other staff, its main component for network system")) ]
    public sealed class ConnectionInfoClientComponent : BaseComponent
    {
        [SerializeField] private int maxAttempts = 2000;
        [SerializeField]  private float intervalConnectTime = 5;

        public ConnectionInfoOfServer ServerInfo  = new ConnectionInfoOfServer { Key = "ClausUmbrella" };

        [HideInInspector]
        public int RoomWorldIndex = 0;

        [HideInInspector]
        public int CurrentAttempts = 0;

        [HideInInspector]
        public float NextConnectTime =0;

        public int MaxAttemtps { get => maxAttempts; }
        public float IntervalConnectTime { get => intervalConnectTime; }
    }

    public enum NetWorkSystemState { Wait, Connect, BeforeSync, Sync, Disconnect, FailToConnect }
    
    [Serializable]
    public struct ConnectionInfoOfServer
    {
        public string Address;
        public int Port;  
        public string Key;
    }
}