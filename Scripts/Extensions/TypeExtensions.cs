using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static class TypeExtensions
    {
        public static List<FieldInfo> GetFieldsUpUntilBaseClass<BaseClass>(
            this Type type, bool includeBaseClass = true)
        {
            List<FieldInfo> fields = new List<FieldInfo>();
            while (typeof(BaseClass).IsAssignableFrom(type))
            {
                if (type == typeof(BaseClass) && !includeBaseClass)
                    break;

                fields.AddRange(type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));

                type = type.BaseType;
                
                if (type == typeof(BaseClass) && includeBaseClass)
                    break;
            }
            return fields;
        }

        public static List<FieldInfo> GetFieldsUpUntilBaseClass<BaseClass, FieldType>(
            this Type type, bool includeBaseClass = true)
        {
            List<FieldInfo> fields = GetFieldsUpUntilBaseClass<BaseClass>(type, includeBaseClass);

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (!typeof(FieldType).IsAssignableFrom(fields[i].FieldType))
                    fields.RemoveAt(i);
            }
            return fields;
        }

        private static FieldInfo GetDeclaringFieldUpUntilBaseClass<BaseClass, FieldType>(
            this Type type, object instance, FieldType value, bool includeBaseClass = true)
        {
            List<FieldInfo> fields = GetFieldsUpUntilBaseClass<BaseClass, FieldType>(
                type, includeBaseClass);

            FieldType fieldValue;
            for (int i = 0; i < fields.Count; i++)
            {
                fieldValue = (FieldType)fields[i].GetValue(instance);
                if (Equals(fieldValue, value))
                    return fields[i];
            }

            return null;
        }

        public static string GetNameOfDeclaringField<BaseClass, FieldType>(
            this Type type, object instance, FieldType value, bool capitalize = false)
        {
            FieldInfo declaringField = type
                .GetDeclaringFieldUpUntilBaseClass<BaseClass, FieldType>(instance, value);

            if (declaringField == null)
                return null;

            return GetFieldName(type, declaringField, capitalize);
        }

        private static string GetFieldName(this Type type, FieldInfo fieldInfo, bool capitalize = false)
        {
            string name = fieldInfo.Name;

            if (!capitalize)
                return name;

            if (name.Length <= 1)
                return name.ToUpper();

            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static Type[] GetAllAssignableClasses(
            this Type type, bool includeAbstract = true, bool includeItself = false)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => type.IsAssignableFrom(t) && (t != type || includeItself) && (includeAbstract || !t.IsAbstract))
                .ToArray();
        }
    }
}
