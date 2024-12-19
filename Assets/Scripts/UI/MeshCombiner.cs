using System.Collections.Generic;
using UnityEngine;

public class MeshCombiner : MonoBehaviour
{
    public void CombineMesh()
    {
        Debug.Log("Combining Meshes");

        // ��ȡ�����Ӷ����MeshFilter���
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        Debug.Log("combine:" + meshFilters.Length);
        // ׼��CombineInstance���飬���ںϲ�����
        List<CombineInstance> combine = new List<CombineInstance>();
        List<Material> mats = new List<Material>();

        // ��������MeshFilter��׼���ϲ�����
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
                // �����в�����ӵ��б��У�ȷ��û���ظ�
                foreach (Material mat in mr.sharedMaterials)
                {
                    if (!mats.Contains(mat))
                    {
                        mats.Add(mat);
                    }
                }
                mr.enabled = false; // ����ԭʼMeshRenderer
            }
        }

        // �ϲ�����
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = "CombinedMesh";
        combinedMesh.CombineMeshes(combine.ToArray(), false);

        // ���ϲ����Mesh��ֵ����GameObject��MeshFilter
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

        // ���ϲ����Materials��ֵ����GameObject��MeshRenderer
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

        // �����MeshCollider��Ҳ��Ҫ����
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = combinedMesh;
        }

        // �ϲ�������ԭ��������
        foreach (var mf in meshFilters)
        {
            mf.gameObject.SetActive(false);
        }

        Debug.Log("Mesh Combining Completed");
    }

    public void Undo()
    {
        Debug.Log("Undoing Mesh Combining");

        // �Ƴ��������MeshRenderer��MeshFilter���
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

        // ��ȡ�����Ӷ����MeshFilter���
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);

        Debug.Log("undo:" + meshFilters.Length);
        // ���������������������Ϸ����
        foreach (var mf in meshFilters)
        {
            if (mf.gameObject != gameObject) // ȷ�����Ǹ��������
            {
                mf.gameObject.SetActive(true); // ������Ϸ����
            }
        }

        // �ٴα������������������MeshRenderer���
        foreach (var mf in meshFilters)
        {
            if (mf.gameObject != gameObject) // ȷ�����Ǹ��������
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = true; // ����MeshRenderer���
                }
            }
        }

        Debug.Log("Mesh Combining Undone");
    }
}