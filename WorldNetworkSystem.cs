using Commands;
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
        ICustomUpdatable, ILateStart, IUpdatable,
        IReactGlobalCommand<ClientConnectSuccessCommand>,
        IReactGlobalCommand<SyncServerComponentsCommand>, IOnApplicationQuit
    {

        [Required] private DataSenderSystem dataSenderSystem;
        [Required] private NetworkClientHolderComponent networkClient;
        [Required] private ConnectionInfoClientComponent connectionInfoClientComponent;

        private HECSMask Replicated = HMasks.GetMask<ReplicatedNetworkEntityComponent>();



        
       // public NetWorkSystemState State { get; private set; } 
        
        public YieldInstruction Interval => interval;
        private YieldInstruction interval = new WaitForSeconds(0.02f);
        
        private IDataProcessor dataProcessor = new HECSDataProcessor();

        public bool IsReady { get; private set; }
        public Guid ClientGUID { get; private set; }

        public override void InitSystem()
        {
            Owner.TryGetSystem(out dataSenderSystem);
            networkClient = Owner.GetHECSComponent<NetworkClientHolderComponent>();
        }

        private void ListenerOnPeerConnectedEvent(NetPeer peer)
        {
         
        }

        private void ListenerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader netPacketReader, DeliveryMethod deliveryMethod)
        {
            var bytes = netPacketReader.GetRemainingBytes();

            if (bytes.Length == 0)
                return;

            var message = MessagePackSerializer.Deserialize<ResolverDataContainer>(bytes);
            dataProcessor.Process(message, Owner.World);
        }

        public void RegisterListener()
        {
            networkClient.Listener.NetworkReceiveEvent += ListenerOnNetworkReceiveEvent;
            networkClient.Listener.PeerDisconnectedEvent += ListenerOnPeerDisconnectedEvent;
            networkClient.Listener.PeerConnectedEvent += ListenerOnPeerConnectedEvent;
            networkClient.Listener.NetworkErrorEvent += ListenerOnNetworkErrorEvent;
        }

        public void UnrigisterListener()
        {
            networkClient.Listener.NetworkReceiveEvent -= ListenerOnNetworkReceiveEvent;
            networkClient.Listener.PeerDisconnectedEvent -= ListenerOnPeerDisconnectedEvent;
            networkClient.Listener.PeerConnectedEvent -= ListenerOnPeerConnectedEvent;
            networkClient.Listener.NetworkErrorEvent -= ListenerOnNetworkErrorEvent;
        }

        private void ListenerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
            => Debug.LogError(socketerror.ToString());

        private void ListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
        {

                EntityManager.Command(new CloseConnectionCommand());

            Debug.Log($"Disconnected. Reason: {disconnectinfo.Reason.ToString()}");
            EntityManager.GlobalCommand(new DisconnectedClientCommand { Reason = disconnectinfo.Reason });
        }


        public override void Dispose()
        {
            UnrigisterListener();
            networkClient.PollEvents();
            networkClient.Disconnect();
            base.Dispose();
        }

        private void Connect(string host, int port, string serverKey, int maxAttemtps)
        {
            networkClient.Connect(host, port, serverKey, maxAttemtps);
            IsReady = true;
        }

        public void OnApplicationExit()
            => Dispose();

        public void CommandGlobalReact(ClientConnectSuccessCommand command)
        {
           

            Debug.Log($"ClientConnectSuccess:{command.Guid}");
            ClientGUID = command.Guid;
          //  HECSDebug.Log($"Connected successfully to: <color=orange>{serverInfo.address}:{serverInfo.port}</color>.");
            EntityManager.GetSingleComponent<ServerInfoComponent>().ServerTickMs = command.ServerData.ServerTickIntervalMilliseconds;
            interval = new WaitForSeconds(command.ServerData.ServerTickIntervalMilliseconds / 1000f);
            networkClient.Sync(command.ServerData.DisconnectTimeoutMs);
        }

        public void UpdateCustom()
        {
            networkClient.PollEvents();
            if (networkClient.State == NetWorkSystemState.Sync)
            {
                Owner.Command(new NetworkTickCommand());
                EntityManager.Command(new NetworkTickCommand(), -1);
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
            if (networkClient.State != NetWorkSystemState.Wait)
            {
                Debug.LogError("Error connecting to the game world, the connection is already establisheds");
                return false;
            }

            connectionInfoClientComponent.RoomWorldIndex = roomWorldIndex;
          
          
            RegisterListener();
            



            Debug.Log($"Подключаюсь к миру по IP:{address}:{port}");
            Connect(address, port, connectionInfoClientComponent.Key, 20);

            connectionInfoClientComponent.CurrentAttempts = 0;
            while (networkClient.State == NetWorkSystemState.Connect)
            {
                await Task.Delay(10);
            }

            if(networkClient.State == NetWorkSystemState.BeforeSync)
            {
                dataSenderSystem.SendCommandToServer(new ClientConnectCommand
                {
                    RoomWorld = connectionInfoClientComponent.RoomWorldIndex,
                });
            }

            connectionInfoClientComponent.CurrentAttempts = 0;
            while (networkClient.State == NetWorkSystemState.BeforeSync)
            {
                connectionInfoClientComponent.CurrentAttempts++;

                if (connectionInfoClientComponent.CurrentAttempts > connectionInfoClientComponent.MaxAttemtps)
                {
                    networkClient.Disconnect();
                    break;
                }

                await Task.Delay(10);
            }

            return networkClient.State == NetWorkSystemState.Sync;
            
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
