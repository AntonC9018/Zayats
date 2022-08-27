#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityObject = UnityEngine.Object;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using System.IO;

namespace Common.Editor
{
    public static class AssetDatabaseHelper
    {
        public static IEnumerable<T> FindObjectsOfType<T>() where T : UnityObject
        {
            return FindObjectsOfType(typeof(T)).Select(a => a as T);
        }

        public static IEnumerable<UnityObject> FindObjectsOfType(System.Type objectType)
        {
            return AssetDatabase.FindAssets("t:" + objectType.Name, searchInFolders: null)
                .Select(a => AssetDatabase.GUIDToAssetPath(a))
                .SelectMany(a => AssetDatabase.LoadAllAssetsAtPath(a));
        }

        public static UnityObject FindObjectOfType(System.Type objectType)
        {
            return FindObjectsOfType(objectType).FirstOrDefault();
        }

        public static T FindObjectOfType<T>() where T : UnityObject
        {
            return (T) FindObjectOfType(typeof(T));
        }

        private const string defaultFolder = "";

        public static UnityObject FindOrCreateObjectOfType(System.Type objectType, string folderWhereToSaveRelativeToAssets = defaultFolder)
        {
            {
                UnityObject foundObject = FindObjectOfType(objectType);
                if (foundObject != null)
                    return foundObject;
            }

            var createdObject = CreateObjectWithDefaults(objectType, folderWhereToSaveRelativeToAssets);
            SelectObject(createdObject);

            return createdObject;
        }

        public static string GetDefaultFileName(System.Type objectType)
        {
            // [CreateAssetMenu], has the default file name.
            var createAssetMenuAttribute = objectType.GetCustomAttribute<CreateAssetMenuAttribute>();
            if (createAssetMenuAttribute is not null)
                return createAssetMenuAttribute.fileName;
            
            // Otherwise just do the default
            return objectType.Name;
        }

        // TODO: better implementation.
        public static T CreateObjectInteractive<T>()
            where T : ScriptableObject
        {
            string filePath = EditorUtility.SaveFilePanel(
                title: "Create a " + typeof(T).Name,
                defaultFolder,
                defaultName: typeof(T).Name,
                extension: ".asset");
            UnityObject createdObject = ObjectFactory.CreateInstance(typeof(T));
            AssetDatabase.CreateAsset(createdObject, filePath);
            SelectObject(createdObject);
            return (T) createdObject;
        }

        public static T CreateObjectWithDefaults<T>(
            string folderWhereToSaveRelativeToAssets = defaultFolder,
            string fileNameWithoutExtension = null)
            where T : UnityObject
        {
            return (T) CreateObjectWithDefaults(typeof(T), folderWhereToSaveRelativeToAssets, fileNameWithoutExtension);
        }

        public struct AssetFilePath
        {
            public string FullPathWithoutExtension;
            public string Extension;
            public readonly string AssetPath => FullPathWithoutExtension + Extension; 
        }

        public static AssetFilePath GetAssetPath(
            System.Type objectType,
            string folderWhereToSaveRelativeToAssets,
            string fileNameWithoutExtension)
        {
            bool isScriptableObject = (typeof(ScriptableObject)).IsAssignableFrom(objectType);
            
            // TODO: find a generic way of determining the file extension
            // https://docs.unity3d.com/2020.1/Documentation/ScriptReference/AssetDatabase.CreateAsset.html
            Assert.IsTrue(isScriptableObject, "For now, we only support scriptable objects");
            Assert.IsNotNull(folderWhereToSaveRelativeToAssets, "The folder must not be null");
            const string extension = ".asset";


            string MakeSureThereAreTrailingSlashes(string path)
            {
                if (path.Length == 0 || path[^1] != '/')
                    path = path + '/';
                
                if (path[0] != '/')
                    path = '/' + path;

                return path;
            }

            fileNameWithoutExtension ??= GetDefaultFileName(objectType);
            string fullFilePath = "Assets" + MakeSureThereAreTrailingSlashes(folderWhereToSaveRelativeToAssets) + fileNameWithoutExtension;

            
            AssetFilePath a;
            a.FullPathWithoutExtension = fullFilePath;
            a.Extension = extension;
            return a;
        }

        public static AssetFilePath AdjustAssetPathUntilUnique(AssetFilePath path)
        {
            // Try appending garbage to the file name until there are no collisions.
            int counter = 0;
            while (CheckAssetExistsAtPath(path.AssetPath))
            {
                path.FullPathWithoutExtension += ((char) counter + '0');
                counter = (counter + 1) % 10;
            }

            return path;
        }

        public static T CreateObjectOrLoadExistingWithDefaults<T>(
            string folderWhereToSaveRelativeToAssets = defaultFolder,
            string fileNameWithoutExtension = null) 
            where T : UnityObject
        {
            return (T) CreateObjectOrLoadExisting(typeof(T),
                GetAssetPath(typeof(T), folderWhereToSaveRelativeToAssets, fileNameWithoutExtension));
        }

        public static T CreateObjectOrLoadExisting<T>(AssetFilePath path) where T : UnityObject
        {
            return (T) CreateObjectOrLoadExisting(typeof(T), path);
        }

        public static UnityObject CreateObjectOrLoadExisting(System.Type objectType, AssetFilePath path)
        {
            var fullPath = path.AssetPath;
            if (CheckAssetExistsAtPath(fullPath))
            {
                // The old objects hang around in the undo system, I guess.
                // They don't exist at the path, but their ids are still valid.
                var t = AssetDatabase.LoadAssetAtPath(fullPath, objectType);
                if (t != null)
                    return t;
            }
            return CreateObject(objectType, fullPath);
        }

        public static UnityObject CreateObjectWithDefaults(
            System.Type objectType,
            string folderWhereToSaveRelativeToAssets = defaultFolder,
            string fileNameWithoutExtension = null)
        {
            var path = GetAssetPath(objectType, folderWhereToSaveRelativeToAssets, fileNameWithoutExtension);
            path = AdjustAssetPathUntilUnique(path);
            return CreateObject(objectType, path.AssetPath);
        }

        public static UnityObject CreateObject(System.Type objectType, string fullPath)
        {
            UnityObject createdObject = ObjectFactory.CreateInstance(objectType);
            AssetDatabase.CreateAsset(createdObject, fullPath);
            SelectObject(createdObject);
            Undo.RegisterCreatedObjectUndo(createdObject, "Create object " + createdObject.name);

            return createdObject;
        }

        public static T FindOrCreateObjectOfType<T>(string folderWhereToSaveRelativeToAssets = defaultFolder) where T : UnityObject
        {
            return (T) FindOrCreateObjectOfType(typeof(T), folderWhereToSaveRelativeToAssets);
        }

        public static void FindOrCreateObjectOfType_AndSelectIt<T>(string folderWhereToSaveRelativeToAssets = defaultFolder) where T : UnityObject
        {
            UnityObject a = FindOrCreateObjectOfType(typeof(T), folderWhereToSaveRelativeToAssets);
            SelectObject(a);
        }

        public static void SelectObject(UnityObject obj)
        {
            EditorGUIUtility.PingObject(obj);
            UnityEditor.Selection.activeObject = obj;
        }

        // https://forum.unity.com/threads/how-to-check-if-assetdatabase-exists.246956/
        public static bool CheckAssetExistsAtPath(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            
            // This doesn't actually work in all cases.
            // Need to check the file manually too.
            if (string.IsNullOrEmpty(guid))
                return false;
            
            return true;
        }
    }
}
#endif
