using UnityEngine;
using System.Collections.Generic;

namespace GeoCity3D.Visuals
{
    public static class CityCombiner
    {
        // 350m spatial sectors for grass: balances layer distance culling with ultra-low chunk count (<30 chunks)
        public const float GRASS_SPATIAL_CELL_SIZE = 350f;

        /// <summary>
        /// Combines child meshes of a parent object by material into unified chunk meshes.
        /// Dramatically cuts draw calls from tens of thousands down to tens.
        /// Destroys all original individual child GameObjects to eliminate empty transform nodes.
        /// </summary>
        public static void CombineMeshesByMaterial(GameObject parentObj)
        {
            if (parentObj == null) return;

            MeshFilter[] meshFilters = parentObj.GetComponentsInChildren<MeshFilter>(false);
            if (meshFilters.Length == 0) return;

            string pName = parentObj.name.ToLower();
            bool isGrass = pName.Contains("grass");

            // Determine if this object type should cast shadows
            bool shouldCastShadows = true;
            if (isGrass || pName.Contains("road") || pName.Contains("park") ||
                pName.Contains("lotfill") || pName.Contains("beach") || pName.Contains("ground") ||
                pName.Contains("water") || pName.Contains("intersection"))
            {
                shouldCastShadows = false;
            }

            // Group by Material -> sectorKey -> List<CombineInstance>
            // Non-grass objects use sectorKey = 0 (pure material grouping for minimal draw calls)
            Dictionary<Material, Dictionary<long, List<CombineInstance>>> materialGroups = 
                new Dictionary<Material, Dictionary<long, List<CombineInstance>>>();

            foreach (var mf in meshFilters)
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterials.Length == 0 || mf.sharedMesh == null) continue;

                Mesh sharedMesh = mf.sharedMesh;
                Material[] sharedMats = mr.sharedMaterials;
                Matrix4x4 localMatrix = parentObj.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

                long sectorKey = 0;
                if (isGrass)
                {
                    int cellX = Mathf.FloorToInt(localMatrix.m03 / GRASS_SPATIAL_CELL_SIZE);
                    int cellZ = Mathf.FloorToInt(localMatrix.m23 / GRASS_SPATIAL_CELL_SIZE);
                    sectorKey = ((long)cellX << 32) | (uint)cellZ;
                }

                for (int subMeshIndex = 0; subMeshIndex < sharedMesh.subMeshCount; subMeshIndex++)
                {
                    if (subMeshIndex >= sharedMats.Length) break;

                    Material mat = sharedMats[subMeshIndex];
                    if (mat == null) continue;

                    if (!materialGroups.TryGetValue(mat, out var sectorMap))
                    {
                        sectorMap = new Dictionary<long, List<CombineInstance>>();
                        materialGroups[mat] = sectorMap;
                    }

                    if (!sectorMap.TryGetValue(sectorKey, out var list))
                    {
                        list = new List<CombineInstance>();
                        sectorMap[sectorKey] = list;
                    }

                    CombineInstance ci = new CombineInstance();
                    ci.mesh = sharedMesh;
                    ci.subMeshIndex = subMeshIndex;
                    ci.transform = localMatrix;

                    list.Add(ci);
                }
            }

            // Maximum vertices per combined mesh (Unity UInt32 limit allows millions, 500,000 is safe and optimal)
            int maxVerticesPerChunk = 500000;
            int totalCombined = 0;

            foreach (var matKvp in materialGroups)
            {
                Material mat = matKvp.Key;
                string mName = mat.name.ToLower();
                bool matCastShadows = shouldCastShadows;
                if (mName.Contains("grass") || mName.Contains("road") || mName.Contains("park") ||
                    mName.Contains("lotfill") || mName.Contains("beach") || mName.Contains("ground") ||
                    mName.Contains("water"))
                {
                    matCastShadows = false;
                }

                foreach (var sectorKvp in matKvp.Value)
                {
                    long sectorKey = sectorKvp.Key;
                    List<CombineInstance> instances = sectorKvp.Value;

                    List<CombineInstance> currentChunk = new List<CombineInstance>();
                    int vertexCount = 0;
                    int subIndex = 0;

                    for (int i = 0; i < instances.Count; i++)
                    {
                        CombineInstance ci = instances[i];
                        int estVerts = ci.mesh.vertexCount;

                        if (vertexCount + estVerts > maxVerticesPerChunk && currentChunk.Count > 0)
                        {
                            CreateCombinedChunk(parentObj.transform, mat, currentChunk, $"{sectorKey}_{subIndex++}", matCastShadows);
                            totalCombined++;
                            currentChunk.Clear();
                            vertexCount = 0;
                        }

                        currentChunk.Add(ci);
                        vertexCount += estVerts;
                    }

                    if (currentChunk.Count > 0)
                    {
                        CreateCombinedChunk(parentObj.transform, mat, currentChunk, $"{sectorKey}_{subIndex}", matCastShadows);
                        totalCombined++;
                    }
                }
            }

            // Destroy ALL original child GameObjects to leave only the combined chunks
            // This eliminates 100,000+ empty GameObject transform nodes from the hierarchy!
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in parentObj.transform)
            {
                if (!child.name.StartsWith("Chunk_"))
                    childrenToDestroy.Add(child.gameObject);
            }
            foreach (var go in childrenToDestroy)
            {
                GameObject.DestroyImmediate(go);
            }

            Debug.Log($"CityCombiner: Merged {meshFilters.Length} objects of '{parentObj.name}' into {totalCombined} chunks (ShadowCasting: {shouldCastShadows}). Cleaned up all original GameObjects.");
        }

        private static void CreateCombinedChunk(Transform parent, Material mat, List<CombineInstance> combiners, string tag, bool castShadows)
        {
            GameObject chunkObj = new GameObject($"Chunk_{mat.name}_{tag}");
            chunkObj.transform.SetParent(parent, false);
            chunkObj.layer = parent.gameObject.layer; // Inherit layer for camera layer-based culling

            MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
            MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            Mesh newMesh = new Mesh();
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            newMesh.CombineMeshes(combiners.ToArray(), true, true);
            mf.sharedMesh = newMesh;

            mr.shadowCastingMode = castShadows 
                ? UnityEngine.Rendering.ShadowCastingMode.On 
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }
    }
}
