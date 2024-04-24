﻿using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitLab.FluidSim
{
    static class BringYourOwnDelegate
    {
        // Declare the delegate that takes 12 parameters. T0 is used for the Entity argument
        [Unity.Entities.CodeGeneratedJobForEach.EntitiesForEachCompatible]
        public delegate void CustomForEachDelegate<T0, T1, T2, T3, T4, T5, T6, T7, T8>
            (T0 t0, T1 t1, ref T2 t2, ref T3 t3, ref T4 t4, in T5 t5,
            in T6 t6, in T7 t7, in T8 t8);

        // Declare the function overload
        public static TDescription ForEach<TDescription, T0, T1, T2, T3, T4, T5, T6, T7, T8>
            (this TDescription description, CustomForEachDelegate<T0, T1, T2, T3, T4, T5, T6, T7, T8> codeToRun)
            where TDescription : struct, Unity.Entities.CodeGeneratedJobForEach.ISupportForEachWithUniversalDelegate =>
            LambdaForEachDescriptionConstructionMethods.ThrowCodeGenException<TDescription>();
    }
    // This system handle subdivide phase of simulation
    // Once time for next subdivision run out we create two new neighboring particles and update current one
    // We also calculate time of next subdivision based on the new dispersion angle
    // The higher the dispersion angle the faster particles move away from each other

    [UpdateAfter( typeof(WaveSpawnSystem) )]
    public class WaveSubdivideSystem : SystemBase
    {
        //-------------------------------------------------------------
        private WaveSpawnSystem                        m_WaveSpawnSystem;
        private EndSimulationEntityCommandBufferSystem m_EndSimECBSystem;

        //-------------------------------------------------------------
        protected override void OnCreate()
        {
            m_WaveSpawnSystem = World.GetOrCreateSystem<WaveSpawnSystem>();
            m_EndSimECBSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        //-------------------------------------------------------------
        protected override void OnUpdate()
        {
            EntityCommandBuffer.ParallelWriter createECB = m_EndSimECBSystem.CreateCommandBuffer().AsParallelWriter();
            EntityCommandBuffer.ParallelWriter deleteECB = m_EndSimECBSystem.CreateCommandBuffer().AsParallelWriter();
            EntityArchetype                    archetype = m_WaveSpawnSystem.WaveArchetype;

            float       dTime        = Time.DeltaTime;
            float cWPRadius    = WaveSpawnSystem.c_WaveParticleRadius.Data;
            const float cWPSpeed     = WaveSpawnSystem.c_WaveParticleSpeed;
            float       cWPMinHeight = WaveSpawnSystem.s_WaveParticleMinHeight;

            Entities
                .ForEach( ( Entity           entity,
                            int              entityInQueryIndex,
                            ref TimeToSubdiv tts,
                            ref WaveHeight   height,
                            ref DispersAngle angle,
                            in  WaveDir      waveDir,
                            in  WavePos      wavePos,
                            in  WaveOrigin   waveOrigin,
                            in  WaveSpeed    waveSpeed ) =>
                {
                    tts.Value -= dTime;

                    if( tts.Value > 0 )
                    {
                        // Particle is not ready for subdivision
                        return;
                    }
                    
                    if(math.abs(height.Value) < cWPMinHeight )
                    {
                        // Particle reached min height, schedule it for deletion 高度过小就剔除掉
                        deleteECB.DestroyEntity( entityInQueryIndex, entity );
                        return;
                    }

                    float newAngle  = angle.Value  / 3.0f;
                    float newHeight = height.Value / 3.0f;
                    // Particle need to be subdivided when gap between two particles become visible
                    // More details on Page 101: http://www.cemyuksel.com/research/waveparticles/cem_yuksel_dissertation.pdf
                    float totalSubdivDistance = cWPRadius / ( 3.0f * math.tan( newAngle * 0.5f ) );
                    float distanceTraveled    = math.distance( waveOrigin.Value, wavePos.Value );
                    float distanceToSubdiv    = totalSubdivDistance - distanceTraveled;
                    float timeToSubdiv        = distanceToSubdiv / waveSpeed.Value;

                    // Create left particle
                    float2 leftWaveDir = math.rotate(
                                                 quaternion.RotateY( -newAngle ),
                                                 new float3( waveDir.Value.x, 0, waveDir.Value.y ) )
                                             .xz;
                    var leftWavePos = new WavePos {Value = waveOrigin.Value + leftWaveDir * distanceTraveled};

                    Entity leftEntity = createECB.CreateEntity( entityInQueryIndex, archetype );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, leftWavePos );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, waveOrigin );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, new WaveDir {Value      = leftWaveDir} );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, new WaveHeight {Value   = newHeight} );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, new WaveSpeed {Value    = waveSpeed.Value} );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, new DispersAngle {Value = newAngle} );
                    createECB.SetComponent( entityInQueryIndex, leftEntity, new TimeToSubdiv {Value = timeToSubdiv} );

                    // Create right particle
                    float2 rightWaveDir = math.rotate(
                                                  quaternion.RotateY( newAngle ),
                                                  new float3( waveDir.Value.x, 0, waveDir.Value.y ) )
                                              .xz;
                    var rightWavePos = new WavePos {Value = waveOrigin.Value + rightWaveDir * distanceTraveled};

                    Entity rightEntity = createECB.CreateEntity( entityInQueryIndex, archetype );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, rightWavePos );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, waveOrigin );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, new WaveDir {Value      = rightWaveDir} );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, new WaveHeight {Value   = newHeight} );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, new WaveSpeed {Value    = waveSpeed.Value} );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, new DispersAngle {Value = newAngle} );
                    createECB.SetComponent( entityInQueryIndex, rightEntity, new TimeToSubdiv {Value = timeToSubdiv} );

                    // Modify current particle
                    height.Value = newHeight;
                    angle.Value  = newAngle;
                    tts.Value    = timeToSubdiv;
                } )
                .ScheduleParallel();

            m_EndSimECBSystem.AddJobHandleForProducer( Dependency );
        }

        //-------------------------------------------------------------
    }
}