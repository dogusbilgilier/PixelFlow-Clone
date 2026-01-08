#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;

/// <summary>
/// Creates a spline points while maintaining the starting and ending points.
/// </summary>
public class BorderSplineBuilder : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private SplineComputer _spline;

    [Title("Rect")]
    [SerializeField] private float _width = 15f;

    [SerializeField] private float _height = 20f;

    [Title("Corner")]
    [SerializeField] private float _cornerRadius = 2f;

    [SerializeField] private int _cornerSegments = 6;

    [Title("Sampling (helps CatmullRom stay straight)")]
    [SerializeField] private float _straightPointSpacing = 2f;

    [Title("SplinePoint")]
    [SerializeField] private float _yOffset = 1f;

    [SerializeField] private float _pointSize = 1.5f;

    [Button]
    private void CreateSpline()
    {
        var existing = _spline.GetPoints();
        if (existing == null || existing.Length < 2) return;

        float left = transform.position.x - _width * 0.5f;
        float right = transform.position.x + _width * 0.5f;
        float bottom = transform.position.z - _height * 0.5f;
        float top = transform.position.z + _height * 0.5f;

        float radius = Mathf.Clamp(_cornerRadius, 0f, Mathf.Min(_width, _height) * 0.5f);

        float startXPos = existing[0].position.x;
        float endZPos = existing[^1].position.z;

        Vector3 start = new Vector3(startXPos, 0f, bottom);
        Vector3 end = new Vector3(left, 0f, endZPos);

        List<Vector3> positions = GenerateBorderSegment(left, right, bottom, top, start, end, radius, _cornerSegments, _straightPointSpacing);
        SetSplineFromPositions(_spline, positions, _yOffset, _pointSize);
    }

    private List<Vector3> GenerateBorderSegment(float left, float right, float bottom, float top, Vector3 start, Vector3 end, float radius, int arcSegments, float straightSpacing)
    {
        var pts = new List<Vector3>(64);
        float y = start.y;

        // bottom: start -> (right - r, bottom)
        AddLineSampled(pts, start, new Vector3(right - radius, y, bottom), straightSpacing, includeStart: true);

        // bottom-right arc: center (right-r, bottom+r), angle -90 -> 0
        AddArc(pts, new Vector3(right - radius, y, bottom + radius), radius, -90f, 0f, arcSegments, skipFirst: true);

        // right: (right, bottom+r) -> (right, top-r)
        AddLineSampled(pts, new Vector3(right, y, bottom + radius), new Vector3(right, y, top - radius), straightSpacing, includeStart: false);

        // top-right arc: 0 -> 90
        AddArc(pts, new Vector3(right - radius, y, top - radius), radius, 0f, 90f, arcSegments, skipFirst: true);

        // top: (right-r, top) -> (left+r, top)
        AddLineSampled(pts, new Vector3(right - radius, y, top), new Vector3(left + radius, y, top), straightSpacing, includeStart: false);

        // top-left arc: 90 -> 180
        AddArc(pts, new Vector3(left + radius, y, top - radius), radius, 90f, 180f, arcSegments, skipFirst: true);

        // left: (left, top-r) -> end
        AddLineSampled(pts, new Vector3(left, y, top - radius), end, straightSpacing, includeStart: false);

        return pts;
    }

    private void AddLineSampled(List<Vector3> pts, Vector3 a, Vector3 b, float spacing, bool includeStart)
    {
        if (includeStart)
        {
            if (pts.Count == 0 || (pts[^1] - a).sqrMagnitude > Mathf.Epsilon) 
                pts.Add(a);
        }

        float dist = Vector3.Distance(a, b);
        if (spacing <= 0f || dist <= spacing)
        {
            if (pts.Count == 0 || (pts[^1] - b).sqrMagnitude > Mathf.Epsilon) 
                pts.Add(b);
            
            return;
        }

        int steps = Mathf.CeilToInt(dist / spacing);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = Vector3.Lerp(a, b, t);
            
            if (pts.Count == 0 || (pts[^1] - p).sqrMagnitude > Mathf.Epsilon) 
                pts.Add(p);
        }
    }

    private void AddArc(List<Vector3> pts, Vector3 center, float radius, float degFrom, float degTo, int segments, bool skipFirst)
    {
        const float eps = 1e-6f;

        int steps = Mathf.Max(1, segments);

        for (int i = 0; i <= steps; i++)
        {
            if (skipFirst && i == 0) continue;

            float t = i / (float)steps;
            float ang = Mathf.Lerp(degFrom, degTo, t) * Mathf.Deg2Rad;

            Vector3 p = new Vector3(center.x + Mathf.Cos(ang) * radius, center.y, center.z + Mathf.Sin(ang) * radius);

            if (pts.Count == 0 || (pts[^1] - p).sqrMagnitude > eps)
                pts.Add(p);
        }
    }

    private void SetSplineFromPositions(SplineComputer spline, List<Vector3> positions, float yOffset, float size)
    {
        var sp = new SplinePoint[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            sp[i] = new SplinePoint
            {
                position = positions[i] + Vector3.up * yOffset,
                normal = Vector3.up,
                size = size,
                color = Color.white
            };
        }

        spline.SetPoints(sp);
    }
}

#endif