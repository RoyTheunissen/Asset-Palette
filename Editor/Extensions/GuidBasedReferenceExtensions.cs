using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    public static class GuidBasedReferenceExtensions
    {
        /// <summary>
        /// Initializes a Guid based reference from a direct reference, for backwards compatibility.
        /// </summary>
        public static void InitializeFromExistingDirectReference<T>(this GuidBasedReference<T> src,
            ref GuidBasedReference<T> guidBasedReference, ref T directReference)
            where T : Object
        {
            if (guidBasedReference == null || !guidBasedReference.HasGuid && directReference != null)
            {
                guidBasedReference = new GuidBasedReference<T>(directReference);
                directReference = null;
            }
        }
        
        /// <summary>
        /// Initializes a Guid based reference list from a direct reference array, for backwards compatibility.
        /// </summary>
        public static void InitializeFromExistingDirectReferences<T>(this GuidBasedReferenceList<T> src,
            ref GuidBasedReferenceList<T> guidBasedReferences, ref T[] directReferences)
            where T : Object
        {
            if (guidBasedReferences == null || guidBasedReferences.Count == 0 && directReferences.Length > 0)
            {
                guidBasedReferences = new GuidBasedReferenceList<T>(directReferences);
                directReferences = null;
            }
        }
        
        /// <summary>
        /// Initializes a Guid based reference list from a direct reference array, for backwards compatibility.
        /// </summary>
        public static void InitializeFromExistingDirectReferences<T>(this GuidBasedReferenceList<T> src,
            ref GuidBasedReferenceList<T> guidBasedReferences, ref List<T> directReferences)
            where T : Object
        {
            if (guidBasedReferences == null || guidBasedReferences.Count == 0 && directReferences.Count > 0)
            {
                guidBasedReferences = new GuidBasedReferenceList<T>(directReferences.ToArray());
                directReferences = null;
            }
        }
    }
}
