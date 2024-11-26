using UnityEngine;
using Unity.Mathematics;
using UnityEditor;

namespace OneBitLab.Services
{
    public class ResourceLocatorService : SingletonBase<ResourceLocatorService>
    {
        //-------------------------------------------------------------
        public RenderTexture m_HeightFieldRT;
        public Camera        m_MainCam;
        public int N=40;
        public int M=40;
        public int L=10;
        [UnityEngine.Tooltip("Local Center of Mass")]
        public Vector3 COM = new Vector3(0, 0, 0);
        public Mesh simulationMesh;
        [ReadOnly]
        public Vector3 WorldCOM = new Vector3(0, 0, 0);
        
        //public Vector3 inertiaTensor = new Vector3(0, 0, 0);
        //public Quaternion intertiaRotation;

        //船舶风载荷系数相关
        public float LOA = 4.91f;//船长
        public float B = 2.06f;//船宽
        public float AF = 1.5346f;//正面投影面积
        public float AL = 2.6183f;//侧面投影面积
        public float C = 0.5445f;//侧面投影型心到船舶中心的距离
        public float HC = 0.3174f;//侧面投影型心到水线的距离
        public float AOD = 0;//甲板上物体的侧投影面积——测不出来，默认是0
        public float HBR = 0.987f;//最上层建筑物到水面的距离——测不出来，默认是船舶高度

        public bool applyWind = false;
        //-------------------------------------------------------------
        [ReadOnly]
        public Vector3 WaterForce = new Vector3(0, 0, 0);
        [ReadOnly]
        public Vector3 WindForce = new Vector3(0, 0, 0);
        [ReadOnly]
        public Vector3 ResultForce = new Vector3(0, 0, 0);

        [ReadOnly]
        public float XArea = 0.0f;
        [ReadOnly]
        public float YArea = 0.0f;
    }
}


public class ReadOnlyAttribute : PropertyAttribute
{

}


[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
