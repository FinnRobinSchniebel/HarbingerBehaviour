using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace HarbingerBehaviour.ShaderBS
{
    public class ShaderTimeSync:MonoBehaviour
    {
        public Material[] SyncedMats;

        public void Update()
        {
            //myMaterial.SetFloat("SyncedTime", Time.time);
            //Shader.SetGlobalFloat("SyncedTime", Time.time);
            foreach(Material mat in SyncedMats)
            {
                //obj.GetComponent<Material>().SetFloat("_SyncedTime", Time.time);
                mat.SetFloat("_SyncedTime", Time.time);
            }
        }
    }
}
