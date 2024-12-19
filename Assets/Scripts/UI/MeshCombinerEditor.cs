using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshCombiner))]
public class MeshCombinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ���Ȼ���Ĭ�ϵ�Inspector����
        DrawDefaultInspector();

        // ���һ����ť�������ʱ�����CombineMesh����
        if (GUILayout.Button("Combine Mesh"))
        {
            // ����Ŀ������SimplifyMesh����
            ((MeshCombiner)target).CombineMesh();
        }

        // ���һ����ť�������ʱ�����undo����
        if (GUILayout.Button("Undo Combine Mesh"))
        {
            // ����Ŀ������SimplifyMesh����
            ((MeshCombiner)target).Undo();
        }
    }
}