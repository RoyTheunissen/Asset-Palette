using System;
using System.Linq;
using System.Reflection;

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class MemberInfoExtensions
    {
        public static T GetAttribute<T>(this MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            object[] attributes = memberInfo.GetCustomAttributes(inherit);
            return attributes.OfType<T>().FirstOrDefault();
        }

        public static bool HasAttribute<T>(this MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            object[] attributes = memberInfo.GetCustomAttributes(inherit);
            return attributes.OfType<T>().Any();
        }
    }
}
