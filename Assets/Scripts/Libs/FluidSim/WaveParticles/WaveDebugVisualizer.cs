﻿using OneBitLab.Services;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace OneBitLab.FluidSim
{
    public class WaveDebugVisualizer : MonoBehaviour
    {
        //-------------------------------------------------------------
        private NativeQueue<WaveDebugData> m_DebugQueue;
        private Vector3 WorldCenterOfMass = Vector3.zero;
        //-------------------------------------------------------------
        private void Awake()
        {
            m_DebugQueue = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<WaveDebugSystem>().m_DebugQueue;
        }

        //-------------------------------------------------------------
        private void OnDrawGizmos()
        {
            WorldCenterOfMass = ResourceLocatorService.Instance.WorldCOM;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(WorldCenterOfMass, 0.05f);
            Handles.Label(WorldCenterOfMass, "WorldCoM");
            if ( !m_DebugQueue.IsCreated ) return;
            // Debug.Log( "hi" );
            while( m_DebugQueue.TryDequeue( out WaveDebugData data ) )
            {
                
                var     center       = new Vector3( data.Pos.x, 0, data.Pos.y );
                var     origin       = new Vector3( data.Origin.x, 0, data.Origin.y );
                var     dir          = new Vector3( data.Dir.x, 0, data.Dir.y );
                Vector3 reflectPoint = center + WaveSpawnSystem.c_WaveParticleSpeed * data.TimeToReflect * dir;

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere( center, 0.1f );
                Gizmos.color = Color.red;
                Gizmos.DrawLine(center, reflectPoint);
                Gizmos.DrawWireSphere(reflectPoint, 0.02f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(origin, 0.02f);
                Gizmos.DrawLine(origin, center);
                // Debug.Log( origin );
            }
            
        }

        //-------------------------------------------------------------
    }
}