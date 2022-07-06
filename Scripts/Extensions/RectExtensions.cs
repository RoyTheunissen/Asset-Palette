using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class RectExtensions
    {
        public static Vector2 GetPosition(this Rect rect, Vector2 localPosition)
        {
            return new Vector2(rect.xMin + rect.width * localPosition.x, rect.yMin + rect.height * localPosition.y);
        }
        
        public static Rect Expand(this Rect rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.xMin -= xMin;
            rect.xMax += xMax;
            rect.yMin -= yMin;
            rect.yMax += yMax;
            return rect;
        }
        
        public static Rect Expand(this Rect rect, float amount)
        {
            return rect.Expand(amount, amount, amount, amount);
        }
        
        public static Rect Inset(this Rect rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.xMin += xMin;
            rect.xMax -= xMax;
            rect.yMin += yMin;
            rect.yMax -= yMax;
            return rect;
        }
        
        public static Rect Inset(this Rect rect, float inset)
        {
            return rect.Inset(inset, inset, inset, inset);
        }
        
#if UNITY_EDITOR
        public static Rect GetControlFirstRect(this Rect rect)
        {
            return GetControlFirstRect(rect, EditorGUIUtility.singleLineHeight);
        }

        public static Rect GetControlFirstRect(this Rect rect, float height)
        {
            return new Rect(rect.x, rect.y, rect.width, height);
        }

        public static Rect GetControlNextRect(this Rect rect)
        {
            return GetControlNextRect(rect, EditorGUIUtility.singleLineHeight);
        }

        public static Rect GetControlNextRect(this Rect rect, float height)
        {
            return new Rect(rect.x, rect.yMax, rect.width, height);
        }
        
        public static Rect GetControlRemainderVertical(this Rect rect, Rect occupant)
        {
            return new Rect(rect.x, occupant.yMax, rect.width, rect.height - occupant.height);
        }
        
        public static Rect GetLabelRect(this Rect rect)
        {
            return new Rect(
                rect.x, rect.y, EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);
        }
        
        public static Rect GetLabelRectRemainder(this Rect rect)
        {
            return new Rect(
                rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);
        }
        
        public static Rect GetLabelRect(this Rect rect, out Rect remainder)
        {
            remainder = rect.GetLabelRectRemainder();
            return rect.GetLabelRect();
        }

        public static Rect Indent(this Rect rect)
        {
            return EditorGUI.IndentedRect(rect);
        }
        
        public static Rect Indent(this Rect rect, int amount)
        {
            int originalIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = amount;
            Rect result = EditorGUI.IndentedRect(rect);
            EditorGUI.indentLevel = originalIndentLevel;
            
            return result;
        }
#endif

        public static Rect GetSubRectFromLeft(this Rect rect, float width)
        {
            return new Rect(rect.x, rect.y, width, rect.height);
        }
        
        public static Rect GetSubRectFromLeft(this Rect rect, float width, out Rect remainder)
        {
            remainder = rect.GetSubRectFromRight(rect.width - width);
            return rect.GetSubRectFromLeft(width);
        }
        
        public static Rect GetSubRectFromRight(this Rect rect, float width)
        {
            return new Rect(rect.xMax - width, rect.y, width, rect.height);
        }
        
        public static Rect GetSubRectFromRight(this Rect rect, float width, out Rect remainder)
        {
            remainder = rect.GetSubRectFromLeft(rect.width - width);
            return rect.GetSubRectFromRight(width);
        }
        
        public static Rect GetSubRectFromTop(this Rect rect, float height)
        {
            return new Rect(rect.x, rect.y, rect.width, height);
        }
        
        public static Rect GetSubRectFromBottom(this Rect rect, float height)
        {
            return new Rect(rect.x, rect.yMax - height, rect.width, height);
        }
        
        public static Rect SubtractFromLeft(this Rect rect, float width)
        {
            return new Rect(rect.x + width, rect.y, rect.width - width, rect.height);
        }
        
        public static Rect SubtractFromRight(this Rect rect, float width)
        {
            return new Rect(rect.x, rect.y, rect.width - width, rect.height);
        }
        
        public static Rect InverseTransform(this Rect rect, Rect child)
        {
            return new Rect(child.x - rect.x, child.y - rect.y, child.width, child.height);
        }
        
        public static Rect Transform(this Rect rect, Rect child)
        {
            return new Rect(child.x + rect.x, child.y + rect.y, child.width, child.height);
        }
    }
}
