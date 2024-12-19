using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshCombiner))]
public class MeshCombinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 首先绘制默认的Inspector界面
        DrawDefaultInspector();

        // 添加一个按钮，当点击时会调用CombineMesh方法
        if (GUILayout.Button("Combine Mesh"))
        {
            // 调用目标对象的SimplifyMesh方法
            ((MeshCombiner)target).CombineMesh();
        }

        // 添加一个按钮，当点击时会调用undo方法
        if (GUILayout.Button("Undo Combine Mesh"))
        {
            // 调用目标对象的SimplifyMesh方法
            ((MeshCombiner)target).Undo();
        }
    }
}