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
    }
}
