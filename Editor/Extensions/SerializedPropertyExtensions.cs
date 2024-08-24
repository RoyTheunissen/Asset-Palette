using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RoyTheunissen.AssetPalette.Windows;
using UnityEditor;

namespace RoyTheunissen.AssetPalette.Extensions
{
    /// <summary>
    /// Utilities for things like quickly getting a custom value of a serialized property.
    /// </summary>
    public static partial class SerializedPropertyExtensions
    {
        public const string GuidPathSeparator = "/";
        
        /// <summary>
        /// Courtesy of douduck08. Cheers.
        /// https://gist.github.com/douduck08/6d3e323b538a741466de00c30aa4b61f
        /// </summary>
        public static T GetValue<T>(this SerializedProperty property) where T : class
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data", "");
            string[] fieldStructure = path.Split('.');
            for (int i = 0; i < fieldStructure.Length; i++)
            {
                if (fieldStructure[i].Contains("["))
                {
                    ParseFieldStructure(fieldStructure[i], out string fieldName, out int index);
                    obj = GetFieldValueWithIndex(fieldName, obj, index);
                }
                else
                {
                    obj = GetFieldValue(fieldStructure[i], obj);
                }
            }

            return (T)obj;
        }

        private static void ParseFieldStructure(string input, out string fieldName, out int index)
        {
            int startIndex = input.IndexOf('[');
            int endIndex = input.IndexOf(']');
            index = int.Parse(input.Substring(startIndex + 1, endIndex - startIndex - 1));
            fieldName = input.Substring(0, startIndex) + input.Substring(endIndex + 1);
        }

        public static bool SetValue<T>(this SerializedProperty property, T value) where T : class
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data", "");
            string[] fieldStructure = path.Split('.');
            Regex rgx = new Regex(@"\[\d+\]");
            for (int i = 0; i < fieldStructure.Length - 1; i++)
            {
                if (fieldStructure[i].Contains("["))
                {
                    int index = System.Convert.ToInt32(new string(fieldStructure[i].Where(char.IsDigit).ToArray()));
                    obj = GetFieldValueWithIndex(rgx.Replace(fieldStructure[i], ""), obj, index);
                }
                else
                {
                    obj = GetFieldValue(fieldStructure[i], obj);
                }
            }

            string fieldName = fieldStructure.Last();
            if (fieldName.Contains("["))
            {
                int index = System.Convert.ToInt32(new string(fieldName.Where(char.IsDigit).ToArray()));
                return SetFieldValueWithIndex(rgx.Replace(fieldName, ""), obj, index, value);
            }

            return SetFieldValue(fieldName, obj, value);
        }

        private static object GetFieldValue(
            string fieldName, object obj,
            BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
                return field.GetValue(obj);

            return default(object);
        }

        private static object GetFieldValueWithIndex(
            string fieldName, object obj, int index,
            BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
        {
            if (index < 0)
                return null;
            
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                object list = field.GetValue(obj);
                if (list.GetType().IsArray)
                {
                    object[] array = (object[])list;
                    if (index >= array.Length)
                        return null;
                    return array[index];
                }
                if (list is IEnumerable)
                {
                    IList iList = (IList)list;
                    if (index >= iList.Count)
                        return null;
                    return iList[index];
                }
            }

            return null;
        }

        public static bool SetFieldValue(
            string fieldName, object obj, object value, bool includeAllBases = false,
            BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                field.SetValue(obj, value);
                return true;
            }

            return false;
        }

        public static bool SetFieldValueWithIndex(
            string fieldName, object obj, int index, object value, bool includeAllBases = false,
            BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                object list = field.GetValue(obj);
                if (list.GetType().IsArray)
                {
                    ((object[])list)[index] = value;
                    return true;
                }

                if (value is IEnumerable)
                {
                    ((IList)list)[index] = value;
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// From: https://gist.github.com/monry/9de7009689cbc5050c652bcaaaa11daa
        /// </summary>
        public static SerializedProperty GetParent(this SerializedProperty serializedProperty)
        {
            string[] propertyPaths = serializedProperty.propertyPath.Split('.');
            if (propertyPaths.Length <= 1)
            {
                return default(SerializedProperty);
            }

            SerializedProperty parentSerializedProperty =
                serializedProperty.serializedObject.FindProperty(propertyPaths.First());
            for (int index = 1; index < propertyPaths.Length - 1; index++)
            {
                if (propertyPaths[index] == "Array")
                {
                    if (index + 1 == propertyPaths.Length - 1)
                    {
                        // reached the end
                        break;
                    }
                    if (propertyPaths.Length > index + 1 && Regex.IsMatch(propertyPaths[index + 1], "^data\\[\\d+\\]$"))
                    {
                        Match match = Regex.Match(propertyPaths[index + 1], "^data\\[(\\d+)\\]$");
                        int arrayIndex = int.Parse(match.Groups[1].Value);
                        parentSerializedProperty = parentSerializedProperty.GetArrayElementAtIndex(arrayIndex);
                        index++;
                    }
                }
                else
                {
                    parentSerializedProperty = parentSerializedProperty.FindPropertyRelative(propertyPaths[index]);
                }
            }

            return parentSerializedProperty;
        }
        
        public static bool PathEquals(
            this SerializedProperty a, SerializedProperty b)
        {
            string pathA = a?.propertyPath;
            string pathB = b?.propertyPath;
            return string.Equals(pathA, pathB, StringComparison.Ordinal);
        }
        
        public static int GetIndexOfArrayElement(
            this SerializedProperty serializedProperty, SerializedProperty element)
        {
            for (int i = 0; i < serializedProperty.arraySize; i++)
            {
                if (serializedProperty.GetArrayElementAtIndex(i).PathEquals(element))
                    return i;
            }
            return -1;
        }

        public static SerializedProperty AddArrayElement(this SerializedProperty serializedProperty)
        {
            serializedProperty.InsertArrayElementAtIndex(serializedProperty.arraySize);
            return serializedProperty.GetArrayElementAtIndex(serializedProperty.arraySize - 1);
        }
        
        public static void DeleteArrayElement(this SerializedProperty serializedProperty, SerializedProperty element)
        {
            for (int i = 0; i < serializedProperty.arraySize; i++)
            {
                if (serializedProperty.GetArrayElementAtIndex(i).PathEquals(element))
                {
                    serializedProperty.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }
        
        /// <summary>
        /// It's possible to get an error called "SerializedProperty folders.Array.data[X] has disappeared!" sometimes.
        /// I think it's to do with undo/redo causing an array element to disappear but you still have a reference to
        /// it. If you then use the property at all, for instance by calling .propertyPath, you will get an error.
        /// Now, the system recovers from this quite gracefully. But I want to be able to detect this issue WITHOUT
        /// triggering the error because if it's not causing a problem then it shouldn't alarm users. So we have to
        /// somehow detect whether the specified property exists in its parent array without calling propertyPath
        /// or GetParent() on it or anything like that. CONVOLUTED
        /// </summary>
        public static bool ExistsInParentArray(this SerializedProperty serializedProperty,
            SerializedProperty parentArray = null, string propertyPath = null)
        {
            if (parentArray == null)
                parentArray = serializedProperty.GetParent();
            
            if (parentArray == null)
                return false;

            if (string.IsNullOrEmpty(propertyPath))
                propertyPath = serializedProperty.propertyPath;
            
            for (int i = 0; i < parentArray.arraySize; i++)
            {
                SerializedProperty element = parentArray.GetArrayElementAtIndex(i);
                if (element == null)
                    continue;
                
                // Make it possible to use a specified path, because this function exists mostly to try and prevent an
                // error about a property going missing *without* triggering an error, and calling propertyPath on the
                // serialized property actually triggers the error already.
                if (string.Equals(propertyPath, element.propertyPath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
        
        public static string GetIdForPath(this SerializedProperty serializedProperty)
        {
            if (serializedProperty == null)
                return null;

            if (serializedProperty.propertyType == SerializedPropertyType.Integer)
                return serializedProperty.intValue.ToString();
            
            if (serializedProperty.propertyType == SerializedPropertyType.String)
                return serializedProperty.stringValue;
            
            throw new NotImplementedException($"Tried to use property '{serializedProperty.propertyPath}' for an " +
                    $"ID path but we don't yet support properties of type '{serializedProperty.propertyType}' " +
                    $"being used as an ID. Try integer or string instead or add support " +
                    $"for {serializedProperty.propertyType}.");
        }

        /// <summary>
        /// The regular propertyPath of a SerializedProperty hierarchy has the indices of arrays baked into it.
        /// For example: folders.Array.data[9].children.Array.data[1]. When you're dealing with moving properties
        /// from one parent to another across a complex hierarchy, working with paths like this is problematic because
        /// the indices change in complex ways. For that purpose it is more convenient to work with order-independent
        /// paths. For example, just using a unique integer instead of names. Then you end up with a path
        /// like "1/5" and you can heuristically figure out which serialized
        /// property that represents. See: SerializedObjectExtensions.FindPropertyFromIdPath
        /// </summary>
        public static string GetIdPath(
            this SerializedProperty serializedProperty, string idPropertyName, string childrenPropertyName)
        {
            if (serializedProperty == null)
                return null;
            
            return serializedProperty.GetIdPathRecursive(idPropertyName, childrenPropertyName);
        }
        
        private static string GetIdPathRecursive(
            this SerializedProperty serializedProperty, string idPropertyName, string childrenPropertyName)
        {
            SerializedProperty parentProperty = serializedProperty.GetParent();
            
            string guid = serializedProperty.FindPropertyRelative(idPropertyName).GetIdForPath();
            
            if (string.Equals(parentProperty.name, childrenPropertyName, StringComparison.Ordinal))
            {
                return parentProperty.GetParent()
                           .GetIdPathRecursive(idPropertyName, childrenPropertyName) + GuidPathSeparator + guid;
            }
            
            return guid;
        }
    }
}
