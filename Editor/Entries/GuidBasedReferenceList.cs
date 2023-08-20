using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.AssetPalette
{
    /// <summary>
    /// There's two reasons why you should be using this class instead of having direct Object[] references:
    /// 
    /// - This way the actual loading of the assets is delayed as much as possible. The palette will not load any of
    /// the references until it's necessary, for example for generating a preview or actually using a palette entry.
    /// 
    /// - This way the Personal Palette can correctly serialize and deserialize the asset reference. The Personal
    /// Palette is not stored in an asset but is stored in the EditorPrefs as JSON instead. By default Unity's
    /// JSON utility does not seem to deserialize asset references correctly and they will become corrupt.
    /// </summary>
    [Serializable]
    public sealed class GuidBasedReferenceList<T> : IList<T> 
        where T : Object
    {
        [SerializeField] private List<string> guids;

        [NonSerialized] private bool didCacheList;
        [NonSerialized] private List<T> cachedList;
        public List<T> List
        {
            get
            {
                if (!didCacheList)
                {
#if UNITY_EDITOR
                    cachedList = new List<T>();
                    for (int i = 0; i < guids.Count; i++)
                    {
                        cachedList.Add(GetAsset(guids[i]));
                    }
                    
                    didCacheList = cachedList != null;
#endif
                }

                return cachedList;
            }
        }
        
        [NonSerialized] private T[] cachedArray;
        [NonSerialized] private bool didCacheArray;
        public T[] Array
        {
            get
            {
                if (!didCacheArray)
                {
                    didCacheArray = true;
                    cachedArray = List.ToArray();
                }
                return cachedArray;
            }
        }

        public GuidBasedReferenceList(T[] directReferences)
        {
#if UNITY_EDITOR
            guids = new List<string>();
            for (int i = 0; i < directReferences.Length; i++)
            {
                guids.Add(GetGuid(directReferences[i]));
            }
#endif
            cachedList = new List<T>(directReferences);
            didCacheList = true;
        }

        public GuidBasedReferenceList(List<T> directReferences)
            : this(directReferences.ToArray())
        {
        }
        
        private T GetAsset(string guid)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
#else
            return null;
#endif
        }

        private string GetGuid(T directReference)
        {
#if UNITY_EDITOR
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(directReference));
#else
            return null;
#endif
        }

        private void ClearCache()
        {
            cachedList = null;
            didCacheList = false;
            cachedArray = null;
            didCacheArray = false;
        }

        public IEnumerator<T> GetEnumerator() => List.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(T item)
        {
#if UNITY_EDITOR
            guids.Add(GetGuid(item));
            ClearCache();
#endif // UNITY_EDITOR
        }

        public void Clear()
        {
            guids.Clear();
            ClearCache();
        }

        public bool Contains(T item) => List.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => List.CopyTo(array, arrayIndex);

        public bool Remove(T item)
        {
            ClearCache();
            return guids.Remove(GetGuid(item));
        }

        public int Count => List.Count;

        bool ICollection<T>.IsReadOnly => ((IList<T>)List).IsReadOnly;

        public int IndexOf(T item) => List.IndexOf(item);

        public void Insert(int index, T item)
        {
            ClearCache();
            guids.Insert(index, GetGuid(item));
        }

        public void RemoveAt(int index)
        {
            ClearCache();
            guids.RemoveAt(index);
        }

        public T this[int index]
        {
            get => List[index];
            set
            {
                ClearCache();
                guids[index] = GetGuid(value);
            }
        }
    }
}
