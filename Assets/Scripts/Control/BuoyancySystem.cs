
using OneBitLab.FluidSim;
using OneBitLab.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Extensions;
using UnityEditor;
using System.IO;

[UpdateBefore(typeof(WaveSubdivideSystem))]
public class BuoyancySystem : SystemBase
{
    private RenderTexture m_HeightFieldRT;
    private Texture2D m_HeightFieldTex;//两者的区别在于，前者只有RFloat一个通道，后者有四个
    private Texture2D m_HeightFieldTex32;
    NativeArray<float> pixData;
    Mesh waterMesh;
    Matrix4x4 waterMatrix;

    // Vertices used for simulation, in local space.
    public Vector3[] LocalVertices;
    // Triangles indices of the simulation mesh.
    public int[] TriIndices;
    public int vertexCount;
    public int triangleCount;

    public float finalForceCoefficient = 1.0f;//Coefficient by which the result force will get multiplied.
    public float finalTorqueCoefficient = 1.0f;//Coefficient by which the torque force will get multiplied.
    public float defaultWaterHeight = 0.0f;//World water height to be used when there is no WaterDataProvider present.
    public Vector3 defaultWaterNormal = Vector3.up;//World water normal to be used when there is no WaterDataProvider present and calculateWaterNormals is enabled.
    public Vector3 defaultWaterFlow = Vector3.zero;//World water flow to be used when there is no WaterDataProvider present and calculateWaterFlows is enabled.
    public bool calculateWaterHeights = true;
    public bool calculateWaterNormals = true;
    public bool calculateWaterFlows = false;
    public float fluidDensity = 1030.0f;
    public float airDensity = 1.29f;
    //public float fluidDensity = 1.03f;
    public float buoyantForceCoefficient = 1.0f;// Coefficient by which the the buoyancy forces are multiplied.
    public float slamForceCoefficient = 1.0f;//Coefficient applied to forces when a face of the object is entering the water.
    public float suctionForceCoefficient = 1.0f;//Coefficient applied to forces when a face of the object is leaving the water.
    public float hydrodynamicForceCoefficient = 1.0f;//Coefficient by which the hydrodynamic forces are multiplied..
    /// <summary>
    /// Determines to which power the dot product between velocity and triangle normal will be raised.
    /// Higher values will result in lower hydrodynamic forces for situations in which the triangle is nearly parallel to the
    /// water velocity. 因为dot的值是小于等于1的，所以这里的power越大，最后阻力就越小
    /// </summary>
    public float velocityDotPower = 1f;
    public float skinDragCoefficient = 0.1f;
/*    public bool convexifyMesh = true;
    public bool simplifyMesh = true;
    public bool weldColocatedVertices = true;
    public int targetTriangleCount = 64;
    public int instanceID;
    public Mesh originalMesh;
    private Mesh simulationMesh;*/


    private Vector3[] WorldVertices;//Simulation vertices coverted to world coordinates.
    private float[] WaterHeights;//Water surface heights of the current water data provider. Default is used if no water data provider is present.
    private Vector3[] WaterNormals;//Water normals of the current water data provider. Default is used if no water data provider is present.
    private Vector3[] WaterFlows;//Water flows of the current water data provider. Default is used if no water data provider is present.
    public Vector3 RigidbodyAngVel = new Vector3(0, 0, 0);//Angular velocity of the target Rigidbody.
    public Vector3 RigidbodyCoM = new Vector3(0, 0, 0);//Center of mass of the target Rigidbody.
    public Vector3 RigidbodyLinearVel = new Vector3(0, 0, 0);//(Linear) velocity of the target Rigidbody.
    private quaternion RigidbodyRotation;
    private Vector3 RigidbodyXDir = new Vector3(0, 0, 0);
    private Vector3 RigidbodyYDir = new Vector3(0, 0, 0);
    private Vector3[] ResultCenters;//Result triangle centers in world coordinates.
    private float[] ResultAreas;//Result triangle areas.
    private float[] ResultDistances;//Result distances to water surface.（转化成distanceToSurface）
    public Vector3 ResultForce;//Total result force.
    public Vector3[] ResultForces;//Per-triangle result forces.
    public Vector3 WindForce;//Total wind force.
    public Vector3[] WindForces;//Per-triangle wind forces.
    public Vector3[] ResultNormals;// Result triangle normals. Not equal to the mesh normals.
    public Vector3[] ResultP0s;//Result sliced triangle vertices.（都在左边，实际上并没有用上）
    /// <summary>
    /// Result triangle states.
    ///     0 - Triangle is under water
    ///     1 - Triangle is partially under water
    ///     2 - Triangle is above water
    ///     3 - Triangle's object is disabled
    ///     4 - Triangle's object is deleted
    /// </summary>
    public int[] ResultStates;
    public Vector3 ResultTorque;//Result total torque acting on the Rigidbody.
    public Vector3[] ResultVelocities;//Result velocities at triangle centers.
    public float submergedVolume;

    private Vector3 _gravity = new Vector3(0, -9.81f, 0);
    private Vector3 GravityForce;
    private Vector3 _worldUpVector = new Vector3(0.0f, 1.0f, 0.0f);//重力的反方向
    private Matrix4x4 _localToWorldMatrix;
    private Vector3 _f0, _f1;
    private Vector3 _c0, _c1;
    private float _a0, _a1;
    private float _dst0, _dst1;

    private float time;
    private float TotalTime;

    private bool applyWind = false;
    private float XArea = 0.0f;
    private float YArea = 0.0f;
    private float FX = 0.0f;
    private float FY = 0.0f;
    private float FXOld = 0.0f;
    private float FYOld = 0.0f;
    private float Phi = 0.0f;

    private float constantWind = 0.0f;
    //-------------------------------------------------------------
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        m_HeightFieldRT = ResourceLocatorService.Instance.m_HeightFieldRT;

        m_HeightFieldTex = new Texture2D(m_HeightFieldRT.width,
                                          m_HeightFieldRT.height,
                                          TextureFormat.RFloat,
                                          mipChain: false,
                                          linear: true);
        m_HeightFieldTex32 = new Texture2D(m_HeightFieldRT.width,
                                          m_HeightFieldRT.height,
                                          TextureFormat.RGBAFloat,
                                          mipChain: false,
                                          linear: true);

        _worldUpVector = Vector3.Normalize(-Physics.gravity);

        Entities.WithAll<Tag_Player>().ForEach((
            ref Translation translation, 
            ref Rotation rotation, 
            ref Unity.Physics.PhysicsMass physicsMass) =>
        {
            Debug.Log("Tag_Player");
            //mesh改成resource locator的mesh
            Mesh mesh = ResourceLocatorService.Instance.simulationMesh;
            LocalVertices = mesh.vertices;
            TriIndices = mesh.triangles;
            vertexCount = LocalVertices.Length;
            Debug.Log("Vertex" + vertexCount);
            triangleCount = TriIndices.Length / 3;
            /*for (int i = 0; i < LocalVertices.Length; i++)
            {
                Vector3 vertex = LocalVertices[i];
                Debug.Log("Vertex " + i + ": " + vertex);
                // 在这里可以对顶点进行处理
            }*/

            //转动惯量可以初始化,不会发生变化的
            /*
            Debug.Log("physicsMass.InverseInertia:" + physicsMass.InverseInertia);
            Debug.Log("physicsMass.InertiaOrientation:" + physicsMass.InertiaOrientation);
            physicsMass.InertiaOrientation = ResourceLocatorService.Instance.intertiaRotation;
            Vector3 inertia= ResourceLocatorService.Instance.inertiaTensor;
            physicsMass.InverseInertia = new Vector3(1.0f/ inertia.x, 1.0f / inertia.y, 1.0f / inertia.z);
            Debug.Log("new InverseInertia:" + physicsMass.InverseInertia);
            Debug.Log("new InertiaOrientation:" + physicsMass.InertiaOrientation);
            */

            WorldVertices = new Vector3[vertexCount];
            WaterHeights = new float[vertexCount];
            WaterNormals = new Vector3[vertexCount];
            WaterFlows = new Vector3[vertexCount];
            ResultStates = new int[triangleCount];
            ResultVelocities = new Vector3[triangleCount];
            ResultP0s = new Vector3[triangleCount * 6];
            ResultForces = new Vector3[triangleCount];
            WindForces = new Vector3[triangleCount];
            ResultCenters = new Vector3[triangleCount];
            ResultNormals = new Vector3[triangleCount];
            ResultAreas = new float[triangleCount];
            ResultDistances = new float[triangleCount];

            // Fill in default data
            for (int i = 0; i < vertexCount; i++)
            {
                WaterHeights[i] = defaultWaterHeight;
                WaterNormals[i] = defaultWaterNormal;
                WaterFlows[i] = defaultWaterFlow;
            }

            for (int i = 0; i < triangleCount; i++)
            {
                ResultStates[i] = 2;//默认都在水上的状态
            }
        })
        .WithoutBurst()
        .Run();

        LOA = ResourceLocatorService.Instance.LOA;//船长
        B = ResourceLocatorService.Instance.B;//船宽
        AF = ResourceLocatorService.Instance.AF;//正面投影面积
        AL = ResourceLocatorService.Instance.AL;//侧面投影面积
        C = ResourceLocatorService.Instance.C;//侧面投影型心到船舶中心的距离
        HC = ResourceLocatorService.Instance.HC;//侧面投影型心到水线的距离
        AOD = ResourceLocatorService.Instance.AOD;//甲板上物体的侧投影面积——测不出来，默认是0
        HBR = ResourceLocatorService.Instance.HBR;//最上层建筑物到水面的距离——测不出来，默认是船舶高度

        applyWind = ResourceLocatorService.Instance.applyWind;

        constantWind = SpectrumService.Instance.windSpeed;

        CLF1 = b10 + b11 * AL / (LOA * B) + b12 * C / LOA;
        CLF2 = b20 + b21 * B / LOA + b22 * HC / LOA + b23 * AOD / (LOA * LOA) + b24 * AF / (B * B);

        CYM1 = gama10 + gama11 * AF / (LOA * B);
        CYM2 = gama20 + gama21 * AOD / (LOA * LOA);

        //Debug:把0~180°的风载荷的结果输出
        StreamWriter sw1 = new StreamWriter(@"D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat\风载荷系数CX.txt");
        StreamWriter sw2 = new StreamWriter(@"D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat\风载荷系数CY.txt");
        StreamWriter sw3 = new StreamWriter(@"D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat\风载荷系数CN.txt");
        StreamWriter sw4 = new StreamWriter(@"D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat\风载荷系数CK.txt");
        StreamWriter sw5 = new StreamWriter(@"D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat\UZ.txt");

        for (int i = 0; i <= 180; i++)
        {
            float phi = (float)i / 180.0f * math.PI;
            float cx = GetCx(phi);
            float cy = GetCy(phi);
            float cN = GetCn(phi);
            float cK = GetCk(phi);
            
            sw1.Write(cx);
            sw1.Write("\n");
            sw2.Write(cy);
            sw2.Write("\n");
            sw3.Write(cN);
            sw3.Write("\n");
            sw4.Write(cK);
            sw4.Write("\n");
            
        }
        sw1.Close();
        sw2.Close();
        sw3.Close();
        sw4.Close();

        for (int i = 0; i <= 100; ++i)
        {
            Vector3 pos = new Vector3(0.0f, i * 0.2f, 0.0f);
            Vector3 UZ = GetWindAtPosition(pos);
            sw5.Write(UZ.x);
            sw5.Write("\n");
        }
        sw5.Close();
    }

    protected override void OnUpdate()
    {
        // 模拟的环境参数，例如重力和水密度
/*        float gravity = 9.81f; // 地球重力加速度，单位m/s^2
        float waterDensity = 1.000f; // 水的密度，单位kg/m^3
        float Volume = 1;
        float mass = 20;*/

        time = Time.DeltaTime;
        Entities.WithAll<Tag_Water>().ForEach((in RenderMesh renderMesh, in LocalToWorld localToWorld) =>
        {
            waterMesh = renderMesh.mesh;
            waterMatrix = localToWorld.Value;
            // 确保Mesh数据可用
            if (waterMesh == null)
            {
                Debug.Log("waterMesh is empty");
                return;
            }
            /*Matrix4x4 matrix= localToWorld.Value;
            Vector3 localPos = new Vector3(1.0f, 1.0f, 0.5f);
            Vector3 WorldPos = matrix.MultiplyPoint(localPos);*/
            //Debug.Log("WorldPos" + WorldPos);//变换前后是一样的,也就是说明了，读取出来的water纹理高度就是实际的高度，不用转到世界坐标系下
        })
        .WithoutBurst()
        .Run();

        XArea = 0;
        YArea = 0;
        FX = 0;
        FY = 0;
        FXOld = 0;
        FYOld = 0;
        Phi = 0;
        Entities.WithAll<Tag_Player>().ForEach((
            ref Translation translation, 
            ref Rotation rotation, 
            ref Unity.Physics.PhysicsVelocity velocity, 
            ref Unity.Physics.PhysicsMass physicsMass,
            //in Unity.Physics.MeshCollider mesh, 
            in LocalToWorld localToWorld
            ) =>
        {
            // 在这里等待之前的作业完成
            Dependency.Complete();

            float3 position = translation.Value;

            // 从RenderTexture中读取高度值
            RenderTexture.active = m_HeightFieldRT;
            /*m_HeightFieldTex.ReadPixels(new Rect(0, 0, m_HeightFieldRT.width, m_HeightFieldRT.height), 0, 0);
            m_HeightFieldTex.Apply();*/
            m_HeightFieldTex32.ReadPixels(new Rect(0, 0, m_HeightFieldRT.width, m_HeightFieldRT.height), 0, 0);
            m_HeightFieldTex32.Apply();
            //pixData = m_HeightFieldTex.GetRawTextureData<float>();
            Color color = m_HeightFieldTex32.GetPixel(m_HeightFieldRT.width / 2, m_HeightFieldRT.height / 2);

            //Debug.Log("color:" + color);
            //Debug.Log("pixData:" + pixData[m_HeightFieldRT.width / 2 + m_HeightFieldRT.width * m_HeightFieldRT.height / 2]);
            /*            Debug.Log("translation:" + translation.Value);
                        Debug.Log("physicsMass.Transform.pos:" + physicsMass.Transform.pos);
            */

            //重心坐标转化到世界坐标
            Vector3 localCOM = ResourceLocatorService.Instance.COM;// physicsMass.CenterOfMass+ 加上一个offset，换成从resource读取的  ResourceLocatorService.Instance.COM
            //physicsMass.CenterOfMass = localCOM;
            _localToWorldMatrix = localToWorld.Value;
            Vector3 COM = _localToWorldMatrix.MultiplyPoint(localCOM);
            
            /*Debug.Log("localCOM:" + localCOM);
            Debug.Log("ResourceLocatorService.Instance.COM:" + ResourceLocatorService.Instance.COM);
            Debug.Log("COM:" + COM.x + "," + COM.y + "," + COM.z);
            Debug.Log("translation:" + translation.Value);*/

            ResourceLocatorService.Instance.WorldCOM = COM;

            //关于rotation的计算
            RigidbodyRotation = rotation.Value;
            Matrix4x4 rot = new Matrix4x4();
            rot.SetTRS(new Vector3(0, 0, 0), RigidbodyRotation, new Vector3(1, 1, 1));
            RigidbodyXDir = new Vector3(rot[0, 0], rot[1, 0], rot[2, 0]);
            RigidbodyYDir = new Vector3(rot[0, 1], rot[1, 1], rot[2, 1]);
            //Debug.Log("RigidbodyXDir:" + RigidbodyXDir);
            //Debug.Log("RigidbodyYDir:" + RigidbodyYDir);

            TickWaterObject(
                //new Vector3(translation.Value.x, translation.Value.y, translation.Value.z),//重心位置，暂时就是translation的中心位置
                //physicsMass.CenterOfMass,
                COM,
                //rigidBody.velocity,
                velocity.Linear,
                //rigidBody.angularVelocity,
                velocity.Angular,
                //transform.localToWorldMatrix,//
                localToWorld.Value,
                Physics.gravity
            );
            //Debug.Log("XArea:" + XArea);//迎风的，纵向的受风面积
            //Debug.Log("YArea:" + YArea);//横向受风面积
            ResourceLocatorService.Instance.XArea = XArea;
            ResourceLocatorService.Instance.YArea = YArea;
            ResourceLocatorService.Instance.FX = FX;
            ResourceLocatorService.Instance.FY = FY;
            ResourceLocatorService.Instance.FXOld = FXOld;
            ResourceLocatorService.Instance.FYOld = FYOld;
            ResourceLocatorService.Instance.Phi = Phi;

            //计算重力
            GravityForce = Physics.gravity / physicsMass.InverseMass;

            // Apply force and torque
            // 应用力矩和力到速度和角速度
            //rigidBody.AddForce(ResultForce);
            //rigidBody.AddTorque(ResultTorque);

            //Debug检查一下physicsMass是否有我们所需的全部属性，比如转动惯量。
            /*Debug.Log("physicsMass.InverseMass:"+ physicsMass.InverseMass);
            Debug.Log("physicsMass.CenterOfMass:" + physicsMass.CenterOfMass);
            Debug.Log("physicsMass.InverseInertia:" + physicsMass.InverseInertia);
            Debug.Log("physicsMass.Transform:" + physicsMass.Transform);*/
            //Debug.Log("ResultForce:" + ResultForce.y);
            //Debug.Log("ResultTorque" + ResultTorque);
            //Debug.Log("GravityForce" + GravityForce);
/*            if (time > 0.03)
            {
                Debug.Log("time:" + time);
            }*/
            
            velocity.ApplyLinearImpulse(physicsMass, ResultForce * time);//冲量
            //补充加上重力的冲量
            velocity.ApplyLinearImpulse(physicsMass, GravityForce * time);
            velocity.ApplyAngularImpulse(physicsMass, ResultTorque * time);
            
        })
            .WithoutBurst()
            .Run();
        //.ScheduleParallel();

    }
    private void TickWaterObject(Vector3 rigidbodyCoM, Vector3 rigidbodyLinVel, Vector3 rigidbodyAngVel, Matrix4x4 l2wMatrix, Vector3 gravity)
    {
        RigidbodyLinearVel = rigidbodyLinVel;
        RigidbodyAngVel = rigidbodyAngVel;
        RigidbodyCoM = rigidbodyCoM;
        Vector3 tmpRigidbodyCoM = rigidbodyCoM;     //备份中心点
        _localToWorldMatrix = l2wMatrix;
        _gravity = gravity;

        for (int i = 0; i < vertexCount; i++)
        {
            WorldVertices[i] = _localToWorldMatrix.MultiplyPoint(LocalVertices[i]);
        }

        //Debug.Log("Vertex " + 0 + ": " + LocalVertices[0]);
        //Debug.Log("Vertex " + 0 + ": " + WorldVertices[0]);
        //Debug.Log("triangleCount " + 0 + ": " + triangleCount);           832个

        //TODO: 顶点坐标已经变换，可以更新WaterHeights[]（也可以就保持现在这样，用一个函数去调用即可）

        for (int i = 0; i < triangleCount; i++)
        {
            CalcTri(i);
        }

        // Calculate result force and torque
        Vector3 forceSum;
        forceSum.x = 0;
        forceSum.y = 0;
        forceSum.z = 0;

        Vector3 torqueSum;
        torqueSum.x = 0;
        torqueSum.y = 0;
        torqueSum.z = 0;

        Vector3 windForceSum;
        windForceSum.x = 0;
        windForceSum.y = 0;
        windForceSum.z = 0;

        Vector3 windTorqueSum;
        windTorqueSum.x = 0;
        windTorqueSum.y = 0;
        windTorqueSum.z = 0;

        Vector3 resultForce;
        Vector3 worldCoM = tmpRigidbodyCoM;//世界坐标下的质心位置
        for (int i = 0; i < triangleCount; i++)
        {
            if (ResultStates[i] < 2)
            {
                resultForce = ResultForces[i];

                forceSum.x += resultForce.x;
                forceSum.y += resultForce.y;
                forceSum.z += resultForce.z;

                Vector3 resultCenter = ResultCenters[i];

                Vector3 dir;//作用点到质心的距离用来算力矩
                dir.x = resultCenter.x - worldCoM.x;
                dir.y = resultCenter.y - worldCoM.y;
                dir.z = resultCenter.z - worldCoM.z;

                Vector3 rf = ResultForces[i];
                Vector3 crossDirForce;
                crossDirForce.x = dir.y * rf.z - dir.z * rf.y;
                crossDirForce.y = dir.z * rf.x - dir.x * rf.z;
                crossDirForce.z = dir.x * rf.y - dir.y * rf.x;

                torqueSum.x += crossDirForce.x;
                torqueSum.y += crossDirForce.y;
                torqueSum.z += crossDirForce.z;
            }
            if (ResultStates[i] == 2)
            {
                //TODO:计算水面上的面片受到的风力合力&力矩
                resultForce = WindForces[i];

                windForceSum.x += resultForce.x;
                windForceSum.y += resultForce.y;
                windForceSum.z += resultForce.z;

                Vector3 resultCenter = ResultCenters[i];

                Vector3 dir;//作用点到质心的距离用来算力矩
                dir.x = resultCenter.x - worldCoM.x;
                dir.y = resultCenter.y - worldCoM.y;
                dir.z = resultCenter.z - worldCoM.z;

                Vector3 rf = WindForces[i];
                Vector3 crossDirForce;
                crossDirForce.x = dir.y * rf.z - dir.z * rf.y;
                crossDirForce.y = dir.z * rf.x - dir.x * rf.z;
                crossDirForce.z = dir.x * rf.y - dir.y * rf.x;

                windTorqueSum.x += crossDirForce.x;
                windTorqueSum.y += crossDirForce.y;
                windTorqueSum.z += crossDirForce.z;
            }

        }
        /*老方法算力和力矩
        Vector3 shipWindSpeed = GetWindAtPosition(new Vector3(worldCoM.x, 0.534f, worldCoM.z));
        Vector3 oldWindForce=new Vector3();
        Vector3 oldWindTorque = new Vector3();
        CalculateOriginalWindForce(shipWindSpeed, ref oldWindForce, ref oldWindTorque);
        ResourceLocatorService.Instance.oldWindForce = oldWindForce;
        ResourceLocatorService.Instance.oldWindTorque = oldWindTorque;
        */

        ResultForce.x = forceSum.x * finalForceCoefficient;
        ResultForce.y = forceSum.y * finalForceCoefficient;
        ResultForce.z = forceSum.z * finalForceCoefficient;

        ResourceLocatorService.Instance.WaterForce = ResultForce;
        if (applyWind)
        {
            ResultForce += windForceSum;
            //ResultForce += oldWindForce;
        }

        ResourceLocatorService.Instance.WindForce = windForceSum;
        ResourceLocatorService.Instance.ResultForce = ResultForce;
        //Debug.Log("windForceSum"+ windForceSum);
        //Debug.Log("ResultForce" + ResultForce);

        ResultTorque.x = torqueSum.x * finalTorqueCoefficient;
        ResultTorque.y = torqueSum.y * finalTorqueCoefficient;
        ResultTorque.z = torqueSum.z * finalTorqueCoefficient;

        if (applyWind)
        {
            ResultTorque += windTorqueSum;
            //ResultTorque += oldWindTorque;
        }
        ResourceLocatorService.Instance.windTorqueSum = windTorqueSum;
        //Debug.Log("windTorqueSum" + windTorqueSum);
        //Debug.Log("ResultTorque" + ResultTorque);
    }

    private void CalcTri(int i)
    {
        if (ResultStates[i] >= 3)
        {
            return;
        }

        int baseIndex = i * 3;
        int vertIndex0 = TriIndices[baseIndex];
        int vertIndex1 = TriIndices[baseIndex + 1];
        int vertIndex2 = TriIndices[baseIndex + 2];

        Vector3 P0 = WorldVertices[vertIndex0];
        Vector3 P1 = WorldVertices[vertIndex1];
        Vector3 P2 = WorldVertices[vertIndex2];

        //float wh_P0 = WaterHeights[vertIndex0];
        //float wh_P1 = WaterHeights[vertIndex1];
        //float wh_P2 = WaterHeights[vertIndex2];
        float wh_P0 = GetWaterHeightAtPosition(new float3(P0.x, P0.y, P0.z));
        float wh_P1 = GetWaterHeightAtPosition(new float3(P1.x, P1.y, P1.z));
        float wh_P2 = GetWaterHeightAtPosition(new float3(P2.x, P2.y, P2.z));

        float d0 = P0.y - wh_P0;
        float d1 = P1.y - wh_P1;
        float d2 = P2.y - wh_P2;


        //All vertices are above water//三个点都在水上
        if (d0 >= 0 && d1 >= 0 && d2 >= 0)
        {
            ResultStates[i] = 2;
            //TODO,计算风力
            //获取当前位置的风向风速，传入进去
            Vector3 Center=(P0 + P1 + P2)/ 3.0f;
            Center.y = (d0 + d1 + d2) / 3.0f;
            Vector3 windSpeed = GetWindAtPosition(Center);
            CalculateWindForces(P0, P1, P2,i,
            windSpeed,
            ref WindForces[i], ref ResultCenters[i],
            ref ResultAreas[i]);
            return;
        }

        // All vertices are underwater
        if (d0 <= 0 && d1 <= 0 && d2 <= 0)
        {
            ThreeUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
        }
        // 1 or 2 vertices are below the water
        else
        {
            // v0 > v1
            if (d0 > d1)
            {
                // v0 > v2
                if (d0 > d2)
                {
                    // v1 > v2
                    if (d1 > d2)
                    {
                        if (d0 > 0 && d1 < 0 && d2 < 0)
                        {
                            // 0 1 2
                            TwoUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
                        }
                        else if (d0 > 0 && d1 > 0 && d2 < 0)
                        {
                            // 0 1 2
                            OneUnderWater(P0, P1, P2, d0, d1, d2, 0, 1, 2, i);
                        }
                    }
                    // v2 > v1
                    else
                    {
                        if (d0 > 0 && d2 < 0 && d1 < 0)
                        {
                            // 0 2 1
                            TwoUnderWater(P0, P2, P1, d0, d2, d1, 0, 2, 1, i);
                        }
                        else if (d0 > 0 && d2 > 0 && d1 < 0)
                        {
                            // 0 2 1
                            OneUnderWater(P0, P2, P1, d0, d2, d1, 0, 2, 1, i);
                        }
                    }
                }
                // v2 > v0
                else
                {
                    if (d2 > 0 && d0 < 0 && d1 < 0)
                    {
                        // 2 0 1
                        TwoUnderWater(P2, P0, P1, d2, d0, d1, 2, 0, 1, i);
                    }
                    else if (d2 > 0 && d0 > 0 && d1 < 0)
                    {
                        // 2 0 1
                        OneUnderWater(P2, P0, P1, d2, d0, d1, 2, 0, 1, i);
                    }
                }
            }
            // v0 < v1
            else
            {
                // v0 < v2
                if (d0 < d2)
                {
                    // v1 < v2
                    if (d1 < d2)
                    {
                        if (d2 > 0 && d1 < 0 && d0 < 0)
                        {
                            // 2 1 0
                            TwoUnderWater(P2, P1, P0, d2, d1, d0, 2, 1, 0, i);
                        }
                        else if (d2 > 0 && d1 > 0 && d0 < 0)
                        {
                            // 2 1 0
                            OneUnderWater(P2, P1, P0, d2, d1, d0, 2, 1, 0, i);
                        }
                    }
                    // v2 < v1
                    else
                    {
                        if (d1 > 0 && d2 < 0 && d0 < 0)
                        {
                            // 1 2 0
                            TwoUnderWater(P1, P2, P0, d1, d2, d0, 1, 2, 0, i);
                        }
                        else if (d1 > 0 && d2 > 0 && d0 < 0)
                        {
                            // 1 2 0
                            OneUnderWater(P1, P2, P0, d1, d2, d0, 1, 2, 0, i);
                        }
                    };
                }
                // v2 < v0
                else
                {
                    if (d1 > 0 && d0 < 0 && d2 < 0)
                    {
                        // 1 0 2
                        TwoUnderWater(P1, P0, P2, d1, d0, d2, 1, 0, 2, i);
                    }
                    else if (d1 > 0 && d0 > 0 && d2 < 0)
                    {
                        // 1 0 2
                        OneUnderWater(P1, P0, P2, d1, d0, d2, 1, 0, 2, i);
                    }
                }
            }
        }
    }

    private void CalculateWindForces(in Vector3 p0, in Vector3 p1, in Vector3 p2,in int index,
        in Vector3 windSpeed,
        ref Vector3 windForce, ref Vector3 center, ref float area)
    {
        windForce.x = 0;
        windForce.y = 0;
        windForce.z = 0;

        //计算重心
        center.x = (p0.x + p1.x + p2.x) * 0.3333333333f;
        center.y = (p0.y + p1.y + p2.y) * 0.3333333333f;
        center.z = (p0.z + p1.z + p2.z) * 0.3333333333f;

        area = 0;

        Vector3 u;
        u.x = p1.x - p0.x;
        u.y = p1.y - p0.y;
        u.z = p1.z - p0.z;

        Vector3 v;
        v.x = p2.x - p0.x;
        v.y = p2.y - p0.y;
        v.z = p2.z - p0.z;

        Vector3 crossUV;//向内的法向
        crossUV.x = u.y * v.z - u.z * v.y;
        crossUV.y = u.z * v.x - u.x * v.z;
        crossUV.z = u.x * v.y - u.y * v.x;

        float crossMagnitude = crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z;
        if (crossMagnitude < 1e-8f)//为了避免下面倒数过大
        {
            ResultStates[index] = 2;//标记为在水面上
            return;
        }

        float invSqrtCrossMag = 1f / Mathf.Sqrt(crossMagnitude);
        crossMagnitude *= invSqrtCrossMag;//单位向量化，总之现在M=sqrt（u×v），就是向量的长度了

        Vector3 normal;                             //面片的法线
        normal.x = crossUV.x * invSqrtCrossMag;     //单位向量化
        normal.y = crossUV.y * invSqrtCrossMag;
        normal.z = crossUV.z * invSqrtCrossMag;
        ResultNormals[index] = normal;

        Vector3 p; //面片重心到刚体重心的距离
        p.x = center.x - RigidbodyCoM.x;
        p.y = center.y - RigidbodyCoM.y;
        p.z = center.z - RigidbodyCoM.z;

        Vector3 crossAngVelP;//计算刚体坐标系下的速度v=d*w
        crossAngVelP.x = RigidbodyAngVel.y * p.z - RigidbodyAngVel.z * p.y;
        crossAngVelP.y = RigidbodyAngVel.z * p.x - RigidbodyAngVel.x * p.z;
        crossAngVelP.z = RigidbodyAngVel.x * p.y - RigidbodyAngVel.y * p.x;

        Vector3 velocity;//计算三角面片在世界坐标系下的速度
        velocity.x = crossAngVelP.x + RigidbodyLinearVel.x;
        velocity.y = crossAngVelP.y + RigidbodyLinearVel.y;
        velocity.z = crossAngVelP.z + RigidbodyLinearVel.z;

        area = crossMagnitude * 0.5f;       //叉乘算三角形面积
        //上面计算的结果，就得到了面片的速度和面积
        ResultVelocities[index] = velocity;
        ResultAreas[index] = area;
        //粘滞力先不管，计算风力

/*        float gama = Vector3.Dot(normal, windSpeed);
        if (gama >= 0)
        {
            return;//只保留迎风的那面
        }*/

        //相对风速
        Vector3 UA = windSpeed - velocity;
        //船舶的纵向x方向向量
        Vector3 xDir = -RigidbodyYDir;//TODO:实际是-y方向
        //实际应该只取水平的分量，然后单位化
        xDir = new Vector3(xDir.x, 0, xDir.z);
        xDir = Vector3.Normalize(xDir);
        //船舶的横向y方向向量
        Vector3 yDir = RigidbodyXDir;//实际是x方向
        yDir = new Vector3(yDir.x, 0, yDir.z);
        yDir = Vector3.Normalize(yDir);

        float cosAlpha = Vector3.Dot(normal, -xDir);
        //投影应该是受风那面的投影，
/*        if (Vector3.Dot(xDir, UA) < 0)
        {
            cosAlpha = Vector3.Dot(normal, xDir);
        }*/
        float Sx = area * cosAlpha;
        Sx = math.abs(Sx);
        if (Sx > 0)
            XArea += math.abs(Sx);
        float cosBeta = Vector3.Dot(normal, -yDir);
/*        if (Vector3.Dot(yDir, UA) < 0)
        {
            cosBeta = Vector3.Dot(normal, yDir);
        }*/
        float Sy = area * cosBeta;
        if (Sy > 0)
            YArea += math.abs(Sy);
        Sy = math.abs(Sy);
        float gama = Vector3.Dot(normal, UA);
        if (gama >= 0)
        {
            return;//只保留迎风的那面
        }

        //风向角,windDir和xdir的夹角
        float phi = math.abs(Vector3.Angle(xDir, -UA)) * math.PI / 180.0f;//度为单位,转化为弧度
        //x方向载荷系数
        float Cx = GetCx(phi);
        //Debug.Log("Cx"+Cx);
        //y方向载荷系数
        float Cy = GetCy(phi);
        //Debug.Log("Cy" + Cy);

        float Fx = 0.5f * Cx * airDensity * UA.magnitude * UA.magnitude * Sx;//纵向力
        float Fy = 0.5f * Cy * airDensity * UA.magnitude * UA.magnitude * Sy;//横向力
        if (Vector3.Dot(yDir, UA) < 0)
        {
            Fy = -Fy;
        }
/*        if (Vector3.Dot(xDir, UA) < 0)
        {
            Fx = -Fx;
        }*/
        FX += Fx;
        FY += Fy;
        //船舶的纵横和世界坐标下的不一样...只要前面x和yDir改好了就可以了
        windForce.x = Fx * xDir.x + Fy * yDir.x;
        windForce.z = Fx * xDir.z + Fy * yDir.z;
/*        Debug.Log("windForce:" + windForce);
        Debug.Log("phi:" + phi);
        Debug.Log("Fx:" + Fx);
        Debug.Log("Fy:" + Fy);
        Debug.Log("xDir:" + xDir);
        Debug.Log("yDir:" + yDir);
        Debug.Log("Cy:" + Cy);
        Debug.Log("UA:" + UA);*/

        //实际世界坐标系下的横向其实是z，y是重力方向
    }
    private void CalculateOriginalWindForce(in Vector3 windSpeed, ref Vector3 windForce, ref Vector3 windTorque)
    {
        //相对风速
        Vector3 velocity = RigidbodyLinearVel;
        Vector3 UA = windSpeed - velocity;
        //船舶的纵向x方向向量
        Vector3 xDir = -RigidbodyYDir;//TODO:实际是-y方向
        //实际应该只取水平的分量，然后单位化
        xDir = new Vector3(xDir.x, 0, xDir.z);
        xDir = Vector3.Normalize(xDir);
        //船舶的横向y方向向量
        Vector3 yDir = RigidbodyXDir;//实际是x方向
        yDir = new Vector3(yDir.x, 0, yDir.z);
        yDir = Vector3.Normalize(yDir);

        float phi = math.abs(Vector3.Angle(xDir, -UA)) * math.PI / 180.0f;//度为单位,转化为弧度
        Phi = Vector3.Angle(xDir, -UA);
        //x方向载荷系数
        float Cx = GetCx(phi);
        //Debug.Log("Cx"+Cx);
        //y方向载荷系数
        float Cy = GetCy(phi);
        //Debug.Log("Cy" + Cy);

        float Fx = 0.5f * Cx * airDensity * UA.magnitude * UA.magnitude * AF;//纵向力//乘-1，是因为风的方向和法线是反向
        float Fy = 0.5f * Cy * airDensity * UA.magnitude * UA.magnitude * AL;//横向力
        //横向力的方向和相对风向有关
        //Debug.Log("UA.magnitude:" + UA.magnitude);
        //Debug.Log("Cx" + Cx);
        float YForce = 1.0f;
        if (Vector3.Dot(yDir, UA) < 0)
        {
            Fy = -Fy;
            YForce = -1.0f;
        }
        FXOld += Fx;
        FYOld += Fy;
        //船舶的纵横和世界坐标下的不一样...只要前面x和yDir改好了就可以了
        windForce.x = Fx * xDir.x + Fy * yDir.x;
        windForce.z = Fx * xDir.z + Fy * yDir.z;

        //windTorque
        float Cn = GetCn(phi);
        float Ck = GetCk(phi);
        float Mk = YForce * 0.5f * Ck * airDensity * UA.magnitude * UA.magnitude  * AL * LOA;//横摇
        float Mn = YForce * 0.5f * Cn * airDensity * UA.magnitude * UA.magnitude  * AL * LOA;//艏摇

        windTorque.y = Mn;
        windTorque.z = Mk;
    }

    private void CalculateForces(in Vector3 p0, in Vector3 p1, in Vector3 p2,
    in float dist0, in float dist1, in float dist2,
    in int index, in int i0, in int i1, in int i2,
    ref Vector3 force, ref Vector3 center, ref float area, ref float distanceToSurface)
    {
        force.x = 0;
        force.y = 0;
        force.z = 0;

        //计算重心
        center.x = (p0.x + p1.x + p2.x) * 0.3333333333f;
        center.y = (p0.y + p1.y + p2.y) * 0.3333333333f;
        center.z = (p0.z + p1.z + p2.z) * 0.3333333333f;

        area = 0;
        distanceToSurface = 0;

        Vector3 u;
        u.x = p1.x - p0.x;
        u.y = p1.y - p0.y;
        u.z = p1.z - p0.z;

        Vector3 v;
        v.x = p2.x - p0.x;
        v.y = p2.y - p0.y;
        v.z = p2.z - p0.z;

        Vector3 crossUV;//(向内) 向外的法向（因为下面需要算出来volum是负数才对）
        crossUV.x = u.y * v.z - u.z * v.y;
        crossUV.y = u.z * v.x - u.x * v.z;
        crossUV.z = u.x * v.y - u.y * v.x;

        float crossMagnitude = crossUV.x * crossUV.x + crossUV.y * crossUV.y + crossUV.z * crossUV.z;
        if (crossMagnitude < 1e-8f)//为了避免下面倒数过大
        {
            ResultStates[index] = 2;//标记为在水面上
            return;
        }

        float invSqrtCrossMag = 1f / Mathf.Sqrt(crossMagnitude);
        crossMagnitude *= invSqrtCrossMag;//单位向量化，总之现在M=sqrt（u×v），就是向量的长度了

        Vector3 normal;                             //面片的法线
        normal.x = crossUV.x * invSqrtCrossMag;     //单位向量化
        normal.y = crossUV.y * invSqrtCrossMag;
        normal.z = crossUV.z * invSqrtCrossMag;
        ResultNormals[index] = normal;

        Vector3 p; //面片重心到刚体重心的距离
        p.x = center.x - RigidbodyCoM.x;
        p.y = center.y - RigidbodyCoM.y;
        p.z = center.z - RigidbodyCoM.z;

        Vector3 crossAngVelP;//计算刚体坐标系下的速度v=d*w
        crossAngVelP.x = RigidbodyAngVel.y * p.z - RigidbodyAngVel.z * p.y;
        crossAngVelP.y = RigidbodyAngVel.z * p.x - RigidbodyAngVel.x * p.z;
        crossAngVelP.z = RigidbodyAngVel.x * p.y - RigidbodyAngVel.y * p.x;

        Vector3 velocity;//计算三角面片在世界坐标系下的速度
        velocity.x = crossAngVelP.x + RigidbodyLinearVel.x;
        velocity.y = crossAngVelP.y + RigidbodyLinearVel.y;
        velocity.z = crossAngVelP.z + RigidbodyLinearVel.z;

        Vector3 waterNormalVector;//初始化水面的法向是向上的
        waterNormalVector.x = _worldUpVector.x;
        waterNormalVector.y = _worldUpVector.y;
        waterNormalVector.z = _worldUpVector.z;

        area = crossMagnitude * 0.5f;       //叉乘算三角形面积
        //Debug.Log("area " + ": " + area);   1-2量级
        distanceToSurface = 0.0f;
        if (area > 1e-8f)
        {
            Vector3 f0;
            f0.x = p0.x - center.x;
            f0.y = p0.y - center.y;
            f0.z = p0.z - center.z;

            Vector3 f1;
            f1.x = p1.x - center.x;
            f1.y = p1.y - center.y;
            f1.z = p1.z - center.z;

            Vector3 f2;
            f2.x = p2.x - center.x;
            f2.y = p2.y - center.y;
            f2.z = p2.z - center.z;

            Vector3 cross12;
            cross12.x = f1.y * f2.z - f1.z * f2.y;
            cross12.y = f1.z * f2.x - f1.x * f2.z;
            cross12.z = f1.x * f2.y - f1.y * f2.x;
            float magCross12 = cross12.x * cross12.x + cross12.y * cross12.y + cross12.z * cross12.z;
            magCross12 = magCross12 < 1e-8f ? 0 : magCross12 / Mathf.Sqrt(magCross12);

            Vector3 cross20;
            cross20.x = f2.y * f0.z - f2.z * f0.y;
            cross20.y = f2.z * f0.x - f2.x * f0.z;
            cross20.z = f2.x * f0.y - f2.y * f0.x;
            float magCross20 = cross20.x * cross20.x + cross20.y * cross20.y + cross20.z * cross20.z;
            magCross20 = magCross20 < 1e-8f ? 0 : magCross20 / Mathf.Sqrt(magCross20);

            float invDoubleArea = 0.5f / area;      //算三个三角形的高度权重
            float w0 = magCross12 * invDoubleArea;  //三个三角形的面积的占比
            float w1 = magCross20 * invDoubleArea;
            float w2 = 1.0f - (w0 + w1);

            if (calculateWaterNormals)
            {
                //Vector3 n0 = WaterNormals[i0];
                //Vector3 n1 = WaterNormals[i1];
                //Vector3 n2 = WaterNormals[i2];
                Vector3 n0 = GetWaterNormalAtPosition(p0);
                Vector3 n1 = GetWaterNormalAtPosition(p1);
                Vector3 n2 = GetWaterNormalAtPosition(p2);

                float dot0 = n0.x * _worldUpVector.x + n0.y * _worldUpVector.y + n0.z * _worldUpVector.z;
                float dot1 = n1.x * _worldUpVector.x + n1.y * _worldUpVector.y + n1.z * _worldUpVector.z;
                float dot2 = n2.x * _worldUpVector.x + n2.y * _worldUpVector.y + n2.z * _worldUpVector.z;

                distanceToSurface =
                    w0 * dist0 * dot0 +
                    w1 * dist1 * dot1 +
                    w2 * dist2 * dot2;

                //Debug.Log("distanceToSurface " + ": " + distanceToSurface);    需要将n0、n1、n2的normal取负数变成y方向为正数，不然算出来的distance会变成负数

                waterNormalVector.x = w0 * n0.x + w1 * n1.x + w2 * n2.x;            //三角片元处的水面法线为三个顶点处水面法线的加权平均
                waterNormalVector.y = w0 * n0.y + w1 * n1.y + w2 * n2.y;
                waterNormalVector.z = w0 * n0.z + w1 * n1.z + w2 * n2.z;
            }
            else
            {
                distanceToSurface =
                    w0 * dist0 +
                    w1 * dist1 +
                    w2 * dist2;//加权计算
            }

            if (calculateWaterFlows)
            {
                Vector3 wf0 = WaterFlows[i0];//默认都是0 还好 
                Vector3 wf1 = WaterFlows[i1];
                Vector3 wf2 = WaterFlows[i2];

                velocity.x += w0 * -wf0.x + w1 * -wf1.x + w2 * -wf2.x;
                velocity.y += w0 * -wf0.y + w1 * -wf1.y + w2 * -wf2.y;
                velocity.z += w0 * -wf0.z + w1 * -wf1.z + w2 * -wf2.z;
            }
        }
        else
        {
            ResultStates[index] = 2;
            return;
        }
        //上面计算的结果，就得到了面片的速度和面积
        ResultVelocities[index] = velocity;
        ResultAreas[index] = area;

        distanceToSurface = distanceToSurface < 0 ? 0 : distanceToSurface;

        float densityArea = fluidDensity * area;   //rho * s

        Vector3 buoyantForce;
        if (buoyantForceCoefficient > 1e-5f)
        {
            float gravity = _gravity.y;
            float dotNormalWaterNormal = Vector3.Dot(normal, waterNormalVector);//面片的法线和水面法线

            float volume = densityArea * distanceToSurface * dotNormalWaterNormal;//rho * s * h * 投影
            submergedVolume -= densityArea * distanceToSurface * dotNormalWaterNormal;//但是实际上是没用的？最多是public出去给人看看

            //Debug.Log("The volume is: " + volume);                        //正负浮动,会一直变大
            //Debug.Log("The submergedVolume is: " + submergedVolume);      //会一直变大，很可怕

            float bfc = volume * gravity * buoyantForceCoefficient;//rho * s * h * 投影 * g
            //Debug.Log("The bfc is: " + bfc);

            buoyantForce.x = waterNormalVector.x * bfc;
            buoyantForce.y = waterNormalVector.y * bfc;
            buoyantForce.z = waterNormalVector.z * bfc;
            //Debug.Log("The buoyantForce is: " + buoyantForce);            //变成负数了！
        }
        else
        {
            buoyantForce.x = 0;
            buoyantForce.y = 0;
            buoyantForce.z = 0;
        }

        //上面算完了浮力，接下来是粘滞力和阻力
        Vector3 dynamicForce;
        dynamicForce.x = 0;
        dynamicForce.y = 0;
        dynamicForce.z = 0;

        float velocityMagnitude = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
        if (velocityMagnitude > 1e-5f)
        {
            float invSqrtVelMag = 1f / Mathf.Sqrt(velocityMagnitude);
            velocityMagnitude *= invSqrtVelMag;

            Vector3 velocityNormalized;//单位向量
            velocityNormalized.x = velocity.x * invSqrtVelMag;
            velocityNormalized.y = velocity.y * invSqrtVelMag;
            velocityNormalized.z = velocity.z * invSqrtVelMag;

            float dotNormVel = Vector3.Dot(normal, velocityNormalized);//面片的法向和速度点乘。如果是垂直的话，就是0。同向就是1。可以理解成水的阻力。

            if (hydrodynamicForceCoefficient > 0.001f)
            {
                if (velocityDotPower < 0.999f || velocityDotPower > 1.001f)
                {
                    dotNormVel = (dotNormVel > 0f ? 1f : -1f) * Mathf.Pow(dotNormVel > 0 ? dotNormVel : -dotNormVel, velocityDotPower);
                }

                float c = -dotNormVel * velocityMagnitude * densityArea;
                dynamicForce.x = normal.x * c;
                dynamicForce.y = normal.y * c;
                dynamicForce.z = normal.z * c;
            }

            if (skinDragCoefficient > 1e-4f)
            {
                float absDot = dotNormVel < 0 ? -dotNormVel : dotNormVel;
                float c = -(1.0f - absDot) * skinDragCoefficient * densityArea;//如果越垂直，那么粘滞力就越大
                dynamicForce.x += velocity.x * c;
                dynamicForce.y += velocity.y * c;
                dynamicForce.z += velocity.z * c;
            }

            float dfc = hydrodynamicForceCoefficient * (dotNormVel > 0 ? slamForceCoefficient : suctionForceCoefficient);//进入还是离开水
            dynamicForce.x *= dfc;
            dynamicForce.y *= dfc;
            dynamicForce.z *= dfc;
        }

        force.x = buoyantForce.x + dynamicForce.x;
        force.y = buoyantForce.y + dynamicForce.y;
        force.z = buoyantForce.z + dynamicForce.z;
    }


    void ThreeUnderWater(Vector3 p0, Vector3 p1, Vector3 p2,
                                 float dist0, float dist1, float dist2,
                                 int i0, int i1, int i2, int index)
    {
        ResultStates[index] = 0;

        int i = index * 6;
        ResultP0s[i] = p0;
        ResultP0s[i + 1] = p1;
        ResultP0s[i + 2] = p2;

        Vector3 zeroVector;
        zeroVector.x = 0;
        zeroVector.y = 0;
        zeroVector.z = 0;

        ResultP0s[i + 3] = zeroVector;
        ResultP0s[i + 4] = zeroVector;
        ResultP0s[i + 5] = zeroVector;

        CalculateForces(p0, p1, p2, -dist0, -dist1, -dist2, index, i0, i1, i2,
            ref ResultForces[index], ref ResultCenters[index],
            ref ResultAreas[index], ref ResultDistances[index]);
    }

    void TwoUnderWater(Vector3 p0, Vector3 p1, Vector3 p2,
        float dist0, float dist1, float dist2,
        int i0, int i1, int i2, int index)
    {
        ResultStates[index] = 1;

        Vector3 H, M, L, IM, IL;
        float hH, hM, hL;
        int mIndex;

        // H is always at position 0
        H = p0;

        // Find the index of M
        mIndex = i0 - 1;
        if (mIndex < 0)
        {
            mIndex = 2;
        }

        // Heights to the water
        hH = dist0;

        if (i1 == mIndex)
        {
            M = p1;
            L = p2;

            hM = dist1;
            hL = dist2;
        }
        else
        {
            M = p2;
            L = p1;

            hM = dist2;
            hL = dist1;
        }

        float cIM = -hM / (hH - hM);
        IM.x = cIM * (H.x - M.x) + M.x;
        IM.y = cIM * (H.y - M.y) + M.y;
        IM.z = cIM * (H.z - M.z) + M.z;

        float cIL = -hL / (hH - hL);
        IL.x = cIL * (H.x - L.x) + L.x;
        IL.y = cIL * (H.y - L.y) + L.y;
        IL.z = cIL * (H.z - L.z) + L.z;

        int i = index * 6;
        ResultP0s[i] = M;
        ResultP0s[i + 1] = IM;
        ResultP0s[i + 2] = IL;

        ResultP0s[i + 3] = M;
        ResultP0s[i + 4] = IL;
        ResultP0s[i + 5] = L;

        CalculateForces(M, IM, IL, -hM, 0, 0, index, i0, i1, i2,
            ref _f0, ref _c0, ref _a0, ref _dst0);
        CalculateForces(M, IL, L, -hM, 0, -hL, index, i0, i1, i2,
            ref _f1, ref _c1, ref _a1, ref _dst1);

        float weight0 = _a0 / (_a0 + _a1);
        float weight1 = 1 - weight0;

        Vector3 resultForce;
        resultForce.x = _f0.x + _f1.x;
        resultForce.y = _f0.y + _f1.y;
        resultForce.z = _f0.z + _f1.z;

        Vector3 resultCenter;
        resultCenter.x = _c0.x * weight0 + _c1.x * weight1;
        resultCenter.y = _c0.y * weight0 + _c1.y * weight1;
        resultCenter.z = _c0.z * weight0 + _c1.z * weight1;

        ResultForces[index] = resultForce;
        ResultCenters[index] = resultCenter;
        ResultDistances[index] = _dst0 * weight0 + _dst1 * weight1;
        ResultAreas[index] = _a0 + _a1;
    }

    void OneUnderWater(Vector3 p0, Vector3 p1, Vector3 p2,
        float dist0, float dist1, float dist2,
        int i0, int i1, int i2, int index)
    {
        ResultStates[index] = 1;

        Vector3 H, M, L, JM, JH;
        float hH, hM, hL;

        L = p2;

        // Find the index of H
        int hIndex = i2 + 1;
        if (hIndex > 2)
        {
            hIndex = 0;
        }

        // Get heights to water
        hL = dist2;

        if (i1 == hIndex)
        {
            H = p1;
            M = p0;

            hH = dist1;
            hM = dist0;
        }
        else
        {
            H = p0;
            M = p1;

            hH = dist0;
            hM = dist1;
        }

        float cJM = -hL / (hM - hL);
        JM.x = cJM * (M.x - L.x) + L.x;
        JM.y = cJM * (M.y - L.y) + L.y;
        JM.z = cJM * (M.z - L.z) + L.z;

        float cJH = -hL / (hH - hL);
        JH.x = cJH * (H.x - L.x) + L.x;
        JH.y = cJH * (H.y - L.y) + L.y;
        JH.z = cJH * (H.z - L.z) + L.z;

        int i = index * 6;
        ResultP0s[i] = L;
        ResultP0s[i + 1] = JH;
        ResultP0s[i + 2] = JM;

        Vector3 zeroVector;
        zeroVector.x = 0;
        zeroVector.y = 0;
        zeroVector.z = 0;

        ResultP0s[i + 3] = zeroVector;
        ResultP0s[i + 4] = zeroVector;
        ResultP0s[i + 5] = zeroVector;

        // Generate trisPtr
        CalculateForces(L, JH, JM, -hL, 0, 0, index, i0, i1, i2,
            ref ResultForces[index], ref ResultCenters[index],
            ref ResultAreas[index], ref ResultDistances[index]);
    }


    // 这里需要一个函数来获取给定位置的水面高度
    private float GetWaterHeightAtPosition(float3 position)
    {
        // 这个函数需要根据你的具体实现来编写
        // 例如从RenderTexture中读取高度值

        // Bake data we want to capture in the job
        int w = m_HeightFieldRT.width;
        int h = m_HeightFieldRT.height;
        float border = HeightFieldSystem.Border;
        float texelW = 2.0f * border / w;
        float texelH = 2.0f * border / h;

        float posx = position.x;
        float posy = position.z;
        float2 wPos = new float2(posx, posy);

        // Make particle positions start from 0,0 coordinates
        float2 pos = -wPos + border;
        // Pixel coordinates with fractional parts
        float xF = pos.x / texelW;
        float yF = pos.y / texelH;
        // Texture pixel indices
        int x = (int)xF;
        int y = (int)yF;
        //TODO：严格来讲，应该是周围四个点加权的结果
        int x0y0 = x + y * w;

        float waterHeight = m_HeightFieldTex32.GetPixel(x, y).g;//获取G通道，即y，即高度
        return waterHeight;
    }

    private Vector3 GetWaterNormalAtPosition(float3 position)
    {

        // Bake data we want to capture in the job
        int w = m_HeightFieldRT.width;
        int h = m_HeightFieldRT.height;

        //float border = 160.0f;
        float border = HeightFieldSystem.Border;
        float texelW = 2.0f * border / w;
        float texelH = 2.0f * border / h;

        float posx = position.x;
        float posy = position.z;
        float2 wPos = new float2(posx, posy);

        // Make particle positions start from 0,0 coordinates
        float2 pos = -wPos + border;
        // Pixel coordinates with fractional parts
        float xF = pos.x / texelW;
        float yF = pos.y / texelH;
        // Texture pixel indices
        int x = (int)xF;
        int y = (int)yF;

        if (x - 1 < 0) x = 0;
        if (y - 1 < 0) y = 0;
        if (x + 1 >= w) x = w - 1;
        if (y + 1 >= h) y = h - 1;
        int x0y0 = (x - 1) + y * w;
        int x1y0 = (x + 1) + y * w;
        int x0y1 = x + (y - 1) * w;
        int x1y1 = x + (y + 1) * w;

        //float heightLeft = m_HeightFieldTex.GetPixel(x - 1 < 0 ? 0 : x - 1, y).r;
        //float heightRight = m_HeightFieldTex.GetPixel(x + 1 >= w ? w - 1 : x + 1, y).r;
        //float heightDown = m_HeightFieldTex.GetPixel(x, y - 1 < 0 ? 0 : y - 1).r;
        //float heightUp = m_HeightFieldTex.GetPixel(x, y + 1 >= h ? h - 1 : y + 1).r;

        float heightLeft = m_HeightFieldTex32.GetPixel(x - 1 < 0 ? 0 : x - 1, y).g;
        float heightRight = m_HeightFieldTex32.GetPixel(x + 1 >= w ? w - 1 : x + 1, y).g;
        float heightDown = m_HeightFieldTex32.GetPixel(x, y - 1 < 0 ? 0 : y - 1).g;
        float heightUp = m_HeightFieldTex32.GetPixel(x, y + 1 >= h ? h - 1 : y + 1).g;

        float dx = (heightRight - heightLeft);
        float dz = (heightUp - heightDown);
        //Debug.Log("The dx is: " + dx);
        //Debug.Log("The dz is: " + dz);
        // 创建两个三维向量，对应于相邻高度差
        Vector3 tangent = new Vector3(2 * texelW, dx, 0.0f);
        Vector3 bitangent = new Vector3(0.0f, dz, 2 * texelH);
        //Debug.Log("tangent: " + tangent);
        //Debug.Log("bitangent: " + bitangent);
        // 计算两个向量的叉积，得到法线向量
        Vector3 normal = Vector3.Cross(tangent, bitangent).normalized;

        normal = -1.0f * normal;

        // 将法线的范围从[-1,1]调整到[0,1]
        //normal = (normal + Vector3.one) * 0.5f;
/*        if(normal.y<0)
           Debug.Log("The normal is: " + normal);*/

        return normal;
    }

    private Vector3 GetWindAtPosition(float3 position)
    {
        Vector3 windSpeed = new Vector3(1.0f,0.0f,0.0f);

        Vector3 windDir = new Vector3(1.0f, 0.0f, 0.0f);//风向可能用一个函数来代替

        //风速=定常风+脉动风
        //float constantWind = 5.0f;//定常风可能用一个函数来代替

        float pulseWind = Davenport(constantWind);
        //pulseWind = 0;

        float U10 = constantWind + pulseWind;
        //Debug.Log("U10"+ U10);
        //float waterHeright = GetWaterHeightAtPosition(position);
        float Height = position.y;//距离海面的高度
        if(Height<0)
            return constantWind * windDir;
        float Uz = U10 * math.pow(Height / 10.0f, 0.125f);//y是高度
        //Debug.Log("Uz:" + Uz);
        //Debug.Log("position.y:" + position.y);
        //Debug.Log("Height" + Height);
        windSpeed = windDir * Uz;

        return windSpeed;
    }

    private float Davenport(float U)
    {
        float pulseWind = 0.0f;
        //TotalTime
        float fmax = 6.0f;
        int sampleN = 600;
        float deltaf = fmax / sampleN;
        TotalTime += time;
        if (TotalTime > (2 * math.PI / deltaf))
        {
            TotalTime = time;
        }
        float Cd = (float)(0.001 + 6 * 0.00001 * U);
        for (int i = 1; i <= sampleN; ++i)
        {
            float f = sampleN * deltaf;
            float fs = 1200 * f / U;
            float Sf = U * U / f * Cd * 4 * fs * fs / math.pow(1 + fs * fs, 4.0f / 3.0f);
            float Ui = math.sqrt(2 * Sf * deltaf);
            pulseWind += Ui * math.cos(f * TotalTime);
        }
        return pulseWind;
    }

    float LOA = 4.91f;//船长
    float B = 2.06f;//船宽
    float AF = 1.5346f;//正面投影面积
    float AL = 2.6183f;//侧面投影面积
    float C = 0.5445f;//侧面投影型心到船舶中心的距离
    float HC = 0.3174f;//侧面投影型心到水线的距离
    float AOD = 0;//甲板上物体的侧投影面积——测不出来，默认是0
    float HBR = 0.987f;//最上层建筑物到水面的距离——测不出来，默认是船舶高度

    float a0 = 0.404f;
    float a1 = 0.386f;
    float a2 = 0.902f;

    float b10 = -0.922f;
    float b11 = 0.507f;
    float b12 = 1.16f;
    float b20 = 0.018f;
    float b21 = -5.09f;
    float b22 = 10.4f;
    float b23 = -3.01f;
    float b24 = -0.341f;

    float gama10 = 0.116f;
    float gama11 =3.35f;
    float gama20 = 0.446f;
    float gama21 = 2.19f;

    float delta10 = 0.458f;
    float delta11 = 3.25f;
    float delta12 = -2.31f;
    float delta20 = -1.9f;
    float delta21 = 12.7f;
    float delta22 = 24.4f;
    float delta23 = -40.3f;
    float delta24 = -5.48f;

    float ep10 = -0.585f;
    float ep11 = -0.906f;
    float ep12 = 3.24f;
    float ep20 = -0.314f;
    float ep21 = -1.12f;

    float CLF1 = 0.0f;
    float CLF2 = 0.0f;
    float CYM1 = 0.0f;
    float CYM2 = 0.0f;
    float CXLI1 = 0.0f;
    float CXLI2 = 0.0f;
    float CALF1 = 0.0f;
    float CALF2 = 0.0f;
    public float GetCx(float phi)//根据风向角phi计算x方向的风载荷系数
    {
        float CLF = 0.0f;//主流阻力
        CLF1 = b10 + b11 * AL / (LOA * B) + b12 * C /LOA ;//0°的时候
        CLF2 = b20 + b21 * B / LOA + b22 * HC / LOA + b23 * AOD / (LOA * LOA) + b24 * AF / (B * B);//180°的时候
        if (phi <= (math.PI / 2))
        {
            CLF = CLF1 * math.cos(phi);
        }
        else {
            CLF = CLF2 * math.cos(phi);
        }
       
        float CXLI = 0.0f;
        CXLI1 = delta10 + delta11 * AL / (LOA * HBR) + delta12 * AF / (B * HBR);
        CXLI2 = delta20 + delta21 * AL / (LOA * HBR) + delta22 * AF / AL + delta23 * B / LOA + delta24 * AF / (B * HBR);
        if (phi <= (math.PI / 2))
        {
            CXLI = CXLI1;
        }
        else
        {
            CXLI = CXLI2;
        }

        float CALF = 0.0f;
        CALF1 = ep10 + ep11 * AOD / AL + ep12 * B / LOA;
        CALF2 = ep20 + ep21 * AOD / AL;
        if (phi <= (math.PI / 2))
        {
            CALF = CALF1;
        }
        else
        {
            CALF = CALF2;
        }
        float CX = CLF + CXLI * (math.sin(phi)
            - 0.5f * math.sin(phi) * math.cos(phi) * math.cos(phi)) * math.sin(phi) * math.cos(phi)
            + CALF * math.sin(phi) * math.cos(phi) * math.cos(phi) * math.cos(phi);

        return CX;
    }
    public float GetCy(float phi)
    {
        //根据风向角phi计算y方向的风载荷系数

        float CCF = 0.0f;
        CCF = a0 + a1 * AF / (B * HBR) + a2 * HBR / LOA;

        float CYLI = 0.0f;
        float CYM = 0.0f;
        CYM1 = gama10 + gama11 * AF / (LOA * B);
        CYM2 = gama20 + gama21 * AOD / (LOA * LOA);
        if (phi <= (math.PI / 2))
        {
            CYM = CYM1;
        }
        else {
            CYM = CYM2;
        }
        CYLI = math.PI * AL / (LOA * LOA) + CYM;

        float CY = CCF * math.sin(phi) * math.sin(phi)
            + CYLI * (math.cos(phi) + 0.5f * math.sin(phi) * math.sin(phi) * math.cos(phi)) * math.sin(phi) * math.cos(phi);

        return CY;
    }
    private float GetCn(float phi)
    {
        //根据风向角phi计算艏摇的风载荷系数

        float CY = GetCy(phi);
        float CN = CY * (0.927f * C / LOA - 0.149f * (phi - math.PI / 2.0f));
        return CN;
    }
    private float GetCk(float phi)
    {
        //根据风向角phi计算横摇的风载荷系数
        float CY = GetCy(phi);
        float HL = HC / LOA;
        float CK;
        if (HL <= 0.097)
        {
            CK = CY * (0.0737f * (float)math.pow(HL, -0.821f));
        }
        else {
            CK = CY * 0.5f;
        }
        return CK;
    }


}
