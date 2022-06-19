using System;
using JCMG.Curves;
using UnityEngine;

namespace Zayats.Experiments.Curves
{
    public class CurveLogic : MonoBehaviour
    {
        [Serializable]
        public struct Data
        {
            public Bezier3DSpline curve;
            public Camera camera;
        }
        [SerializeField] private Data _curveData;

        void Start()
        {
            var camera = _curveData.camera;
            {
                var orthographicSize = camera.orthographicSize;
                var vertExtent = orthographicSize;
                var horzExtent = vertExtent * Screen.width / Screen.height;
                var cameraPosition = camera.transform.position;
                var screenCenterOffset = new Vector3(horzExtent, 0, vertExtent);

                Vector3 NegateX(Vector3 a) => new Vector3(-a.x, a.y, a.z);
                Vector3 NegateZ(Vector3 a) => new Vector3(a.x, a.y, -a.z);
                Vector3 ZeroXY(Vector3 a) => new Vector3(0, 0, a.z);
                Vector3 ZeroYZ(Vector3 a) => new Vector3(a.x, 0, 0);

                // var testCurve = new Bezier3DCurve(
                //     startPoint: cameraPosition - screenCenterOffset,
                //     firstHandle: cameraPosition + NegateX(screenCenterOffset),
                //     endPoint: cameraPosition + screenCenterOffset,
                //     secondHandle: cameraPosition + NegateZ(screenCenterOffset),
                //     steps: 60);

                {
                    Knot knot = new Knot(
                        position: cameraPosition - screenCenterOffset,
                        handleIn: new Vector3(screenCenterOffset.x * 2, 0, 0),
                        handleOut: new Vector3(0, 0, screenCenterOffset.z * 2));
                    _curveData.curve.AddKnot(knot);
                }
            }
        }
    }
}