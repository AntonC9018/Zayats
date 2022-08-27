using UnityEngine;
using Kari.Plugins.AdvancedEnum;
using Zayats.Core;
using Zayats.Unity.View.Generated;
using System;
using Kari.Plugins.DataObject;
using UnityEngine.Serialization;

namespace Zayats.Unity.View
{
    using static Assert;

    [GenerateArrayWrapper]
    public enum Corners
    {
        TopLeft,
        BottomRight,
        BottomLeft,
    }

    public static class CornersHelper
    {
        public static (Vector3 origin, Vector3 vwidth, Vector3 vheight) GetCornersInfo(this CornersArray<Transform> corners)
        {
            var origin = corners.BottomLeft.position;
            var vheight = corners.TopLeft.position - origin;
            var vwidth = corners.BottomRight.position - origin;
            return (origin, vheight, vwidth);
        }
    }

    [Serializable]
    [DataObject]
    public partial struct SpawnMapStuff
    {
        public CornersArray<Transform> Corners;
        public Transform MapParent;
        public GameObject CellPrefab;
        public Vector2Int MaxMapSize;
        public bool TopToBottom;
    }

    [ExecuteInEditMode]
    public class SpawnMap : MonoBehaviour
    {
        public SpawnMapStuff Config;
        public CornersArray<Transform> Corners => Config.Corners;
        public Transform MapParent => Config.MapParent;
        public GameObject CellPrefab => Config.CellPrefab;

        private SpawnMapStuff? _previous;

        void Update()
        {
            if (_previous.HasValue && _previous.Value == Config)
                return;
            _previous = Config;

            for (int i = MapParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(MapParent.GetChild(i).gameObject);

            assert(MapParent != null);
            assert(CellPrefab != null);
            var parentObject = CellPrefab.transform;
            assert(parentObject.localScale == Vector3.one);
            var modelObject = parentObject.GetChild(0);
            var meshFilter = modelObject.GetComponent<MeshFilter>();
            assert(meshFilter != null);
            var mesh = meshFilter.sharedMesh;
            assert(mesh != null);
            var bounds = mesh.bounds;
            var size3 = Vector3.Scale(bounds.size, modelObject.localScale);
            var cellSize = new Vector2(size3.x, size3.z);

            var (origin, vwidth, vheight) = Corners.GetCornersInfo();

            var vx = vwidth.normalized;
            var vy = vheight.normalized;

            var width = vwidth.magnitude;
            var howManyWholeCellsInWidth = Mathf.Floor(width / cellSize.x);
            assert(howManyWholeCellsInWidth >= 0, "Make the map wider.");
            howManyWholeCellsInWidth = Mathf.Min(howManyWholeCellsInWidth, Config.MaxMapSize.x);

            var height = vheight.magnitude;
            var howManyWholeCellsInHeight = Mathf.Floor(height / cellSize.y);
            assert(howManyWholeCellsInHeight >= 0, "Make the map taller.");
            howManyWholeCellsInHeight = Mathf.Min(howManyWholeCellsInHeight, Config.MaxMapSize.y);

            var xgap = (width - howManyWholeCellsInWidth * cellSize.x) / (howManyWholeCellsInWidth + 1);
            var xstart = xgap;
            var ygap = (height - howManyWholeCellsInHeight * cellSize.y) / (howManyWholeCellsInHeight + 1);
            var ystart = ygap;

            int xcount = (int) howManyWholeCellsInWidth;
            int ycount = (int) howManyWholeCellsInHeight;

            var halfCellSize = cellSize / 2;
            for (int iy = 0; iy < ycount; iy++)
            {
                var posy = ystart + iy * (ygap + cellSize.y) + halfCellSize.y;
                var vposy = posy * vy;
                if (Config.TopToBottom)
                    vposy = -vposy + vheight;

                var row = new GameObject("row " + iy).transform;
                row.SetParent(MapParent, worldPositionStays: false);
                row.position = origin + vposy;

                for (int ix = 0; ix < xcount; ix++)
                {
                    var posx = xstart + ix * (xgap + cellSize.x) + halfCellSize.x;
                    var vposx = posx * vx;
                    var pos = origin + vposx + vposy;
                    var cell = GameObject.Instantiate(CellPrefab, pos, Quaternion.identity, row);
                    cell.name = $"cell {ix},{iy}";
                }
            }
        }
    }
}