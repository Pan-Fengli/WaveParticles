using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MeshSave : MonoBehaviour
{
    public string mesh_name;
#if UNITY_EDITOR
    public void SaveAsset()
    {
        Debug.Log("开始提取mesh");
        try
        {
            Mesh mesh = this.GetComponent<MeshFilter>().mesh;
            if (mesh != null)
            {
                //AssetDatabase.CreateAsset(mesh, "Assets/Models/提取_" + name + "63.asset");
                AssetDatabase.CreateAsset(mesh, "Assets/Models/提取_" + mesh_name + ".asset");
                Debug.Log("提取mesh成功：提取_" + name);
            }
            else
                Debug.LogWarning("提取mesh失败：无MeshFilter组件");
        }
        catch (Exception e)
        {
            Debug.LogWarning("提取mesh失败：" + e.ToString());
        }
    }
#endif
}

