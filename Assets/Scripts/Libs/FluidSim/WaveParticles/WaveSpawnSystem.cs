using OneBitLab.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using System;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using Unity.Burst;

namespace OneBitLab.FluidSim
{
    [AlwaysUpdateSystem]
    public class WaveSpawnSystem : SystemBase
    {
        //-------------------------------------------------------------
        public static readonly SharedStatic<float> c_WaveParticleRadius =
            SharedStatic<float>.GetOrCreate<WaveSpawnSystem, FloatFieldKey>();

        // Define a Key type to identify IntField
        private class FloatFieldKey { }
        //public static float c_WaveParticleRadius = 0.15f;
        public const float c_WaveParticleHeight = 0.05f;
        public const float c_WaveParticleSpeed = 0.5f;
        public float m_windSpeed = 0.5f;
        private const float gravity = 9.8f;

        private const int c_StartEntitiesCount = 0;//1000000; //300

        public static float s_WaveParticleMinHeight = c_WaveParticleHeight;

        public float DropsPerSecond
        {
            set
            {
                // TODO: change for math.EPSILON
                // avoid division by 0
                m_DropsInterval = 1.0f / math.max(value, 0.0000001f);
                m_TimeToDrop = m_DropsInterval;
            }
        }

        public EntityArchetype WaveArchetype => m_Archetype;

        private JobHandle                              m_ExternalDependency;
        private EntityArchetype                        m_Archetype;
        private EndSimulationEntityCommandBufferSystem m_EndSimECBSystem;
        private float                                  m_DropsInterval;
        private float                                  m_TimeToDrop = 1000000.0f;
        private Random                                 m_Rnd        = new Random( seed: 1234 );
        private EntityQuery                            m_AllEntitiesQuery;
        
        private int N;
        private int M;
        private int L;

        private bool regular;
        private float regularLambda;

        //-------------------------------------------------------------
        public void AddExternalDependency(JobHandle newDependency)
        {
            m_ExternalDependency = JobHandle.CombineDependencies(
                m_ExternalDependency,
                newDependency
            );
        }

        //-------------------------------------------------------------
        protected override void OnCreate()
        {
            m_EndSimECBSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            m_Archetype = EntityManager.CreateArchetype(
                typeof(WaveOrigin),
                typeof(WavePos),
                typeof(WaveDir),
                typeof(WaveHeight),
                typeof(WaveSpeed),
                typeof(DispersAngle),
                typeof(TimeToReflect),
                typeof(TimeToSubdiv),
                typeof(WaveVector),
                typeof(Radius)
            );

            var entities = new NativeArray<Entity>( c_StartEntitiesCount, Allocator.Temp );
            EntityManager.CreateEntity( m_Archetype, entities );

            float k = 3.0f;//先默认一个值
            float kLength = math.max(0.001f, k);
            float speed = (float)Math.Sqrt(gravity / kLength);
            speed = 0.5f;
            float radius = (float)Math.PI / kLength;
            radius = 0.15f;
            c_WaveParticleRadius.Data = radius;
            //生成指定数量的波粒子
            float2 dir = math.normalizesafe(m_Rnd.NextFloat2(-1.0f, 1.0f));
            for (int i = 0; i < c_StartEntitiesCount; i++)
            {
                int n = 48;
                int l = 10;
                float mink = 2 * (float)Math.PI / l;
                float maxk = 2 * (float)Math.PI * n / l;

                dir = math.normalizesafe(m_Rnd.NextFloat2( -1.0f, 1.0f ));
                //float height = 2 * (float)Math.Sqrt(SpectrumService.Instance.JONSWAPSpectrum(k, dir) * 2);
                // height = -0.05f;
                //float height = 0.1f;
                k = m_Rnd.NextFloat(mink, maxk);
                kLength = math.max(0.001f, k);
                speed = (float)Math.Sqrt(gravity / kLength);
                radius = (float)Math.PI / kLength;
                //float height = (float)Math.Sqrt(SpectrumService.Instance.PhillipsSpectrum(kLength, dir) * 2) / 2;
                float height = (float)Math.Sqrt(SpectrumService.Instance.JONSWAPSpectrum(kLength, dir) * 2);
                //float height = 0.005f;
                // if(m_Rnd.NextBool())
                // {
                //     radius = 0.15f;
                // }
                // else{
                //     radius= 0.25f;
                // }
                // Debug.Log("height"+height);
                EntityManager.SetComponentData( entities[ i ], new WavePos {Value = m_Rnd.NextFloat2( -5.0f, 5.0f )} );
                EntityManager.SetComponentData( entities[ i ], new WaveHeight {Value = height} );
                EntityManager.SetComponentData( entities[ i ], new WaveSpeed {Value = speed} );
                EntityManager.SetComponentData( entities[ i ], new WaveDir { Value = dir } );
                EntityManager.SetComponentData( entities[ i ], new WaveVector { Value = kLength } );
                EntityManager.SetComponentData( entities[ i ], new Radius { Value = radius } );
            }
            entities.Dispose();



            /*int N = 40;
            int M = 40;
            int L = 10;//限制lambda max*/

            N = ResourceLocatorService.Instance.N;
            M = ResourceLocatorService.Instance.M;
            L = ResourceLocatorService.Instance.L;

            //k的具体取值还要思考，本意是为了限制半径r的范围//其实还应该限制速度
            //参考20年的论文
            float kmin = (float)Math.PI / L;//
            float kmax = 75.0f;
            float Rmax = 5.0f;
            float border = 5.0f; //那个plane的大小是这么大
            float minHeight = 0.0015f;
            float dk = 2 * (float)Math.PI / L;

            //生成指定参数的规则波
            regular = ResourceLocatorService.Instance.regular;
            if (regular)
            {
                //根据波长生成规则波
                regularLambda = ResourceLocatorService.Instance.regularLambda;
                float RRadius = regularLambda / 2.0f;
                float Rk = math.PI / RRadius;
                float RHeight = ResourceLocatorService.Instance.regularHeight/2.0f;//波粒子的高度是实际振幅的一半....
                float RSpeed = (float)Math.Sqrt(gravity / Rk);
                dir = new float2(1.0f, 0.0f);
                GenerateParticles(dir, RHeight, RSpeed, Rk, RRadius, border);
            }

            for (int n = -N / 2; n < N / 2 ; n++)
            {
                float kx = 2 * (float)Math.PI * n / L;
                for (int m = -M / 2; m < M / 2 ; m++)
                {
                    float ky = 2 * (float)Math.PI * m / L;
                    float K = (float)Math.Sqrt(kx * kx + ky * ky);
/*                    K = 2;
                    kx = 1;
                    ky = 2;*/
                    //Debug.Log("K:" + K);
                    //K=3f;
                    if (K < kmin || K > kmax)
                    {
                        Debug.Log("K out of range:" + K);//主要是把0排除出去
                        continue;
                    }
                    dir = math.normalizesafe(new float2(kx, ky)); //计算每个k对应的dir
                    //dir = math.normalizesafe(new float2(1.0f, -1.0f));
                    //Debug.Log("dir:" + dir);
                    float Radius = (float)Math.PI / K;
                    //Radius = 0.4f;
                    if (Radius > Rmax)
                    {
                        Debug.Log("R out of range:" + Radius);
                        continue;
                    }
                    float Height = (float)Math.Sqrt(SpectrumService.Instance.JONSWAPSpectrum(K, dir) * 2);//* dk 在24年的论文没有/2，但是20年的有/2
                    //Height = 0.15f;
                    //float Height = (float)Math.Sqrt(SpectrumService.Instance.PhillipsSpectrum(K, dir) * 2)  / 2;//Phillips谱
                    /*minHeight = Math.Min(0.003f * Radius * dk, 0.001f * dk);//自适应剔除的范围，根据半径长度来判断
                    if (Height < minHeight)
                    {
                        //Debug.Log("Height out of range:" + Height + ",m:" + m + ",n:" + n+",K:"+K);
                        //continue;
                    }*/
                    //Debug.Log("Height:" + Height);
                    speed = (float)Math.Sqrt(gravity / K);
                    //把创建一层（一类，一个阵列）粒子的过程封装成一个函数
                    GenerateParticles(dir, Height, speed, K, Radius, border);
                }
            }
            //swell
/*            for (int n = -N / 2; n < -N / 2; n++)
            {
                float kx = 2 * (float)Math.PI * n / L;
                for (int m = -M / 2; m < M / 2; m++)
                {
                    float ky = 2 * (float)Math.PI * m / L;
                    float K = (float)Math.Sqrt(kx * kx + ky * ky);
                    if (K < kmin || K > kmax)
                    {
                        Debug.Log("K out of range:" + K);
                        continue;
                    }
                    dir = math.normalizesafe(new float2(kx, ky)); //计算每个k对应的dir
                    float Radius = (float)Math.PI / K;
                    if (Radius > Rmax)
                    {
                        Debug.Log("R out of range:" + Radius);
                        continue;
                    }
                    float Height = (float)Math.Sqrt(SpectrumService.Instance.JONSWAPGlennSpectrum(K, dir) * 2) * dk;
                    if (Height < minHeight)
                    {
                        Debug.Log("Height out of range:" + Height);
                        continue;
                    }
                    speed = (float)Math.Sqrt(gravity / K);
                    GenerateParticles(dir, Height, speed, K, Radius, border);
                }
            }
*/
            m_AllEntitiesQuery = GetEntityQuery(ComponentType.ReadOnly<WaveHeight>());
            Debug.Log(
                "particle count:" + m_AllEntitiesQuery.CalculateEntityCountWithoutFiltering()
            );
        }

        private void GenerateParticles(float2 dir, float Height, float speed, float k, float radius,float border)
        {
            // dir = math.normalizesafe(m_Rnd.NextFloat2(-1.0f, 1.0f)); //计算每个w对应的dir？
            // radius = 0.15f * w; //计算每个w对应的radius等
            //两个循环，摆放粒子
            NativeQueue<float2> wavepos_queue = new NativeQueue<float2>(Allocator.Temp);
            NativeQueue<float2> neg_wavepos_queue = new NativeQueue<float2>(Allocator.Temp);
            // neg_wavepos_queue.Enqueue(new float2(0.0f,0.0f));
            //需要处理dirx=0的特殊情况
            if (dir.x == 0)
            {
                /*Debug.Log("dirx==0");
                Debug.Log("dir.y:"+ dir.y);*/
                for (int i = 0; i < 400; i++) //200其实这里就包含了rmin是10/200=0.05
                {
                    //Entity entity = EntityManager.CreateEntity(m_Archetype);
                    float2 wavepos;
                    wavepos = new float2(0, -border + 2 * i * radius); //此时dir.y一定是正负1
                    if (wavepos.y > border - radius) //不足够放一个负粒子了
                    {
                        break;
                    }
                    wavepos_queue.Enqueue(wavepos);

                    //负粒子
                    float2 neg_wavepos;
                    neg_wavepos = new float2(0, -border + (2 * i + 1) * radius );
                    neg_wavepos_queue.Enqueue(neg_wavepos);

                    //垂直dir方向的，每差一个radius/2//此时dirx=0，diry=正负1
                    float Radius = radius * 0.5f;
                    for (int j = 1; j < 300; j++) //300
                    {
                        float2 wavepos3 = new float2(
                            wavepos.x + Radius * j * dir.y,
                            wavepos.y - Radius * j * dir.x
                        );
                        float2 wavepos4 = new float2(
                            wavepos.x - Radius * j * dir.y,
                            wavepos.y + Radius * j * dir.x
                        );
                        if (math.abs(wavepos3.x) < border)
                        {
                            wavepos_queue.Enqueue(wavepos3);
                        }
                        if (math.abs(wavepos4.x) < border)
                        {
                            wavepos_queue.Enqueue(wavepos4);
                        }
                        float2 neg_wavepos3 = new float2(
                            neg_wavepos.x + Radius * j * dir.y,
                            neg_wavepos.y - Radius * j * dir.x
                        );
                        if (math.abs(neg_wavepos3.x) < border)
                            neg_wavepos_queue.Enqueue(neg_wavepos3);
                        float2 neg_wavepos4 = new float2(
                            neg_wavepos.x - Radius * j * dir.y,
                            neg_wavepos.y + Radius * j * dir.x
                        );
                        if (math.abs(neg_wavepos4.x) < border)
                            neg_wavepos_queue.Enqueue(neg_wavepos4);
                    }
                }
            }
            else
            {
                //加一个随机数
                float dev = m_Rnd.NextFloat(0, 0.5f * radius);
                dev = 0;
                for (int i = 0; i < 400; i++) //200
                {
                    //Entity entity = EntityManager.CreateEntity(m_Archetype);
                    float2 wavepos;
                    wavepos = new float2(-border + dev + 2 * i * radius / math.abs(dir.x), 0); //已经正则化成单位向量了
                    if (wavepos.x > (border - radius/ math.abs(dir.x)) ) //不足够放一个负粒子了//是这一行的问题导致的？
                        //if (wavepos.x > border - radius)
                    {
                        break;
                    }

                    wavepos_queue.Enqueue(wavepos);

                    //负粒子
                    float2 neg_wavepos;
                    neg_wavepos = new float2(-border + dev + (2 * i + 1) * radius / math.abs(dir.x), 0);

                    neg_wavepos_queue.Enqueue(neg_wavepos);

                    //垂直dir方向的，每差一个radius
                    float Radius = radius * 0.5f; //*0.9f
                    for (int j = 1; j < 500; j++) //300
                    {
                        float2 wavepos3 = new float2(
                            wavepos.x + Radius * j * dir.y,
                            wavepos.y - Radius * j * dir.x
                        );
                        float2 wavepos4 = new float2(
                            wavepos.x - Radius * j * dir.y,
                            wavepos.y + Radius * j * dir.x
                        );
                        if (math.abs(wavepos3.y) < border)
                        {
                            wavepos_queue.Enqueue(wavepos3);
                        }
                        if (math.abs(wavepos4.y) < border)
                        {
                            wavepos_queue.Enqueue(wavepos4);
                        }
                        float2 neg_wavepos3 = new float2(
                            neg_wavepos.x + Radius * j * dir.y,
                            neg_wavepos.y - Radius * j * dir.x
                        );
                        if (math.abs(neg_wavepos3.y) < border)
                            neg_wavepos_queue.Enqueue(neg_wavepos3);
                        float2 neg_wavepos4 = new float2(
                            neg_wavepos.x - Radius * j * dir.y,
                            neg_wavepos.y + Radius * j * dir.x
                        );
                        if (math.abs(neg_wavepos4.y) < border)
                            neg_wavepos_queue.Enqueue(neg_wavepos4);
                    }
                }
            }
            //有些属性，比如waveDir，是固定的，就可以不用每次都new？
            WaveHeight wh = new WaveHeight { Value = Height };
            WaveSpeed ws = new WaveSpeed { Value = speed };
            WaveDir wd = new WaveDir { Value = dir };
            WaveVector wv = new WaveVector { Value = k };
            Radius wr = new Radius { Value = radius };
            while (wavepos_queue.TryDequeue(out float2 wavepos))
            {
                Entity entity = EntityManager.CreateEntity(m_Archetype);
                EntityManager.SetComponentData(entity, new WavePos { Value = wavepos });
                EntityManager.SetComponentData(entity, wh);
                EntityManager.SetComponentData(entity, ws);
                EntityManager.SetComponentData(entity, wd);
                EntityManager.SetComponentData(entity, wv);
                EntityManager.SetComponentData(entity, wr);
            }
            wavepos_queue.Dispose();
            wh = new WaveHeight { Value = -Height };
            while (neg_wavepos_queue.TryDequeue(out float2 wavepos))
            {
                Entity entity = EntityManager.CreateEntity(m_Archetype);
                EntityManager.SetComponentData(entity, new WavePos { Value = wavepos });
                EntityManager.SetComponentData(entity, wh);
                EntityManager.SetComponentData(entity, ws);
                EntityManager.SetComponentData(entity, wd);
                EntityManager.SetComponentData(entity, wv);
                EntityManager.SetComponentData(entity, wr);
            }
            neg_wavepos_queue.Dispose();
        }

        //-------------------------------------------------------------
        private void UpdateMinParticleHeight()
        {
            int particleCount = m_AllEntitiesQuery.CalculateEntityCountWithoutFiltering();

            // When number of particles is small we can let them live longer
            int subDivNumber = particleCount < 50_000 ? 4 : 3;

            s_WaveParticleMinHeight = c_WaveParticleHeight / math.pow(3, subDivNumber); //如果高度小于这个，那就可以裁剪掉
        }

        //-------------------------------------------------------------
        protected override void OnUpdate()
        {
            UpdateMinParticleHeight();

            m_ExternalDependency.Complete();

            var messageQueue =
                MessageService.Instance.GetOrCreateMessageQueue<ParticleSpawnMessage>();

            // Check if need to add drops to the spawning queue
            m_TimeToDrop -= Time.DeltaTime;
            // Continue until we have available drops to spawn
            while (m_TimeToDrop < 0)
            {
                messageQueue.Enqueue(
                    new ParticleSpawnMessage { Pos = m_Rnd.NextFloat3(-5.0f, 5.0f) }
                );
                // We don't just reset m_TimeToDrop to the m_DropsInterval value,
                // it's done to avoid edge cases when deltaTime grows very big and we miss drops spawning,
                // for that scenario we just increase TimeToDrop and if we still below 0 then spawn agian
                m_TimeToDrop += m_DropsInterval;
            }

            EntityCommandBuffer ecb = m_EndSimECBSystem.CreateCommandBuffer();
            EntityArchetype archetype = m_Archetype;

            Dependency = Job.WithCode(() =>
                {
                    while (messageQueue.TryDequeue(out ParticleSpawnMessage message))
                    {
                        // TODO: change 0.01 for math.EPSILON in future update
                        for (float rot = 0; rot < 2.0f * math.PI - 0.01f; rot += math.PI / 3.0f)
                        {
                            var waveDir = new WaveDir
                            {
                                Value = math.rotate(
                                    quaternion.RotateY(rot),
                                    new float3(1.0f, 0.0f, 0.0f)
                                ).xz
                            };

                            float dispersionAngle = math.PI / 3.0f;

                            // Particle need to be subdivided when gap between two particles become visible
                            // More details on Page 101: http://www.cemyuksel.com/research/waveparticles/cem_yuksel_dissertation.pdf
                            float timeToSubdivide =
                                c_WaveParticleRadius.Data
                                / (2.0f * math.tan(dispersionAngle * 0.5f) * c_WaveParticleSpeed);

                            Entity entity = ecb.CreateEntity(archetype);
                            ecb.SetComponent(entity, new WaveOrigin { Value = message.Pos.xz });
                            ecb.SetComponent(entity, new WavePos { Value = message.Pos.xz });
                            ecb.SetComponent(entity, waveDir);
                            ecb.SetComponent(
                                entity,
                                new WaveHeight { Value = c_WaveParticleHeight }
                            );
                            ecb.SetComponent(entity, new WaveSpeed { Value = c_WaveParticleSpeed });
                            ecb.SetComponent(entity, new DispersAngle { Value = dispersionAngle });
                            ecb.SetComponent(entity, new TimeToSubdiv { Value = timeToSubdivide });
                        }
                    }
                })
                .Schedule(JobHandle.CombineDependencies(Dependency, m_ExternalDependency));

            m_EndSimECBSystem.AddJobHandleForProducer(Dependency);
        }

        //-------------------------------------------------------------
    }
}
