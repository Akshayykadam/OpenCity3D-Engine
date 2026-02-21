using UnityEngine;
using System.Collections.Generic;

namespace GeoCity3D.Visuals
{
    public static class CityCombiner
    {
        /// <summary>
        /// Groups all child meshes of a parent object by material and combines them.
        /// Dramatically reduces draw calls for mass-instantiated Prefab buildings.
        /// </summary>
        public static void CombineMeshesByMaterial(GameObject parentObj)
        {
            if (parentObj == null) return;

            MeshFilter[] meshFilters = parentObj.GetComponentsInChildren<MeshFilter>(false);
            if (meshFilters.Length == 0) return;

            // 1. Group all mesh COMBINERS by their exact shared material across all submeshes
            Dictionary<Material, List<CombineInstance>> materialGroups = new Dictionary<Material, List<CombineInstance>>();

            foreach (var mf in meshFilters)
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterials.Length == 0 || mf.sharedMesh == null) continue;

                Mesh sharedMesh = mf.sharedMesh;
                Material[] sharedMats = mr.sharedMaterials;

                for (int subMeshIndex = 0; subMeshIndex < sharedMesh.subMeshCount; subMeshIndex++)
                {
                    // Safety check if renderer has fewer materials than the mesh has submeshes
                    if (subMeshIndex >= sharedMats.Length) break;
                    
                    Material mat = sharedMats[subMeshIndex];
                    if (mat == null) continue;

                    if (!materialGroups.ContainsKey(mat))
                        materialGroups[mat] = new List<CombineInstance>();

                    CombineInstance ci = new CombineInstance();
                    ci.mesh = sharedMesh;
                    ci.subMeshIndex = subMeshIndex; // Explicitly grab only this submesh
                    ci.transform = parentObj.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    
                    materialGroups[mat].Add(ci);
                }

                // Destroy the original mesh renderer and filter so they don't clog up 
                // Unity's internal Static Batching arrays later in the generation process.
                GameObject.DestroyImmediate(mr);
                GameObject.DestroyImmediate(mf);
            }

            // 2. For each material group, combine meshes together
            int totalCombined = 0;
            foreach (var kvp in materialGroups)
            {
                Material mat = kvp.Key;
                List<CombineInstance> instances = kvp.Value;

                // We use 32-bit indices allowing up to ~4 billion vertices, but Unity often struggles
                // rendering single meshes larger than 500k vertices. We will batch responsibly.
                int maxVerticesPerChunk = 500000; // Safer threshold for single mesh
                
                List<CombineInstance> currentChunk = new List<CombineInstance>();
                int vertexCount = 0;

                for (int i = 0; i < instances.Count; i++)
                {
                    CombineInstance ci = instances[i];
                    
                    // We realistically estimate using the whole mesh vertex count per CombineInstance
                    int estVerts = ci.mesh.vertexCount; 

                    if (vertexCount + estVerts > maxVerticesPerChunk && currentChunk.Count > 0)
                    {
                        CreateCombinedChunk(parentObj.transform, mat, currentChunk, totalCombined++);
                        currentChunk.Clear();
                        vertexCount = 0;
                    }

                    currentChunk.Add(ci);
                    vertexCount += estVerts;
                }

                // Combine remainder
                if (currentChunk.Count > 0)
                {
                    CreateCombinedChunk(parentObj.transform, mat, currentChunk, totalCombined++);
                }
            }

            Debug.Log($"CityCombiner: Merged {meshFilters.Length} individual meshes into {totalCombined} batched chunks.");
        }

        private static void CreateCombinedChunk(Transform parent, Material mat, List<CombineInstance> combiners, int index)
        {
            GameObject chunkObj = new GameObject($"CombinedChunk_{mat.name}_{index}");
            chunkObj.transform.SetParent(parent, false);

            MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
            MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            Mesh newMesh = new Mesh();
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Allow large massive meshes
            newMesh.CombineMeshes(combiners.ToArray(), true, true);
            mf.sharedMesh = newMesh;

            // Disable shadow casting on buildings by default if needed, or leave it On for URP
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
    }
}
