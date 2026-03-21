using System.Collections.Generic;
using Dreamteck.Splines;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
public class RoundedRectSplineBuilder : MonoBehaviour
{
    [SerializeField] private SplineComputer _spline;

    [Header("Shape")]
    [SerializeField] private float _width = 14f;
    [SerializeField] private float _height = 20f;
    [SerializeField] private float _cornerRadius = 2.5f;

    [Header("Sampling")]
    [SerializeField, Range(2, 64)] private int _arcSegments = 20;
    [SerializeField] private float _lineStep = 1f;

    [Header("Spline Point Visuals")]
    [SerializeField] private float _pointSize = 1.5f;
    [SerializeField] private float _yOffset = 1.5f;
    [SerializeField] private float _zOffset = -4f;

    [Button]
    private void CreateSpline()
    {
        if (_spline == null) return;

        Vector3 center = transform.position + Vector3.up * _yOffset + Vector3.forward * _zOffset;

        float halfWidth = _width * 0.5f;
        float halfHeight = _height * 0.5f;

        float r = Mathf.Clamp(_cornerRadius, 0f, Mathf.Min(halfWidth, halfHeight));

        Vector3 brCenter = new Vector3(center.x + halfWidth - r, center.y, center.z - halfHeight + r); // bottom-right
        Vector3 trCenter = new Vector3(center.x + halfWidth - r, center.y, center.z + halfHeight - r); // top-right
        Vector3 tlCenter = new Vector3(center.x - halfWidth + r, center.y, center.z + halfHeight - r); // top-left
        Vector3 blCenter = new Vector3(center.x - halfWidth + r, center.y, center.z - halfHeight + r); // bottom-left

        Vector3 start = new Vector3(center.x - halfWidth + r, center.y, center.z - halfHeight);

        List<Vector3> points = new List<Vector3>();
        points.Add(start);

        AddLineSampled(
            points,
            start,
            new Vector3(center.x + halfWidth - r, center.y, center.z - halfHeight),
            _lineStep
        );

        // 2) Bottom-right arc: -90° -> 0°
        AddArc(points, brCenter, r, -90f, 0f, _arcSegments);

        // 3) Right edge
        AddLineSampled(
            points,
            new Vector3(center.x + halfWidth, center.y, center.z - halfHeight + r),
            new Vector3(center.x + halfWidth, center.y, center.z + halfHeight - r),
            _lineStep
        );

        // 4) Top-right arc: 0° -> 90°
        AddArc(points, trCenter, r, 0f, 90f, _arcSegments);

        // 5) Top edge
        AddLineSampled(
            points,
            new Vector3(center.x + halfWidth - r, center.y, center.z + halfHeight),
            new Vector3(center.x - halfWidth + r, center.y, center.z + halfHeight),
            _lineStep
        );

        // 6) Top-left arc: 90° -> 180°
        AddArc(points, tlCenter, r, 90f, 180f, _arcSegments);

        // 7) Left edge
        AddLineSampled(
            points,
            new Vector3(center.x - halfWidth, center.y, center.z + halfHeight - r),
            new Vector3(center.x - halfWidth, center.y, center.z - halfHeight + r),
            _lineStep
        );

        // 8) Bottom-left arc: 180° -> 270°  
        AddArc(points, blCenter, r, 180f, 270f, _arcSegments);

        SplinePoint[] splinePoints = new SplinePoint[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            splinePoints[i] = new SplinePoint
            {
                position = points[i],
                normal = Vector3.up,
                size = _pointSize,
                color = Color.white
            };
        }

        _spline.type = Spline.Type.Linear;
        _spline.SetPoints(splinePoints);
        _spline.RebuildImmediate();
        _spline.GetComponent<SplineMesh>().RebuildImmediate();
    }

    private static void AddLineSampled(List<Vector3> pts, Vector3 from, Vector3 to, float step)
    {
        float dist = Vector3.Distance(from, to);
        int segments = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.0001f, step)));

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(from, to, t);
            pts.Add(p);
        }
    }

    private static void AddArc(List<Vector3> pts, Vector3 center, float radius, float startDeg, float endDeg, int segments)
    {
        segments = Mathf.Max(1, segments);

        float startRad = startDeg * Mathf.Deg2Rad;
        float endRad = endDeg * Mathf.Deg2Rad;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = Mathf.Lerp(startRad, endRad, t);

            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;

            Vector3 p = new Vector3(center.x + x, center.y, center.z + z);
            pts.Add(p);
        }
    }
}
}