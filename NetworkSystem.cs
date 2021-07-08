using Commands;
using Components;
using HECSFramework.Core;
using HECSFramework.Network;
using HECSFramework.Unity;
using LiteNetLib;
using MessagePack;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    public class NetworkSystem : BaseSystem, INetworkSystem, ICustomUpdatable, ILateStart,
        IReactGlobalCommand<ConnectToServerCommand>,
        IReactGlobalCommand<ClientConnectSuccessCommand>,
        IReactGlobalCommand<SyncServerComponentsCommand>
    {
        public enum NetWorkSystemState { Wait, Connect, BeforeSync, Sync, Disconnect }

        private NetManager client;
        private NetPeer peer;
        private (string address, int port, string key) serverInfo;

        public NetWorkSystemState State { get; private set; } = NetWorkSystemState.Wait;
        public YieldInstruction Interval => interval;
        private YieldInstruction interval = new WaitForSeconds(0.02f);
        private IDataProcessor dataProcessor = new HECSDataProcessor();

        public bool IsReady { get; private set; }

        private float nextConnectTime;
        private float intervalConnectTime = 5;

        [Required] private INetworkSyncEntitiesSystem syncSystem;
        [Required] private INetworkSpawnEntitiesSystem spawnSystem;
        [Required] private IDataSenderSystem dataSenderSystem;
        [Required] private ConnectionsHolderComponent connectionHolderComponent;

        private HECSMask Replicated = HMasks.GetMask<ReplicatedNetworkEntityComponent>();

        public override void InitSystem()
        {
            Owner.TryGetSystem(out syncSystem);
            Owner.TryGetSystem(out spawnSystem);
            Owner.TryGetSystem(out dataSenderSystem);
            connectionHolderComponent = Owner.GetHECSComponent<ConnectionsHolderComponent>();
            client = new NetManager(connectionHolderComponent.Listener) { DisconnectTimeout = 20000 };
        }

        private void ListenerOnPeerConnectedEvent(NetPeer peer)
        {
            State = NetWorkSystemState.BeforeSync;
        }

        private void ListenerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader netPacketReader, DeliveryMethod deliveryMethod)
        {
            var bytes = netPacketReader.GetRemainingBytes();
            var message = MessagePackSerializer.Deserialize<ResolverDataContainer>(bytes);
            dataProcessor.Process(message);
        }

        public void Start(int localPort)
        {
            client.Start(localPort);
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
            => Debug.Log($"Disconnected. Reason: {disconnectinfo.Reason.ToString()}");

        public bool Equals(ISystem other)
            => other is INetworkSystem;


        public override void Dispose()
        {
            Stop();
            client.PollEvents();
            base.Dispose();
        }

        public void CommandGlobalReact(ConnectToServerCommand command)
        {
        }

        private void ConnectingLoop()
        {
            if (nextConnectTime > Time.time)
                return;

            nextConnectTime = Time.time + intervalConnectTime;

            Connect(serverInfo.address, serverInfo.port, serverInfo.key);

            if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                State = NetWorkSystemState.BeforeSync;
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
            Debug.Log("Connected successfully.");
            interval = new WaitForSeconds(command.ServerTickIntervalMilliseconds / 1000f);
        }

        public void UpdateCustom()
        {
            client.PollEvents();

            switch (State)
            {
                case NetWorkSystemState.Wait:
                    break;
                case NetWorkSystemState.Connect:
                    ConnectingLoop();
                    break;
                case NetWorkSystemState.BeforeSync:
                    State = NetWorkSystemState.Sync;

                    var neededMask = HMasks.GetMask<NetworkClientTagComponent>();
                    EntityManager.TryGetEntityByComponents(out var netID, ref neededMask);

                    var connect = new ClientConnectCommand
                    {
                        Client = netID.GUID,
                    };

                    dataSenderSystem.SendCommand(peer, Guid.Empty, connect);
                    break;
                case NetWorkSystemState.Sync:
                    if (peer.ConnectionState == ConnectionState.Disconnected)
                    {
                        EntityManager.Command(new CloseConnectionCommand());
                        return;
                    }
                    syncSystem.SyncNetworkComponents();
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

        public void LateStart()
        {
            var connectionsToServer = Owner.GetHECSComponent<ServerConnectionsComponent>();
            var neededConnection = connectionsToServer.ServerConnectionBluePrints.First(x => x.IsActive);

            Start(neededConnection.LocalPort);
            serverInfo = (neededConnection.Address, Convert.ToInt32(neededConnection.Port), neededConnection.ServerKey);
            State = NetWorkSystemState.Connect;
        }
    }
}

namespace Systems
{
    public interface INetworkSystem : ISystem
    {
        bool IsReady { get; }
        NetworkSystem.NetWorkSystemState State { get; }
    }
}