using System.Collections.Generic;
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
            int count = 3 * nv;
            for (int m = 0, v = nv - 1; nv > 2; )
            {
                if ((count--) <= 0) break; // Break out to fallback instead of returning partial/empty

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
                    count = 3 * nv;
                }
            }

            // If ear clipping completed fully, reverse indices to match Unity winding (Clockwise / Upward normal)
            if (nv <= 2)
            {
                indices.Reverse();
                return indices;
            }

            // Fallback: If ear clipping got stuck or produced fewer triangles than needed,
            // complete the remaining polygon with fan triangulation so no holes occur.
            if (indices.Count > 0)
            {
                // Fan triangulate the remaining nv vertices using the same winding as clipped ears
                for (int i = 1; i < nv - 1; i++)
                {
                    indices.Add(V[0]);
                    indices.Add(V[i]);
                    indices.Add(V[i + 1]);
                }
                indices.Reverse();
                return indices;
            }

            // Complete fallback: simple fan triangulation from vertex 0 with Clockwise winding (Upward normal)
            int[] order = new int[n];
            if (Area(points) > 0)
            {
                for (int i = 0; i < n; i++) order[i] = i;
            }
            else
            {
                for (int i = 0; i < n; i++) order[i] = (n - 1) - i;
            }

            for (int i = 1; i < n - 1; i++)
            {
                indices.Add(order[0]);
                indices.Add(order[i + 1]);
                indices.Add(order[i]);
            }

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
            // Proper 2D cross-products on the XZ plane:
            // Point P is inside counter-clockwise triangle ABC iff P is on the left of AB, BC, and CA.
            float c1 = (B.x - A.x) * (P.z - A.z) - (B.z - A.z) * (P.x - A.x);
            float c2 = (C.x - B.x) * (P.z - B.z) - (C.z - B.z) * (P.x - B.x);
            float c3 = (A.x - C.x) * (P.z - C.z) - (A.z - C.z) * (P.x - C.x);

            return (c1 >= -1e-5f && c2 >= -1e-5f && c3 >= -1e-5f);
        }

        // ═══════════════════════════════════════════════
        // CATMULL-ROM SPLINE — smooth road curves
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Smooth a polyline path using Centripetal Catmull-Rom spline interpolation (alpha = 0.5).
        /// Centripetal parameterization guarantees NO self-intersections, cusps, or overshoot loops.
        /// Preserves exact start and end node positions.
        /// </summary>
        public static List<Vector3> SmoothPath(List<Vector3> points, int subdivisions = 4)
        {
            if (points == null || points.Count < 3) return points;

            // Filter out near-duplicate internal points while preserving exact start point
            List<Vector3> clean = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (Vector3.Distance(points[i], clean[clean.Count - 1]) > 0.4f)
                    clean.Add(points[i]);
            }

            // Always preserve exact end point
            Vector3 lastPt = points[points.Count - 1];
            if (Vector3.Distance(clean[clean.Count - 1], lastPt) > 0.1f)
                clean.Add(lastPt);
            else
                clean[clean.Count - 1] = lastPt;

            if (clean.Count < 3) return clean;

            List<Vector3> result = new List<Vector3>();
            int n = clean.Count;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = clean[Mathf.Max(i - 1, 0)];
                Vector3 p1 = clean[i];
                Vector3 p2 = clean[i + 1];
                Vector3 p3 = clean[Mathf.Min(i + 2, n - 1)];

                // Check angle: if turn is extremely sharp (>110 degrees), interpolate linearly to avoid any weird cusp
                Vector3 seg1 = (p1 - p0).normalized;
                Vector3 seg2 = (p2 - p1).normalized;
                bool isSharp = (i > 0 && Vector3.Dot(seg1, seg2) < -0.2f);

                for (int s = 0; s < subdivisions; s++)
                {
                    float t = (float)s / subdivisions;
                    if (isSharp)
                    {
                        result.Add(Vector3.Lerp(p1, p2, t));
                    }
                    else
                    {
                        result.Add(CentripetalCatmullRom(p0, p1, p2, p3, t));
                    }
                }
            }

            // Add final point exactly
            result.Add(clean[n - 1]);
            return result;
        }

        /// <summary>
        /// Barry and Goldman's pyramidal formulation of Centripetal Catmull-Rom (alpha = 0.5).
        /// </summary>
        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float dt0 = Mathf.Pow(Mathf.Max(0.0001f, (p1 - p0).sqrMagnitude), 0.25f);
            float dt1 = Mathf.Pow(Mathf.Max(0.0001f, (p2 - p1).sqrMagnitude), 0.25f);
            float dt2 = Mathf.Pow(Mathf.Max(0.0001f, (p3 - p2).sqrMagnitude), 0.25f);

            // Time coordinates along spline
            float t0 = 0f;
            float t1 = t0 + dt0;
            float t2 = t1 + dt1;
            float t3 = t2 + dt2;

            float curT = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = (t1 - curT) / (t1 - t0) * p0 + (curT - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - curT) / (t2 - t1) * p1 + (curT - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - curT) / (t3 - t2) * p2 + (curT - t2) / (t3 - t2) * p3;

            Vector3 b1 = (t2 - curT) / (t2 - t0) * a1 + (curT - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - curT) / (t3 - t1) * a2 + (curT - t1) / (t3 - t1) * a3;

            Vector3 c = (t2 - curT) / (t2 - t1) * b1 + (curT - t1) / (t2 - t1) * b2;
            return c;
        }

        // ═══════════════════════════════════════════════
        // CONVEX HULL (Monotone Chain algorithm)
        // ═══════════════════════════════════════════════

        public static List<Vector3> GetConvexHull(List<Vector3> points)
        {
            if (points == null || points.Count <= 3)
                return new List<Vector3>(points);

            // Sort points lexicographically (first by x, then by z)
            List<Vector3> sortedPoints = new List<Vector3>(points);
            sortedPoints.Sort((a, b) =>
            {
                int cmp = a.x.CompareTo(b.x);
                return cmp != 0 ? cmp : a.z.CompareTo(b.z);
            });

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

        // ═══════════════════════════════════════════════
        // 2D POINT-IN-POLYGON & DISTANCE QUERIES
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Robust 2D Jordan curve ray-casting test.
        /// Returns true if the point (x, z) lies strictly inside the 2D polygon in the XZ plane.
        /// Works for convex, concave, clockwise, or counter-clockwise polygons.
        /// </summary>
        public static bool PointInPolygon(float x, float z, List<Vector3> poly)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false;
            int j = poly.Count - 1;
            for (int i = 0; i < poly.Count; j = i++)
            {
                if (((poly[i].z > z) != (poly[j].z > z)) &&
                    (x < (poly[j].x - poly[i].x) * (z - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Computes the squared 2D distance between a point P and line segment AB in the XZ plane.
        /// </summary>
        public static float DistancePointToSegmentSqr2D(Vector3 p, Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float lenSqr = dx * dx + dz * dz;
            if (lenSqr < 1e-6f)
            {
                float px = p.x - a.x;
                float pz = p.z - a.z;
                return px * px + pz * pz;
            }

            float t = ((p.x - a.x) * dx + (p.z - a.z) * dz) / lenSqr;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            float projX = a.x + t * dx;
            float projZ = a.z + t * dz;
            float diffX = p.x - projX;
            float diffZ = p.z - projZ;
            return diffX * diffX + diffZ * diffZ;
        }
    }
}
