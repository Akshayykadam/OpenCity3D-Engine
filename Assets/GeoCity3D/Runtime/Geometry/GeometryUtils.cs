using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public static class GeometryUtils
    {
        // Simple Ear Clipping Triangulation
        // Based on standard algorithms for simple polygons without holes
        public static List<int> Triangulate(List<Vector3> points)
        {
            List<int> indices = new List<int>();
            int n = points.Count;
            if (n < 3) return indices;

            int[] V = new int[n];
            if (Area(points) > 0)
            {
                for (int v = 0; v < n; v++) V[v] = v;
            }
            else
            {
                for (int v = 0; v < n; v++) V[v] = (n - 1) - v;
            }

            int nv = n;
            int count = 2 * nv;
            for (int m = 0, v = nv - 1; nv > 2; )
            {
                if ((count--) <= 0) return indices;

                int u = v;
                if (nv <= u) u = 0;
                v = u + 1;
                if (nv <= v) v = 0;
                int w = v + 1;
                if (nv <= w) w = 0;

                if (Snip(points, u, v, w, nv, V))
                {
                    int a, b, c, s, t;
                    a = V[u];
                    b = V[v];
                    c = V[w];
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                    m++;
                    for (s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                    nv--;
                    count = 2 * nv;
                }
            }

            indices.Reverse();
            return indices;
        }

        private static float Area(List<Vector3> points)
        {
            int n = points.Count;
            float A = 0.0f;
            for (int p = n - 1, q = 0; q < n; p = q++)
            {
                A += points[p].x * points[q].z - points[q].x * points[p].z;
            }
            return A * 0.5f;
        }

        private static bool Snip(List<Vector3> points, int u, int v, int w, int n, int[] V)
        {
            int p;
            Vector3 A = points[V[u]];
            Vector3 B = points[V[v]];
            Vector3 C = points[V[w]];

            if (Mathf.Epsilon > (((B.x - A.x) * (C.z - A.z)) - ((B.z - A.z) * (C.x - A.x)))) return false;

            for (p = 0; p < n; p++)
            {
                if ((p == u) || (p == v) || (p == w)) continue;
                Vector3 P = points[V[p]];
                if (InsideTriangle(A, B, C, P)) return false;
            }
            return true;
        }

        private static bool InsideTriangle(Vector3 A, Vector3 B, Vector3 C, Vector3 P)
        {
            float ax, az, bx, bz, cx, cz, px, pz;
            float cCROSSap, bCROSScp, aCROSSbp;

            ax = C.x - B.x; az = C.z - B.z;
            bx = A.x - C.x; bz = A.z - C.z;
            cx = B.x - A.x; cz = B.z - A.z;
            px = P.x - A.x; pz = P.z - A.z;

            return ((ax * pz - az * px) >= 0.0f) &&
                   ((bx * (P.z - B.z) - bz * (P.x - B.x)) >= 0.0f) &&
                   ((cx * pz - cz * px) >= 0.0f);
        }

        // ═══════════════════════════════════════════════
        // CATMULL-ROM SPLINE — smooth road curves
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Smooth a polyline path using Catmull-Rom spline interpolation.
        /// Each segment is subdivided into 'subdivisions' sub-segments.
        /// </summary>
        public static List<Vector3> SmoothPath(List<Vector3> points, int subdivisions = 4)
        {
            if (points == null || points.Count < 3) return points;

            // Remove near-duplicate points that cause zero-length segments
            List<Vector3> clean = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count; i++)
            {
                if (Vector3.Distance(points[i], clean[clean.Count - 1]) > 0.5f)
                    clean.Add(points[i]);
            }
            if (clean.Count < 3) return clean;

            List<Vector3> result = new List<Vector3>();
            int n = clean.Count;

            for (int i = 0; i < n - 1; i++)
            {
                // Catmull-Rom needs 4 control points: p0, p1, p2, p3
                // Clamp at endpoints
                Vector3 p0 = clean[Mathf.Max(i - 1, 0)];
                Vector3 p1 = clean[i];
                Vector3 p2 = clean[Mathf.Min(i + 1, n - 1)];
                Vector3 p3 = clean[Mathf.Min(i + 2, n - 1)];

                for (int s = 0; s < subdivisions; s++)
                {
                    float t = (float)s / subdivisions;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            // Add final point
            result.Add(clean[n - 1]);
            return result;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        // ═══════════════════════════════════════════════
        // CONVEX HULL (Monotone Chain algorithm)
        // ═══════════════════════════════════════════════

        public static List<Vector3> GetConvexHull(List<Vector3> points)
        {
            if (points == null || points.Count <= 3)
                return new List<Vector3>(points);

            // Sort points lexicographically (first by x, then by z)
            var sortedPoints = points.OrderBy(p => p.x).ThenBy(p => p.z).ToList();

            List<Vector3> hull = new List<Vector3>();

            // Build lower hull
            foreach (var p in sortedPoints)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(p);
            }

            // Build upper hull
            int lowerCount = hull.Count;
            for (int i = sortedPoints.Count - 2; i >= 0; i--)
            {
                var p = sortedPoints[i];
                while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(p);
            }

            // Remove the last point because it's the same as the first one
            hull.RemoveAt(hull.Count - 1);

            return hull;
        }

        // 2D cross product of OA and OB vectors (using X and Z axes)
        // Returns positive if OAB makes a counter-clockwise turn,
        // negative for clockwise, and zero if the points are collinear.
        private static float Cross(Vector3 o, Vector3 a, Vector3 b)
        {
            return (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
        }
    }
}
