using System.Collections.Generic;
using GeoCity3D.Data;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates civil-engineered bridge meshes with seamless road connections:
    /// - Smooth cubic approach ramps starting flush with the connecting road (0.08m)
    /// - Solid grounded abutment retaining walls anchoring ramps into the banks
    /// - Stately concrete abutment piers at the ramp-to-span transition
    /// - Open elevated river/chasm spans with crossbeam support pillars (never blocking road entrances)
    /// - Continuous railings and optional raised pedestrian sidewalks
    /// </summary>
    public class BridgeBuilder
    {
        public const float ROAD_SURFACE_Y = 0.08f;       // Matches RoadBuilder.ROAD_Y_SURFACE
        public const float BRIDGE_ELEVATION = 4.5f;      // Clearance height above ground/water
        public const float BRIDGE_DECK_THICKNESS = 0.5f; // Structural deck slab thickness
        public const float BRIDGE_RAIL_HEIGHT = 1.0f;    // Side railing height
        public const float BRIDGE_RAIL_THICKNESS = 0.15f;
        public const float SIDEWALK_WIDTH = 1.2f;
        public const float SIDEWALK_CURB = 0.08f;
        public const float PILLAR_WIDTH = 1.2f;
        public const float PILLAR_SPACING = 20f;

        public static GameObject Build(List<Vector3> path, float width,
            Material roadMat, Material sidewalkMat, long id, string highwayType,
            bool rampAtStart = true, bool rampAtEnd = true,
            float startElevation = ROAD_SURFACE_Y, float endElevation = ROAD_SURFACE_Y)
        {
            if (path == null || path.Count < 2) return null;

            GameObject parent = new GameObject($"Bridge_{id}");

            // ── 1. Measure Total Length & Compute Ramp Parameters ──
            float totalLength = 0f;
            for (int i = 1; i < path.Count; i++)
                totalLength += Vector3.Distance(path[i], path[i - 1]);

            if (totalLength < 4f)
            {
                // Fallback for microscopic bridges / culverts
                float elev = (!rampAtStart || !rampAtEnd) ? BRIDGE_ELEVATION : ROAD_SURFACE_Y;
                GameObject fallbackRoad = RoadBuilder.CreateSolidStrip(path, width, elev, 0.12f, roadMat, $"BridgeRoad_{id}");
                if (fallbackRoad != null) fallbackRoad.transform.SetParent(parent.transform);
                return parent;
            }

            // Smooth slope parameters (~14% max grade for comfortable vehicle ascent)
            float maxGrade = 0.14f;
            float targetElevation = BRIDGE_ELEVATION;

            float startRampLen = 0f;
            if (rampAtStart)
            {
                startRampLen = Mathf.Clamp(totalLength * 0.32f, 6f, 26f);
                float maxElev = startElevation + startRampLen * maxGrade;
                targetElevation = Mathf.Min(targetElevation, maxElev);
            }

            float endRampLen = 0f;
            if (rampAtEnd)
            {
                endRampLen = Mathf.Clamp(totalLength * 0.32f, 6f, 26f);
                float maxElev = endElevation + endRampLen * maxGrade;
                targetElevation = Mathf.Min(targetElevation, maxElev);
            }

            // ── 2. Dense 3D Path Sampling (~2m spacing) ──
            float sampleInterval = 2.0f;
            int sampleCount = Mathf.Max(12, Mathf.CeilToInt(totalLength / sampleInterval) + 1);

            List<Vector3> denseDeckPath = new List<Vector3>(sampleCount);
            List<float> sampleDistances = new List<float>(sampleCount);

            for (int s = 0; s < sampleCount; s++)
            {
                float t = (float)s / (sampleCount - 1);
                float dist = t * totalLength;
                Vector3 pt = RoadBuilder.GetPointAlongPath(path, t);
                pt.y = ComputeElevation(dist, totalLength, startRampLen, endRampLen, targetElevation,
                    rampAtStart, rampAtEnd, startElevation, endElevation);
                denseDeckPath.Add(pt);
                sampleDistances.Add(dist);
            }

            // Guarantee exact boundary contact with incoming and outgoing ground roads
            Vector3 startPt = denseDeckPath[0];
            startPt.y = rampAtStart ? startElevation : targetElevation;
            denseDeckPath[0] = startPt;

            Vector3 endPt = denseDeckPath[denseDeckPath.Count - 1];
            endPt.y = rampAtEnd ? endElevation : targetElevation;
            denseDeckPath[denseDeckPath.Count - 1] = endPt;

            // ── 3. Bridge Roadway Deck (3D Solid Strip) ──
            GameObject roadway = RoadBuilder.CreateSolidStrip(denseDeckPath, width,
                BRIDGE_DECK_THICKNESS, roadMat, $"BridgeRoadway_{id}");
            if (roadway != null) roadway.transform.SetParent(parent.transform);

            // ── 4. Solid Retaining Walls Under Approach Ramps (Abutment Wings) ──
            Material concreteMat = sidewalkMat != null ? sidewalkMat : roadMat;
            if (rampAtStart && startRampLen > 2f)
            {
                GameObject startAbutmentWalls = CreateRampRetainingWalls(denseDeckPath, sampleDistances,
                    0f, startRampLen, width, concreteMat, $"AbutmentWalls_Start_{id}");
                if (startAbutmentWalls != null) startAbutmentWalls.transform.SetParent(parent.transform);
            }

            if (rampAtEnd && endRampLen > 2f)
            {
                GameObject endAbutmentWalls = CreateRampRetainingWalls(denseDeckPath, sampleDistances,
                    totalLength - endRampLen, totalLength, width, concreteMat, $"AbutmentWalls_End_{id}");
                if (endAbutmentWalls != null) endAbutmentWalls.transform.SetParent(parent.transform);
            }

            // ── 5. Abutment Anchor Piers at Ramp-to-Span Transitions ──
            if (rampAtStart && startRampLen > 2f)
            {
                Vector3 startPierPos = RoadBuilder.GetPointAlongPath(denseDeckPath, startRampLen / totalLength);
                float startPierHeight = Mathf.Max(0.5f, startPierPos.y - BRIDGE_DECK_THICKNESS);
                startPierPos.y = 0f;
                GameObject startPier = CreateAbutmentPier(startPierPos, width + 0.8f, 1.8f, startPierHeight,
                    concreteMat, $"AbutmentPier_Start_{id}");
                if (startPier != null) startPier.transform.SetParent(parent.transform);
            }

            if (rampAtEnd && endRampLen > 2f)
            {
                Vector3 endPierPos = RoadBuilder.GetPointAlongPath(denseDeckPath, (totalLength - endRampLen) / totalLength);
                float endPierHeight = Mathf.Max(0.5f, endPierPos.y - BRIDGE_DECK_THICKNESS);
                endPierPos.y = 0f;
                GameObject endPier = CreateAbutmentPier(endPierPos, width + 0.8f, 1.8f, endPierHeight,
                    concreteMat, $"AbutmentPier_End_{id}");
                if (endPier != null) endPier.transform.SetParent(parent.transform);
            }

            // ── 6. Open Elevated Span Support Pillars ──
            float spanStart = rampAtStart ? (startRampLen + 4f) : 2f;
            float spanEnd = rampAtEnd ? (totalLength - endRampLen - 4f) : (totalLength - 2f);
            float spanLength = spanEnd - spanStart;

            if (spanLength >= 10f)
            {
                int pillarCount = Mathf.Max(1, Mathf.RoundToInt(spanLength / PILLAR_SPACING));
                for (int p = 1; p <= pillarCount; p++)
                {
                    float frac = (float)p / (pillarCount + 1);
                    float pDist = spanStart + frac * spanLength;
                    Vector3 pPos = RoadBuilder.GetPointAlongPath(denseDeckPath, pDist / totalLength);
                    float deckY = pPos.y;
                    pPos.y = 0f;

                    float pillarHeight = deckY - BRIDGE_DECK_THICKNESS;
                    if (pillarHeight >= 1.2f)
                    {
                        GameObject pillar = CreatePillarWithCap(pPos, PILLAR_WIDTH, width + 0.5f, pillarHeight,
                            concreteMat, $"Pillar_{id}_{p}");
                        if (pillar != null) pillar.transform.SetParent(parent.transform);
                    }
                }
            }

            // ── 7. Pedestrian Sidewalks (Left & Right) ──
            float halfRoad = width / 2f;
            bool hasSidewalks = sidewalkMat != null;

            if (hasSidewalks)
            {
                List<Vector3> leftSWPath = new List<Vector3>(denseDeckPath.Count);
                List<Vector3> rightSWPath = new List<Vector3>(denseDeckPath.Count);

                for (int i = 0; i < denseDeckPath.Count; i++)
                {
                    Vector3 forward = RoadBuilder.GetForward(denseDeckPath, i);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    Vector3 pt = denseDeckPath[i];
                    pt.y += SIDEWALK_CURB;

                    leftSWPath.Add(pt - right * (halfRoad + SIDEWALK_WIDTH / 2f));
                    rightSWPath.Add(pt + right * (halfRoad + SIDEWALK_WIDTH / 2f));
                }

                GameObject swL = RoadBuilder.CreateSolidStrip(leftSWPath, SIDEWALK_WIDTH, 0.16f,
                    sidewalkMat, $"BridgeSidewalkL_{id}");
                GameObject swR = RoadBuilder.CreateSolidStrip(rightSWPath, SIDEWALK_WIDTH, 0.16f,
                    sidewalkMat, $"BridgeSidewalkR_{id}");

                if (swL != null) swL.transform.SetParent(parent.transform);
                if (swR != null) swR.transform.SetParent(parent.transform);
            }

            // ── 8. Safety Railings (Left & Right) ──
            float railOffset = halfRoad + (hasSidewalks ? SIDEWALK_WIDTH : 0f) + BRIDGE_RAIL_THICKNESS / 2f;
            float curbOffset = hasSidewalks ? SIDEWALK_CURB : 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                List<Vector3> railPath = new List<Vector3>(denseDeckPath.Count);
                for (int i = 0; i < denseDeckPath.Count; i++)
                {
                    Vector3 forward = RoadBuilder.GetForward(denseDeckPath, i);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    Vector3 pt = denseDeckPath[i] + right * (railOffset * side);
                    pt.y += curbOffset + BRIDGE_RAIL_HEIGHT / 2f;
                    railPath.Add(pt);
                }

                string railName = side < 0 ? $"RailL_{id}" : $"RailR_{id}";
                GameObject rail = RoadBuilder.CreateSolidStrip(railPath, BRIDGE_RAIL_THICKNESS,
                    BRIDGE_RAIL_HEIGHT, concreteMat, railName);
                if (rail != null) rail.transform.SetParent(parent.transform);

                // Concrete end-post barriers at the road interface (only at true terminal ground ends)
                if (railPath.Count >= 2)
                {
                    if (rampAtStart)
                    {
                        Vector3 postStart = railPath[0];
                        postStart.y = startElevation + curbOffset;
                        GameObject p1 = CreateEndPost(postStart, 0.35f, 1.1f, concreteMat, $"RailPost_Start_{id}_{side}");
                        if (p1 != null) p1.transform.SetParent(parent.transform);
                    }

                    if (rampAtEnd)
                    {
                        Vector3 postEnd = railPath[railPath.Count - 1];
                        postEnd.y = endElevation + curbOffset;
                        GameObject p2 = CreateEndPost(postEnd, 0.35f, 1.1f, concreteMat, $"RailPost_End_{id}_{side}");
                        if (p2 != null) p2.transform.SetParent(parent.transform);
                    }
                }
            }

            return parent;
        }

        // ══════════════════════════════════════════════════════════════
        //  ELEVATION PROFILE COMPUTATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes smooth elevation along the bridge path:
        /// Respects rampAtStart and rampAtEnd flags so connecting bridge segments remain elevated without dipping.
        /// </summary>
        public static float ComputeElevation(float dist, float totalLength,
            float startRampLength, float endRampLength, float targetElevation,
            bool rampAtStart = true, bool rampAtEnd = true,
            float startElevation = ROAD_SURFACE_Y, float endElevation = ROAD_SURFACE_Y)
        {
            if (dist <= 0f) return rampAtStart ? startElevation : targetElevation;
            if (dist >= totalLength) return rampAtEnd ? endElevation : targetElevation;

            if (rampAtStart && dist < startRampLength)
            {
                float t = dist / startRampLength;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                return Mathf.Lerp(startElevation, targetElevation, smoothT);
            }
            else if (rampAtEnd && dist > totalLength - endRampLength)
            {
                float t = (totalLength - dist) / endRampLength;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                return Mathf.Lerp(endElevation, targetElevation, smoothT);
            }
            else
            {
                // Middle span over river / chasm — add gentle 0.25m parabolic crown if span is wide enough
                float spanStart = rampAtStart ? startRampLength : 0f;
                float spanEnd = rampAtEnd ? (totalLength - endRampLength) : totalLength;
                float spanLen = spanEnd - spanStart;
                float crown = 0f;
                if (spanLen > 8f)
                {
                    float midT = Mathf.Clamp01((dist - spanStart) / spanLen);
                    crown = Mathf.Sin(midT * Mathf.PI) * 0.25f;
                }
                return targetElevation + crown;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID RAMP RETAINING WALLS (ABUTMENTS)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates solid concrete retaining walls on left & right sides of the approach ramp,
        /// extending from the roadway edge down to y=0 to anchor the ramp into the ground.
        /// </summary>
        private static GameObject CreateRampRetainingWalls(List<Vector3> path, List<float> distances,
            float startDist, float endDist, float roadWidth, Material mat, string name)
        {
            List<int> rampIndices = new List<int>();
            for (int i = 0; i < distances.Count; i++)
            {
                if (distances[i] >= startDist - 0.1f && distances[i] <= endDist + 0.1f)
                    rampIndices.Add(i);
            }

            if (rampIndices.Count < 2) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float halfW = roadWidth / 2f;
            float uvX = 0f;

            // Generate left and right retaining wall ribbons down to y = 0
            for (int side = -1; side <= 1; side += 2)
            {
                int baseVert = verts.Count;
                uvX = 0f;

                for (int k = 0; k < rampIndices.Count; k++)
                {
                    int idx = rampIndices[k];
                    Vector3 pos = path[idx];
                    Vector3 fwd = RoadBuilder.GetForward(path, idx);
                    Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;

                    Vector3 topPt = pos + rgt * (halfW * side);
                    Vector3 botPt = new Vector3(topPt.x, 0f, topPt.z);

                    verts.Add(topPt); // baseVert + 2*k + 0
                    verts.Add(botPt); // baseVert + 2*k + 1

                    if (k > 0)
                    {
                        float d = Vector3.Distance(path[rampIndices[k]], path[rampIndices[k - 1]]);
                        uvX += d * 0.25f;
                    }

                    uvs.Add(new Vector2(uvX, topPt.y * 0.25f));
                    uvs.Add(new Vector2(uvX, 0f));
                }

                for (int k = 0; k < rampIndices.Count - 1; k++)
                {
                    int b0 = baseVert + 2 * k;
                    int b1 = b0 + 1;
                    int n0 = b0 + 2;
                    int n1 = b0 + 3;

                    if (side < 0)
                    {
                        // Left wall facing outward (-X)
                        tris.Add(b0); tris.Add(n0); tris.Add(b1);
                        tris.Add(b1); tris.Add(n0); tris.Add(n1);
                    }
                    else
                    {
                        // Right wall facing outward (+X)
                        tris.Add(b0); tris.Add(b1); tris.Add(n0);
                        tris.Add(b1); tris.Add(n1); tris.Add(n0);
                    }
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        // ══════════════════════════════════════════════════════════════
        //  ABUTMENT PIER & SUPPORT PILLARS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Solid concrete abutment pier block at the transition point where the bridge leaves the riverbank.
        /// </summary>
        private static GameObject CreateAbutmentPier(Vector3 basePos, float width, float depth, float height,
            Material mat, string name)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            float hw = width / 2f;
            float hd = depth / 2f;

            Mesh mesh = new Mesh();
            Vector3[] verts = new Vector3[]
            {
                // Bottom
                basePos + new Vector3(-hw, 0f, -hd),
                basePos + new Vector3( hw, 0f, -hd),
                basePos + new Vector3( hw, 0f,  hd),
                basePos + new Vector3(-hw, 0f,  hd),
                // Top
                basePos + new Vector3(-hw, height, -hd),
                basePos + new Vector3( hw, height, -hd),
                basePos + new Vector3( hw, height,  hd),
                basePos + new Vector3(-hw, height,  hd),
            };

            int[] tris = new int[]
            {
                // Front
                0,4,1, 1,4,5,
                // Right
                1,5,2, 2,5,6,
                // Back
                2,6,3, 3,6,7,
                // Left
                3,7,0, 0,7,4,
                // Top
                4,7,5, 5,7,6,
                // Bottom
                0,1,3, 1,2,3,
            };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        /// <summary>
        /// Concrete bridge pier consisting of a central column and a wide transverse cap beam
        /// that directly supports the bridge deck underside.
        /// </summary>
        private static GameObject CreatePillarWithCap(Vector3 basePos, float colWidth, float capWidth,
            float totalHeight, Material mat, string name)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            float capHeight = 0.5f;
            float colHeight = Mathf.Max(0.2f, totalHeight - capHeight);
            float hColW = colWidth / 2f;
            float hCapW = capWidth / 2f;
            float capDepth = colWidth * 1.2f;
            float hCapD = capDepth / 2f;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            // 1. Column Box (basePos to basePos + colHeight)
            AddBox(verts, tris, basePos, new Vector3(-hColW, 0f, -hColW),
                new Vector3(hColW, colHeight, hColW));

            // 2. Cap Beam Box (sitting on top of column)
            Vector3 capBase = basePos + new Vector3(0f, colHeight, 0f);
            AddBox(verts, tris, capBase, new Vector3(-hCapW, 0f, -hCapD),
                new Vector3(hCapW, capHeight, hCapD));

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        private static GameObject CreateEndPost(Vector3 basePos, float size, float height,
            Material mat, string name)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            float hs = size / 2f;
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            AddBox(verts, tris, basePos, new Vector3(-hs, 0f, -hs), new Vector3(hs, height, hs));

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        private static void AddBox(List<Vector3> verts, List<int> tris, Vector3 origin, Vector3 min, Vector3 max)
        {
            int b = verts.Count;
            verts.Add(origin + new Vector3(min.x, min.y, min.z)); // 0
            verts.Add(origin + new Vector3(max.x, min.y, min.z)); // 1
            verts.Add(origin + new Vector3(max.x, min.y, max.z)); // 2
            verts.Add(origin + new Vector3(min.x, min.y, max.z)); // 3

            verts.Add(origin + new Vector3(min.x, max.y, min.z)); // 4
            verts.Add(origin + new Vector3(max.x, max.y, min.z)); // 5
            verts.Add(origin + new Vector3(max.x, max.y, max.z)); // 6
            verts.Add(origin + new Vector3(min.x, max.y, max.z)); // 7

            // Front
            tris.Add(b+0); tris.Add(b+4); tris.Add(b+1);
            tris.Add(b+1); tris.Add(b+4); tris.Add(b+5);
            // Right
            tris.Add(b+1); tris.Add(b+5); tris.Add(b+2);
            tris.Add(b+2); tris.Add(b+5); tris.Add(b+6);
            // Back
            tris.Add(b+2); tris.Add(b+6); tris.Add(b+3);
            tris.Add(b+3); tris.Add(b+6); tris.Add(b+7);
            // Left
            tris.Add(b+3); tris.Add(b+7); tris.Add(b+0);
            tris.Add(b+0); tris.Add(b+7); tris.Add(b+4);
            // Top
            tris.Add(b+4); tris.Add(b+7); tris.Add(b+5);
            tris.Add(b+5); tris.Add(b+7); tris.Add(b+6);
            // Bottom
            tris.Add(b+0); tris.Add(b+1); tris.Add(b+3);
            tris.Add(b+1); tris.Add(b+2); tris.Add(b+3);
        }
    }
}
