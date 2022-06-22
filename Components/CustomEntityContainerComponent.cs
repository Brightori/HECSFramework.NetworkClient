using HECSFramework.Core;
using Helpers;
using Sirenix.OdinInspector;

namespace Components
{
    public partial class CustomEntityContainerComponent : IBeforeSerializationComponent, IAfterSerializationComponent
    {
        [DrawWithUnity]
        public EntityContainerReference CustomContainer;

        public void AfterSync()
        {
            CustomContainer = new EntityContainerReference(ContainerGUID);
        }

        public void BeforeSync()
        {
            this.ContainerGUID = CustomContainer.AssetGUID;
        }
    }
}
