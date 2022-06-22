using HECSFramework.Core;
using LiteNetLib;

namespace Commands
{
    public struct DisconnectedClientCommand : IGlobalCommand
    {
        public DisconnectReason Reason { get; set; }
    }
}