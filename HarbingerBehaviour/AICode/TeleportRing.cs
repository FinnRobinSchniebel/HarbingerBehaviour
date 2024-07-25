using DunGen;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.AI;
using HarmonyLib;

namespace HarbingerBehaviour.AICode
{
    internal class TeleportRing : MonoBehaviour
    {

        public GameObject Container;

        public AudioSource FinishSound;

        //public GameObject SelectedEnemy;

        public EnemyAI Teleport;

        public Vector3 EnemyLocation;


        public void setup(EnemyAI ToTeleport, Vector3 Location)
        {
            
            Teleport = ToTeleport;
            transform.position = Location;
            
        }

        public void CompleteTeleport()
        {

            EnemyLocation = Teleport.transform.position;
            TeleportCreature(Teleport);

            LayerMask layerMask = LayerMask.GetMask("Player");
            Collider[] playersInRange = Physics.OverlapCapsule(Container.transform.position, Container.transform.position, 3f, layerMask);
            
            foreach (Collider c in playersInRange)
            {
                if (c.CompareTag("Player") && c.GetComponent<PlayerControllerB>().IsOwner)
                {
                    NavMeshHit hit = new NavMeshHit();
                    float maxDistance = 2.0f;

                    int tries = 0;
                    while (!hit.hit && tries < 15)
                    {
                        //Vector3 target = EnemyLocation + new Vector3(UnityEngine.Random.Range(.01f, .5f), 0, UnityEngine.Random.Range(.01f, .5f));
                        Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * UnityEngine.Random.Range(1, 2);
                        Vector3 TpPos = EnemyLocation + new Vector3(RandomPoint.x, 0, RandomPoint.y);

                        NavMesh.SamplePosition(TpPos, out hit, maxDistance, NavMesh.AllAreas);
                        tries++;
                    }
                    if (hit.hit)
                    {
                        StartCoroutine(TeleportPlayer(c, hit.position));
                    }
                    
                }
            }

        }

        //Avoid spawning on the creature before teleport
        private IEnumerator TeleportPlayer(Collider c, Vector3 pos)
        {
            yield return new WaitForSeconds(.1f);
            c.GetComponent<PlayerControllerB>().TeleportPlayer(pos, true);
        }

        public void VisualEffectsActive()
        {
            Container.SetActive(true);

        }

        public void BurstEffects()
        {
            if (FinishSound)
            {
                FinishSound.Play();
            }
            StartCoroutine(Deactivate());
        }

        private IEnumerator Deactivate()
        {
            yield return new WaitForSeconds(.2f);
            Container.SetActive(false);
        }

        public void Cancel()
        {
            Container.SetActive(false);
        }

        public void CreatureConfigure()
        {
            if(Teleport is CentipedeAI)
            {
                ((CentipedeAI)Teleport).SwitchToBehaviourServerRpc(2);
            }
            else
            {
                Teleport.agent.Warp(transform.position);
            }
        }

       public void TeleportCreature(EnemyAI ai)
        {

            
            Vector3 centeroffset = new Vector3();
            Vector3[] SlimeBones = new Vector3[8];
            //  prefix/prep
            if (ai is BlobAI)
            {
                centeroffset = ((BlobAI)ai).centerPoint.position - ((BlobAI)ai).transform.position;
                for (int i =0; i < ((BlobAI)ai).SlimeRaycastTargets.Length; i++)
                {
                   SlimeBones[i] = ((BlobAI)ai).SlimeBones[i].position - ((BlobAI)ai).centerPoint.position;                    
                }
                
            }

            if (Teleport.IsOwner)
            {
                //HarbingerLoader.mls.LogInfo("Teleporting " + Teleport.gameObject.name);
                Teleport.agent.Warp(transform.position);
            }
            //  postfix
            if (ai is BlobAI)
            {
                ((BlobAI)ai).centerPoint.position = ((BlobAI)ai).transform.position + centeroffset;
                for (int i = 0; i < ((BlobAI)ai).SlimeRaycastTargets.Length; i++)
                {
                    SlimeBones[i] = ((BlobAI)ai).centerPoint.position + SlimeBones[i];
                }
                Traverse.Create((BlobAI)ai).Field("SlimeBonePositions").SetValue(SlimeBones);
            }
        }

    }
}
