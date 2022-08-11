using Zayats.Unity.View;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using Common.Unity;
using static Zayats.Core.Assert;
public class Stuff : MonoBehaviour
{
    public Transform[] Cells;
    public Transform[] Things;
    public UIHolderInfo[] UIHolders;
    public Transform ItemContainersTransform;
    private Transform[] Things2;
    private Transform[] ThingsUI;

    private List<(Vector3, Vector3, Color)> _markers;

    void Update()
    {
        _markers ??= new();
        _markers.Clear();

        for (int i = 0; i < UIHolders.Length; i++)
        {
            if (ItemContainersTransform.childCount <= i)
                UIHolders[i].InitializeAnimatedContainer(ItemContainersTransform, i);
            else
                UIHolders[i].AnimatedTransform = ItemContainersTransform.GetChild(i);
        }

        if (Things2 is null || Things2.Length != Things.Length)
        {
            Things2 = Things.Select(t => 
            {
                var a = Instantiate(t.gameObject).transform;
                var (_, m) = a.GetObject(ObjectHierarchy.Model);
                var mat = m.material;
                mat.color = Color.red;
                return a;
            }).ToArray();
        }

        if (ThingsUI is null || ThingsUI.Length != Things.Length)
        {            
            ThingsUI = Things.Select(t => 
            {
                var a = Instantiate(t.gameObject).transform;
                var (_, m) = a.GetObject(ObjectHierarchy.Model);
                var mat = m.material;
                mat.color = Color.blue;
                return a;
            }).ToArray();
        }
        
        int count = Math.Min(Cells.Length, Things.Length);
        count = Math.Min(count, UIHolders.Length);

        for (int i = 0; i < count; i++)
        {
            var cell = Cells[i];
            var thing = Things[i];

            if (thing.gameObject.activeInHierarchy)
            {
                var top = GetTop(cell);
                // _markers.Add((top, Vector3.one, Color.black));
                var bottom = PlaceWithBottomAt(top, cell.up, thing);
                // _markers.Add((bottom, Vector3.one, Color.white));
                Things2[i].position = bottom;
            }

            var holder = UIHolders[i];
            if (thing.gameObject.activeInHierarchy
                && false)
            {
                var itemFrame = holder.ItemFrameTransform;
                var (ccenter, csize) = itemFrame.GetWorldSpaceRect();
                var (t, model) = thing.GetObject(ObjectHierarchy.Model);
                var bounds = model.bounds;
                var s = bounds.size;

                var maxXZ = Mathf.Max(s.z, s.x);
                var maxSizes = new Vector2(maxXZ, s.y);
                var minRatio = Vector2.Scale(csize, maxSizes.Inverse()).Min();
                var offsetToCenter = bounds.center - thing.position;
                var offsetInLocalSpace = offsetToCenter * minRatio;

                // _markers.Add((ccenter, Vector3.Scale(Vector3.one * minRatio, bounds.size), Color.red));

                // Must be a child of ItemFrameTransform
                var ct = holder.CenteringTransform;
                var zoffset = - minRatio * itemFrame.forward;
                var offset = - offsetInLocalSpace;
                // ct.position = ccenter + zoffset;

                holder.AnimatedTransform.position = ccenter + zoffset; 
                ct.localPosition = offset;
                ct.localScale = Vector3.one * minRatio;
                ThingsUI[i].position = ccenter + zoffset + offset;
                ThingsUI[i].localScale = Vector3.one;
                ThingsUI[i].parent = ct;

                var (t1, model1) = ThingsUI[i].GetObject(ObjectHierarchy.Model);
                // _markers.Add((model1.bounds.center, model1.bounds.size, Color.cyan));
            }

            static Vector3 GetTop(Transform parent)
            {
                var (t, model) = parent.GetObject(ObjectHierarchy.Model);
                var worldSpaceBounds = model.bounds;
                return (worldSpaceBounds.size.y / 2) * parent.up + worldSpaceBounds.center; 
            }

            static Vector3 PlaceWithBottomAt(Vector3 position, Vector3 up, Transform parent)
            {
                var (t, model) = parent.GetObject(ObjectHierarchy.Model);
                var bounds = model.localBounds;
                var trs = t.GetLocalTRS();
                var offsetToCenter = trs.MultiplyPoint3x4(bounds.center);
                var size = Vector3.Scale(t.localScale, bounds.size);
                return position - offsetToCenter + size.y / 2 * up;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (_markers is null)
            return;
        Debug.Log(_markers.Count);
        foreach (var (pos, size, color) in _markers)
        {
            Gizmos.color = color;
            Gizmos.DrawCube(pos, size);
        }
    }
}