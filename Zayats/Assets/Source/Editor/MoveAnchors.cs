// https://gist.github.com/achimmihca/4f053a81983c91bdf661214e1b88f65b
using UnityEditor;
using UnityEngine;
using Common.Editor;
using System.Collections.Generic;

namespace Editor
{
    public static class AnchorsToCornersMenuItems
    {
        // Hotkey: Alt+A
        [MenuItem("Tools/Anchors to Corners (RectTransform)/Width and Height &a")]
        public static void MoveAnchorsToCorners()
        {
            GetSelectedComponents<RectTransform>().ForEach(it =>
            {
                Undo.RecordObject(it, "MoveAnchorsToCorners");
                MoveAnchorsToCornersExtensions.MoveAnchorsToCorners(it);
            });
        }

        [MenuItem("Tools/Anchors to Corners (RectTransform)/Width")]
        public static void MoveAnchorsToCorners_Width()
        {
            GetSelectedComponents<RectTransform>().ForEach(it =>
            {
                Undo.RecordObject(it, "MoveAnchorsToCorners_Width");
                MoveAnchorsToCornersExtensions.MoveAnchorsToCorners_Width(it);
            });
        }

        [MenuItem("Tools/Anchors to Corners (RectTransform)/Height")]
        public static void MoveAnchorsToCorners_Height()
        {
            GetSelectedComponents<RectTransform>().ForEach(it =>
            {
                Undo.RecordObject(it, "MoveAnchorsToCorners_Height");
                MoveAnchorsToCornersExtensions.MoveAnchorsToCorners_Height(it);
            });
        }

        private static List<T> GetSelectedComponents<T>()
        {
            List<T> result = new List<T>();

            GameObject[] activeGameObjects = Selection.gameObjects;
            if (activeGameObjects == null || activeGameObjects.Length == 0)
                return result;

            foreach (GameObject gameObject in activeGameObjects)
            {
                T component = gameObject.GetComponent<T>();
                if (component != null)
                    result.Add(component);
            }
            return result;
        }
    }
}