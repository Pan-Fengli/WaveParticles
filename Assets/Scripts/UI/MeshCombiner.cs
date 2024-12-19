using System.Collections.Generic;
using UnityEngine;

public class MeshCombiner : MonoBehaviour
{
    public void CombineMesh()
    {
        Debug.Log("Combining Meshes");

        // 获取所有子对象的MeshFilter组件
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        Debug.Log("combine:" + meshFilters.Length);
        // 准备CombineInstance数组，用于合并网格
        List<CombineInstance> combine = new List<CombineInstance>();
        List<Material> mats = new List<Material>();

        // 遍历所有MeshFilter，准备合并数据
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Mesh mesh = mf.sharedMesh;
            if (mesh != null)
            {
                for (int j = 0; j < mesh.subMeshCount; j++)
                {
                    CombineInstance ci = new CombineInstance();
                    ci.mesh = mesh;
                    ci.subMeshIndex = j;
                    ci.transform = mf.transform.localToWorldMatrix;
                    combine.Add(ci);
                }
                // 将所有材质添加到列表中，确保没有重复
                foreach (Material mat in mr.sharedMaterials)
                {
                    if (!mats.Contains(mat))
                    {
                        mats.Add(mat);
                    }
                }
                mr.enabled = false; // 禁用原始MeshRenderer
            }
        }

        // 合并网格
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = "CombinedMesh";
        combinedMesh.CombineMeshes(combine.ToArray(), false);

        // 将合并后的Mesh赋值给父GameObject的MeshFilter
        MeshFilter parentMeshFilter = GetComponent<MeshFilter>();
        if (parentMeshFilter != null)
        {
            parentMeshFilter.mesh = combinedMesh;
        }
        else
        {
            parentMeshFilter = gameObject.AddComponent<MeshFilter>();
            parentMeshFilter.mesh = combinedMesh;
        }

        // 将合并后的Materials赋值给父GameObject的MeshRenderer
        MeshRenderer parentMeshRenderer = GetComponent<MeshRenderer>();
        if (parentMeshRenderer != null)
        {
            parentMeshRenderer.sharedMaterials = mats.ToArray();
        }
        else
        {
            parentMeshRenderer = gameObject.AddComponent<MeshRenderer>();
            parentMeshRenderer.sharedMaterials = mats.ToArray();
        }

        // 如果有MeshCollider，也需要更新
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = combinedMesh;
        }

        // 合并后隐藏原来的物体
        foreach (var mf in meshFilters)
        {
            mf.gameObject.SetActive(false);
        }

        Debug.Log("Mesh Combining Completed");
    }

    public void Undo()
    {
        Debug.Log("Undoing Mesh Combining");

        // 移除父组件的MeshRenderer和MeshFilter组件
        MeshRenderer parentMeshRenderer = GetComponent<MeshRenderer>();
        if (parentMeshRenderer != null)
        {
            DestroyImmediate(parentMeshRenderer);
        }
        MeshFilter parentMeshFilter = GetComponent<MeshFilter>();
        if (parentMeshFilter != null)
        {
            DestroyImmediate(parentMeshFilter);
        }

        // 获取所有子对象的MeshFilter组件
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);

        Debug.Log("undo:" + meshFilters.Length);
        // 遍历所有子组件，激活游戏对象
        foreach (var mf in meshFilters)
        {
            if (mf.gameObject != gameObject) // 确保不是父组件本身
            {
                mf.gameObject.SetActive(true); // 激活游戏对象
            }
        }

        // 再次遍历所有子组件，激活MeshRenderer组件
        foreach (var mf in meshFilters)
        {
            if (mf.gameObject != gameObject) // 确保不是父组件本身
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = true; // 启用MeshRenderer组件
                }
            }
        }

        Debug.Log("Mesh Combining Undone");
    }
}