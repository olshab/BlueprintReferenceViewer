using BlueprintReferenceViewer.Utils;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;

namespace BlueprintReferenceViewer.Components
{
    public class UChildActorComponent : UActorComponent
    {
        public string? ChildActorClass { get; private set; }

        public UChildActorComponent(NormalExport ComponentExport)
            : base(ComponentExport)
        {
            UAsset Asset = (UAsset)ComponentExport.Asset;

            /** ChildActorClass */
            ObjectPropertyData? ChildActorClassObject = FindPropertyByName<ObjectPropertyData>("ChildActorClass");
            if (ChildActorClassObject is null || !ChildActorClassObject.Value.IsImport())
                return;

            ChildActorClass = ChildActorClassObject.ToImport(Asset).GetOutermostPackage(Asset).ObjectName.ToString();
        }
    }
}
