using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;

namespace BlueprintReferenceViewer.Components
{
    public class UActorComponent
    {
        public List<PropertyData> Properties { get; private set; }
        public string ClassName { get; private set; }

        private NormalExport ComponentExport;

        public UActorComponent(NormalExport ComponentExport)
        {
            /** ClassName */
            Import ClassImport = ComponentExport.ClassIndex.ToImport(ComponentExport.Asset);
            ClassName = ClassImport.ObjectName.ToString();

            /** Properties */
            Properties = ComponentExport.Data;

            this.ComponentExport = ComponentExport;
        }

        public bool HasAnyTags(string[] Tags)
        {
            ArrayPropertyData? ComponentTags = FindPropertyByName<ArrayPropertyData>("ComponentTags");
            if (ComponentTags is null)
                return false;

            foreach (PropertyData data in ComponentTags.Value)
            {
                NamePropertyData ComponentTag = (NamePropertyData)data;

                if (Tags.Contains(ComponentTag.Value.ToString()))
                    return true;
            }

            return false;
        }

        public T? FindPropertyByName<T>(string PropertyName) where T : PropertyData
        {
            foreach (PropertyData Property in Properties)
                if (Property.Name.ToString() == PropertyName)
                    return (T)Property;

            return null;
        }

        public T FindPropertyByNameChecked<T>(string PropertyName) where T : PropertyData
        {
            foreach (PropertyData Property in Properties)
                if (Property.Name.ToString() == PropertyName)
                    return (T)Property;

            throw new Exception($"Failed to find property {PropertyName} in component {ComponentExport.ObjectName.ToString()}");
        }

        public NormalExport GetComponentExport()
        {
            return ComponentExport;
        }
    }
}
