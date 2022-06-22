using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Commands;
using Components;
using HECSFramework;
using HECSFramework.Core;
using HECSFramework.Network;
using HECSFramework.Unity;
using LiteNetLib;
using MessagePack;
using UnityEngine;

namespace Systems
{
    [Serializable, BluePrint]
    [RequiredAtContainer(typeof(ServerConnectionsComponent), typeof(NetworkClientTagComponent))]
    public class NetworkSystem : BaseSystem, 
        INetworkSystem, ICustomUpdatable, ILateStart, IUpdatable,
        IReactGlobalCommand<ClientConnectSuccessCommand>,
        IReactGlobalCommand<SyncServerComponentsCommand>, IOnApplicationQuit
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

        [Required] private DataSenderSystem dataSenderSystem;
        [Required] private ConnectionsHolderComponent connectionHolderComponent;

        private HECSMask Replicated = HMasks.GetMask<ReplicatedNetworkEntityComponent>();

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
        
        private void ConnectingLoop()
        {
            if (nextConnectTime > Time.time)
                return;

            nextConnectTime = Time.time + intervalConnectTime;

            Connect(serverInfo.address, serverInfo.port, serverInfo.key);

            if (peer != null && peer.ConnectionState == ConnectionState.Connected)
            {
                State = NetWorkSystemState.BeforeSync;
                connectionHolderComponent.serverPeer = peer;
            }
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
                    ConnectingLoop();
                    break;
                case NetWorkSystemState.BeforeSync:
                    State = NetWorkSystemState.Sync;

                    var netId = EntityManager.GetSingleComponent<NetworkClientTagComponent>();
                    var appVer = EntityManager.GetSingleComponent<AppVersionComponent>();

                    var connect = new ClientConnectCommand
                    {
                        Client = netId.ClientGuid,
                        Version = appVer.Version,
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

        public void LateStart()
        {
            var neededConnection = Owner.GetHECSComponent<ServerConnectionsComponent>().GetConnection();
            Start(neededConnection.LocalPort);
            serverInfo = (neededConnection.Address, Convert.ToInt32(neededConnection.Port), neededConnection.ServerKey);
            State = NetWorkSystemState.Connect;
        }

        public void UpdateLocal()
        {
            //=> dataProcessor.UpdateLocal();
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