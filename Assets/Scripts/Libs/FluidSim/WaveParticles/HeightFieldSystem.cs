using OneBitLab.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace OneBitLab.FluidSim
{
    unsafe struct Player {
        public void* ptr;//指向一个nativearray的指针
    }
    [UpdateAfter( typeof(WaveMoveSystem) )]
    public class HeightFieldSystem : SystemBase
    {
        //-------------------------------------------------------------
        private RenderTexture m_HeightFieldRT;
        private RenderTexture m_TmpHeightFieldRT;
        private RenderTexture m_TmpHeightFieldRT2;
        private RenderTexture m_TmpHeightFieldRT3;
        private RenderTexture m_TmpHeightFieldRT4;
        private Texture2D     m_HeightFieldTex;
        private Texture2D     m_HeightFieldTex1;
        // private NativeArray<Texture2D> m_HeightFieldTexes;
        private Texture2D[] m_HeightFieldTexes;
        private float L;
        private int sample_count;
        private float sample_interval;
        private Material      m_FilterMat;
        private Material      m_AddMat;
        private Texture2D     texture2D;
        private RenderTexture myRT;

        public const float Border = 5.0f;
        //-------------------------------------------------------------
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_HeightFieldRT    = ResourceLocatorService.Instance.m_HeightFieldRT;
            m_TmpHeightFieldRT = new RenderTexture( m_HeightFieldRT );
            m_TmpHeightFieldRT2 = new RenderTexture( m_HeightFieldRT );
            m_TmpHeightFieldRT2.enableRandomWrite = true;
            m_TmpHeightFieldRT3 = new RenderTexture( m_HeightFieldRT );
            m_TmpHeightFieldRT3.enableRandomWrite = true;
            m_TmpHeightFieldRT4 = new RenderTexture( m_HeightFieldRT );
            m_TmpHeightFieldRT4.enableRandomWrite = true;
            m_HeightFieldTex = new Texture2D( m_HeightFieldRT.width,
                                              m_HeightFieldRT.height,
                                              TextureFormat.RFloat,
                                              mipChain: false,
                                              linear: true );//创建纹理
            m_HeightFieldTex1 = new Texture2D( m_HeightFieldRT.width,
                                              m_HeightFieldRT.height,
                                              TextureFormat.RFloat,
                                              mipChain: false,
                                              linear: true );
            L = 5.1f;
            sample_count = 25;
            sample_interval = L / sample_count;
            Debug.Log("sample_interval" + sample_interval);
            // m_HeightFieldTexes= new NativeArray<Texture2D>( sample_count, Allocator.Temp );
            m_HeightFieldTexes = new Texture2D[sample_count];
            for(int i=0;i<sample_count;i++)
            {
                m_HeightFieldTexes[i]= new Texture2D( m_HeightFieldRT.width,
                                              m_HeightFieldRT.height,
                                              TextureFormat.RFloat,
                                              mipChain: false,
                                              linear: true );
            }//创建纹理
            
            m_FilterMat = new Material( Shader.Find( "FluidSim/WaveFilter_02" ) );
            // m_FilterMat.SetFloat( "_WaveParticleRadius", WaveSpawnSystem.c_WaveParticleRadius.Data );
            
            texture2D = new Texture2D(m_HeightFieldRT.width, m_HeightFieldRT.height, TextureFormat.RGBAFloat, false);
            // myRT= new RenderTexture(m_HeightFieldRT.width, m_HeightFieldRT.height, 0);
            m_AddMat =new Material( Shader.Find( "FluidSim/s_AddFilter" ) );
        }

        //-------------------------------------------------------------
        unsafe protected override void OnUpdate()
        {
            
            NativeArray<float> pixData = m_HeightFieldTex.GetRawTextureData<float>();
            NativeArray<float> pixData1 = m_HeightFieldTex1.GetRawTextureData<float>();
            int pLength=pixData.Length;
            NativeArray<int> counts = new NativeArray<int>(sample_count, Allocator.TempJob);
            // NativeArray<float> pixDatas= new NativeArray<float>(pLength*sample_count, Allocator.TempJob);//
            //Dictionary<int,NativeArray<float>> pixDatas2=new Dictionary<int,NativeArray<float>>();
            //NativeArray<float>[] pixDatas3 = new NativeArray<float>[sample_count];
            //void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(counts);
            NativeArray<Player> pixDatas4 = new NativeArray<Player>(sample_count, Allocator.TempJob);
            NativeArray<float> TmpPixData = new NativeArray<float>(pLength, Allocator.Temp);
            for (int i=0;i<sample_count;i++)
            {
                TmpPixData = m_HeightFieldTexes[i].GetRawTextureData<float>();
                // for (int j = 0; j < pLength; ++j)
                // {
                //     // i * lenght gives the offset of each array because they are the same lenght
                //     pixDatas[i * pLength + j] = TmpPixData[j];
                // }
/*                if(pixDatas2.ContainsKey(i))
                {
                    pixDatas2[i] = TmpPixData;
                }
                else{
                    pixDatas2.Add(i,TmpPixData);
                }*/
                // pixDatas[i*pLength]= m_HeightFieldTexes[i].GetRawTextureData<float>();
                //pixDatas3[i] = TmpPixData;
                Player p = new Player();
                p.ptr = NativeArrayUnsafeUtility.GetUnsafePtr(TmpPixData);
                pixDatas4[i] = p;
            }
            TmpPixData.Dispose();
            int Asample_count=sample_count;
            float Asample_interval=sample_interval;
            // Clear texture color to black//初始化为0，每个时刻一开始都要初始化成0
            Job
                .WithCode( () =>
                {
/*                    for( int i = 0; i < pixData.Length; i++ )
                    {
                        pixData[ i ] = 0;
                        pixData1[ i ] = 0;
                    }*/
                    for(int i=0;i<Asample_count;i++)
                    {
                        Player p = pixDatas4[i];
                        float* TmpPixData = (float*)p.ptr;
                        // int length=TmpPixData.Length;
                        for ( int j = 0; j < pLength; j++ )
                        {
                            TmpPixData[ j ] = 0;
                            // pixDatas[i * pLength + j] = 0;
                            // pixDatas2[i][j]=0;
                        }
                        // pixDatas2[i]=TmpPixData;
                        // Debug.Log("i:"+i+pixDatas2[i][i]);//设0没问题
                        // pixDatas2[i] = pixData;
                    }
                } )
                .Schedule();
/*                .WithoutBurst()
                .Run();*/
            // for(int i=0;i<Asample_count;i++)
            // {
            //     pixDatas2[i] = pixData;
            //     Debug.Log("i:"+i+pixDatas2[i][i]);
            // }
            //NativeArray<float> before = new NativeArray<float>(pLength,Allocator.Temp);;
            //before.CopyFrom(pixDatas2[0]);
            // Debug.Log(before[0]);
            // Bake data we want to capture in the job
            int   w      = m_HeightFieldRT.width;
            int   h      = m_HeightFieldRT.height;
            float border = 5.0f;//那个plane的大小是这么大
            float texelW = 2.0f * border / w;//所以可以判断出采样间隔
            float texelH = 2.0f * border / h;
            // Project all wave particles to texture
            // TODO: split in two jobs, first to create list of all modification (can be parallel)

                Entities
                .ForEach((in WavePos wPos, in WaveHeight height, in Radius radius) =>
                {
                    // Filter all particles which are beyond the border
                    // Also filter particles in the first and last raws of hegihtmap, 
                    // that helps us reduce three "if" statements during anti-aliasing
                    if (math.abs(wPos.Value.x) >= (border - 5.0f * texelW) ||
                        math.abs(wPos.Value.y) >= (border - 5.0f * texelH))//粒子位置超出了范围就忽略
                    {
                        return;
                    }

                    // Make particle positions start from 0,0 coordinates
                    float2 pos = -wPos.Value + border;
                    // Pixel coordinates with fractional parts
                    float xF = pos.x / texelW;
                    float yF = pos.y / texelH;
                    // Texture pixel indices
                    int x = (int)xF;
                    int y = (int)yF;
                    // Interpolation coefficients between texture indices
                    float dX = xF - x;
                    float dY = yF - y;
                    // Indices 
                    int x0y0 = x + y * w;
                    int x1y0 = (x + 1) + y * w;
                    int x0y1 = x + (y + 1) * w;
                    int x1y1 = (x + 1) + (y + 1) * w;

                    // Do manual anti-aliasing for the 2x2 pixel square
                    int index = (int)(radius.Value / Asample_interval);
                    if (radius.Value > (index + 0.5f) * Asample_interval)
                    {
                        index++;
                    }
                    if (index >= Asample_count)
                    {
                        /*index = Asample_count - 1;*/
                        //半径过大的就不管了
                        counts[Asample_count - 1]++;
                    }
                    else //if(index == targetIndex)
                    {
                        //NativeArray<float> TmpPixData = pixDatas2[index];
                        Player p = pixDatas4[index];
                        //float[] tmp = new float[pLength];
                        float* TmpPixData = (float*)p.ptr;
                        //p.ptr;
                        counts[index]++;
                        TmpPixData[x0y0] = TmpPixData[x0y0] + height.Value * (1.0f - dX) * (1.0f - dY);
                        TmpPixData[x1y0] = TmpPixData[x1y0] + height.Value * dX * (1.0f - dY);
                        TmpPixData[x0y1] = TmpPixData[x0y1] + height.Value * (1.0f - dX) * dY;
                        TmpPixData[x1y1] = TmpPixData[x1y1] + height.Value * dX * dY;
                    }
                    /*if(x0y0<0)
                    {
                        Debug.Log("index x0y0" + x0y0+","+x+","+y);
                        Debug.Log("wPos.Value" + wPos.Value);
                    }*/

                })
                /*.WithoutBurst()
                .Run();*/
                .ScheduleParallel();
            

            // We have to wait for all jobs before applying changes to the texture
            Dependency.Complete();
/*            m_HeightFieldTex.Apply();
            m_HeightFieldTex1.Apply();*/
            for(int i=0;i<sample_count;i++)
            {
                // NativeArray<float> TmpPixData = m_HeightFieldTexes[i].GetRawTextureData<float>();
                // TmpPixData = pixDatas2[i];
                // Debug.Log("i"+i+TmpPixData.Equals(pixDatas2[i]));
                if (counts[i] == 0 && i != 0)
                    continue;
                m_HeightFieldTexes[i].Apply();
                //Debug.Log("counts["+i+"]:"+counts[i]);
                // TmpPixData = m_HeightFieldTexes[i].GetRawTextureData<float>();
                // Debug.Log("i"+i+TmpPixData.Equals(pixDatas2[i]));//apply之前一样，之后不一样
            }
            // Debug.Log(before.Equals(pixDatas2[0]));

            // Horizontal filter pass
/*            m_FilterMat.SetFloat( "_WaveParticleRadius", 0.15f );
            m_FilterMat.SetFloat( "_DeltaScale", 0.3f );
            Graphics.Blit( m_HeightFieldTex, m_TmpHeightFieldRT, m_FilterMat, pass: 0 );
            // Graphics.Blit( m_TmpHeightFieldRT, m_HeightFieldRT, m_FilterMat, pass: 1 ); 
            Graphics.Blit( m_TmpHeightFieldRT, m_TmpHeightFieldRT2, m_FilterMat, pass: 1 ); 
            //试一下能不能把2变成RT，然后直接修改到RT 看起来好像不行
            // Graphics.Blit( m_TmpHeightFieldRT, m_HeightFieldRT, m_FilterMat, pass: 1 );
            
            m_FilterMat.SetFloat( "_WaveParticleRadius", 0.25f );
            Graphics.Blit( m_HeightFieldTex1, m_TmpHeightFieldRT, m_FilterMat, pass: 0 );
            Graphics.Blit( m_TmpHeightFieldRT, m_TmpHeightFieldRT3, m_FilterMat, pass: 1 ); 
            
            // Graphics.Blit (texture2D, m_HeightFieldRT);
            m_AddMat.SetTexture( "_MainTex2", m_TmpHeightFieldRT3);
            Graphics.Blit (m_TmpHeightFieldRT2, m_HeightFieldRT,m_AddMat,pass:0);*/

            // Graphics.Blit (m_TmpHeightFieldRT3, m_HeightFieldRT); 
            //texture叠层
            float scale = 2.0f;
            float RR = 0.5f * Asample_interval;
            float Scale = 0.5f - 0.1f * RR;//下面的也要一起改...
            m_FilterMat.SetFloat( "_WaveParticleRadius", RR);
            m_FilterMat.SetFloat( "_DeltaScale", Scale);
            Graphics.Blit( m_HeightFieldTexes[0], m_TmpHeightFieldRT, m_FilterMat, pass: 0 );
            Graphics.Blit( m_TmpHeightFieldRT, m_TmpHeightFieldRT2, m_FilterMat, pass: 1 );
            for(int i=1;i< sample_count; ++i)
            {
                if (counts[i] == 0)
                {
                    continue; 
                }
                RR = i  * Asample_interval;
                m_FilterMat.SetFloat( "_WaveParticleRadius", RR);
                //float Scale = (sample_count - i+2) * scale / sample_count;
                Scale = 0.5f - 0.1f * RR;
                //Scale = 0.0f;
                m_FilterMat.SetFloat( "_DeltaScale", Scale);
                // 半径越小(i越小)，delta越大
                Graphics.Blit( m_HeightFieldTexes[i], m_TmpHeightFieldRT, m_FilterMat, pass: 0 );
                Graphics.Blit( m_TmpHeightFieldRT, m_TmpHeightFieldRT3, m_FilterMat, pass: 1 );
                
                m_AddMat.SetTexture( "_MainTex2", m_TmpHeightFieldRT3);
                Graphics.Blit (m_TmpHeightFieldRT2, m_TmpHeightFieldRT4,m_AddMat,pass:0);
                // m_TmpHeightFieldRT2=m_TmpHeightFieldRT4;
                Graphics.CopyTexture(m_TmpHeightFieldRT4,m_TmpHeightFieldRT2);
                // Graphics.Blit (m_HeightFieldRT, m_HeightFieldRT,m_AddMat,pass:0);
            }
            // m_HeightFieldRT=m_TmpHeightFieldRT2;
            Graphics.CopyTexture(m_TmpHeightFieldRT2,m_HeightFieldRT);

            counts.Dispose();
            pixData.Dispose();
            pixData1.Dispose();
            pixDatas4.Dispose();
           
            // pixDatas.Dispose();//需要dispose
        }

        //-------------------------------------------------------------
        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            // Clear height field render texture
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = m_HeightFieldRT;
            GL.Clear( false, true, Color.clear );
            RenderTexture.active = currentRT;
            // delete [] m_HeightFieldTexes;
        }

        //-------------------------------------------------------------
    }
}