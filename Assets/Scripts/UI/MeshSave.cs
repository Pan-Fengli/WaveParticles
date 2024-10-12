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
        Debug.Log("��ʼ��ȡmesh");
        try
        {
            Mesh mesh = this.GetComponent<MeshFilter>().mesh;
            if (mesh != null)
            {
                //AssetDatabase.CreateAsset(mesh, "Assets/Models/��ȡ_" + name + "63.asset");
                AssetDatabase.CreateAsset(mesh, "Assets/Models/��ȡ_" + mesh_name + ".asset");
                Debug.Log("��ȡmesh�ɹ�����ȡ_" + name);
            }
            else
                Debug.LogWarning("��ȡmeshʧ�ܣ���MeshFilter���");
        }
        catch (Exception e)
        {
            Debug.LogWarning("��ȡmeshʧ�ܣ�" + e.ToString());
        }
    }
#endif
}

