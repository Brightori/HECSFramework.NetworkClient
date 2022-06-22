using HECSFramework.Core;
using HECSFramework.Unity;
using System;

namespace Components
{
    [Serializable, BluePrint]
    public partial class ServerInfoComponent : BaseComponent
    {
        public int ServerTickMs;
    }
}