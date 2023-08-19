using System.Collections.Generic;
using RoyTheunissen.AssetPalette.Extensions;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Windows
{
    public class EntryDragData
    {
        private readonly List<SerializedProperty> entryProperties;
        public List<SerializedProperty> EntryProperties => entryProperties;

        private readonly SerializedProperty folderDraggingFromProperty;
        public SerializedProperty FolderDraggingFromProperty => folderDraggingFromProperty;

        private readonly string folderDraggingFromPath;
        public string FolderDraggingFromPath => folderDraggingFromPath;

        public EntryDragData(List<SerializedProperty> entryProperties, SerializedProperty folderDraggingFromProperty)
        {
            this.entryProperties = new List<SerializedProperty>(entryProperties);
            this.folderDraggingFromProperty = folderDraggingFromProperty;
            folderDraggingFromPath = folderDraggingFromProperty.GetIdPath("name", "children");
        }
    }
}
