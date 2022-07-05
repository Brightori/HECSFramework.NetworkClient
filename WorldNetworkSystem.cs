﻿using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using HECSFramework.Unity;
using LiteNetLib;
using MessagePack;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    [RequiredAtContainer(typeof(ServerConnectionsComponent), typeof(NetworkClientTagComponent))]
    [Documentation(Doc.Network, "The system responsible for working with the world game server")]
    public class WorldNetworkSystem : BaseSystem,
        INetworkSystem, ICustomUpdatable, ILateStart, IUpdatable,
        IReactGlobalCommand<ClientConnectSuccessCommand>,
        IReactGlobalCommand<SyncServerComponentsCommand>, IOnApplicationQuit
    {

        [Required] private DataSenderSystem dataSenderSystem;
        [Required] private ConnectionsHolderComponent connectionHolderComponent;
        [Required] private ConnectionInfoClientComponent connectionInfoClientComponent;

        private HECSMask Replicated = HMasks.GetMask<ReplicatedNetworkEntityComponent>();

        private NetManager client;
        private NetPeer peer;
        
        public NetWorkSystemState State { get; private set; } = NetWorkSystemState.Wait;
        
        public YieldInstruction Interval => interval;
        private YieldInstruction interval = new WaitForSeconds(0.02f);
        
        private IDataProcessor dataProcessor = new HECSDataProcessor();

        public bool IsReady { get; private set; }
        public Guid ClientGUID { get; private set; }

        public override void InitSystem()
        {
            Owner.TryGetSystem(out dataSenderSystem);
            connectionHolderComponent = Owner.GetHECSComponent<ConnectionsHolderComponent>();
            client = new NetManager(connectionHolderComponent.Listener);
        }

        private void ListenerOnPeerConnectedEvent(NetPeer peer)
        {
            State = NetWorkSystemState.BeforeSync;
        }

        private void ListenerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader netPacketReader, DeliveryMethod deliveryMethod)
        {
            var bytes = netPacketReader.GetRemainingBytes();

            if (bytes.Length == 0)
                return;

            var message = MessagePackSerializer.Deserialize<ResolverDataContainer>(bytes);
            dataProcessor.Process(message, Owner.World);
        }

        public void InitClient()
        {
            client.Start();
            connectionHolderComponent.Listener.NetworkReceiveEvent += ListenerOnNetworkReceiveEvent;
            connectionHolderComponent.Listener.PeerDisconnectedEvent += ListenerOnPeerDisconnectedEvent;
            connectionHolderComponent.Listener.PeerConnectedEvent += ListenerOnPeerConnectedEvent;
            connectionHolderComponent.Listener.NetworkErrorEvent += ListenerOnNetworkErrorEvent;
        }

        public void Stop()
        {
            if (peer != null)
            {
                client.DisconnectPeer(peer);
            }

            connectionHolderComponent.Listener.NetworkReceiveEvent -= ListenerOnNetworkReceiveEvent;
            connectionHolderComponent.Listener.PeerDisconnectedEvent -= ListenerOnPeerDisconnectedEvent;
            connectionHolderComponent.Listener.PeerConnectedEvent -= ListenerOnPeerConnectedEvent;
            connectionHolderComponent.Listener.NetworkErrorEvent -= ListenerOnNetworkErrorEvent;
            client.Stop();
        }

        private void ListenerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
            => Debug.LogError(socketerror.ToString());

        private void ListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
        {
            Debug.Log($"Disconnected. Reason: {disconnectinfo.Reason.ToString()}");
            EntityManager.GlobalCommand(new DisconnectedClientCommand { Reason = disconnectinfo.Reason });
        }

        public bool Equals(ISystem other)
            => other is INetworkSystem;


        public override void Dispose()
        {
            Stop();
            client.PollEvents();
            base.Dispose();
        }

        private void Connect(string host, int port, string serverKey)
        {
            peer?.Disconnect();
            peer = client.Connect(host, port, serverKey);
            IsReady = true;
        }

        public void OnApplicationExit()
            => Dispose();

        public void CommandGlobalReact(ClientConnectSuccessCommand command)
        {
            State = NetWorkSystemState.BeforeSync;
            connectionHolderComponent.serverPeer = peer;

            ClientGUID = command.Guid;
            HECSDebug.Log($"Connected successfully to: <color=orange>{serverInfo.address}:{serverInfo.port}</color>.");
            EntityManager.GetSingleComponent<ServerInfoComponent>().ServerTickMs = command.ServerData.ServerTickIntervalMilliseconds;
            interval = new WaitForSeconds(command.ServerData.ServerTickIntervalMilliseconds / 1000f);
            client.DisconnectTimeout = command.ServerData.DisconnectTimeoutMs;
        }

        public void UpdateCustom()
        {
            client.PollEvents();

            switch (State)
            {
                case NetWorkSystemState.Wait:
                    break;
                case NetWorkSystemState.Connect:
                    //ConnectingLoop();
                    break;
                case NetWorkSystemState.BeforeSync:
                    State = NetWorkSystemState.Sync;

                    var connect = new ClientConnectCommand
                    {
                        RoomWorld = connectionInfoClientComponent.RoomWorldIndex,
                    };
                    connectionHolderComponent.serverPeer = peer;
                    dataSenderSystem.SendCommandToServer(connect);
                    break;
                case NetWorkSystemState.Sync:
                    if (peer.ConnectionState == ConnectionState.Disconnected)
                    {
                        EntityManager.Command(new CloseConnectionCommand());
                        return;
                    }
                    Owner.Command(new NetworkTickCommand());
                    EntityManager.Command(new NetworkTickCommand(), -1);
                    break;
                case NetWorkSystemState.Disconnect:
                    break;
            }
        }

        public void CommandGlobalReact(SyncServerComponentsCommand command)
        {
            foreach (var dataContainer in command.Components)
            {
                var local = dataContainer;
                if (EntityManager.TryGetEntityByID(dataContainer.EntityGuid, out var entity))
                {
                    if (entity.ContainsMask(ref Replicated))
                        EntityManager.ResolversMap.ProcessResolverContainer(ref local, ref entity);
                    else
                    {
                        if (local.IsSyncSelf)
                            EntityManager.ResolversMap.ProcessResolverContainer(ref local, ref entity);
                    }
                }
            }
        }

        public async Task<bool> ConnectTo(string address, int port, int roomWorldIndex)
        {
            if (State != NetWorkSystemState.Wait)
            {
                Debug.LogError("Error connecting to the game world, the connection is already establisheds");
                return false;
            }

            connectionInfoClientComponent.RoomWorldIndex = roomWorldIndex;
            connectionInfoClientComponent.CurrentAttempts = 0;
            Debug.Log($"Подключаюсь к миру по IP:{address}:{port}");
            InitClient();
            
            connectionInfoClientComponent.ServerInfo.Address = address;
            connectionInfoClientComponent.ServerInfo.Port = port;

            State = NetWorkSystemState.Connect;


            var serverInfo = connectionInfoClientComponent.ServerInfo;
            Connect(serverInfo.Address, serverInfo.Port, serverInfo.Key);

            while (State == NetWorkSystemState.Connect)
            {
                connectionInfoClientComponent.CurrentAttempts++;
                
                if (connectionInfoClientComponent.CurrentAttempts > connectionInfoClientComponent.MaxAttemtps)
                {
                    State = NetWorkSystemState.FailToConnect;
                    break;
                }

                await Task.Delay(10);
            }

            Debug.Log($"attempts: {connectionInfoClientComponent.CurrentAttempts}  state:{peer.ConnectionState} systemState: {State}");
            return State != NetWorkSystemState.FailToConnect;
    
        }

        public void UpdateLocal()
        {
            //=> dataProcessor.UpdateLocal();
        }

        public void LateStart()
        {
        }
    }
}

namespace Systems
{
    public interface INetworkSystem : ISystem
    {
        bool IsReady { get; }
        NetWorkSystemState State { get; }
    }
}