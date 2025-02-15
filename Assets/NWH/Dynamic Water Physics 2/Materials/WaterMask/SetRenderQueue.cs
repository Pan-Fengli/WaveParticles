﻿/*
	SetRenderQueue.cs
 
	Sets the RenderQueue of an object's materials on Awake. This will instance
	the materials, so the script won't interfere with other renderers that
	reference the same materials.
*/

using UnityEngine;

namespace NWH.DWP2
{
    [AddComponentMenu("Rendering/SetRenderQueue")]
    public class SetRenderQueue : MonoBehaviour
    {
        public int queue;


        protected void Awake()
        {
            Material[] materials = GetComponent<Renderer>().materials;
            for (int i = 0; i < materials.Length; ++i)
            {
                materials[i].renderQueue = queue;
            }
        }
    }
}