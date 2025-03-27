using GameNetcodeStuff;
using HarbingerBehaviour.ConfigSync;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

namespace HarbingerBehaviour.AICode
{
    internal class SpaceFractureEnemy : EnemyAI
    {
        public HarbingerAI OwnedBy;
        public GameObject TouchCollider;
        //public float stunDifficulty = 1.5f;
        public Vector3 ExitPosition;
        public Transform[] visualPlanes;
        public float NeededStunDuration = 5f;
        float TotalTimeStunned = 0.0f;
        
        public GameObject[] FractureDrop;
        public float dropChance = .3f;

        bool dying = false;
        /*bool isStunned = false;
        PlayerControllerB stunnedBy;
        bool LastStunState = false;*/
        private System.Random FracturRand;


        public Vector3 shockPosition;


        public override void Start() { 
            base.Start();
            FracturRand = new System.Random();
        }

        public override void Update()
        {
            if (stunnedIndefinitely <= 0)
            {
                if (stunNormalizedTimer >= 0f)
                {
                    stunNormalizedTimer -= Time.deltaTime / enemyType.stunTimeMultiplier;
                }
                else
                {
                    stunnedByPlayer = null;
                    if (postStunInvincibilityTimer >= 0f)
                    {
                        postStunInvincibilityTimer -= Time.deltaTime * 5f;
                    }
                }
            }

            if (!RoundManager.Instance.IsHost || dying)
            {
                return;
            }

            //if stunned
            if(stunNormalizedTimer + TotalTimeStunned >= NeededStunDuration)
            {
                TotalTimeStunned += stunNormalizedTimer;
            }
            if(stunNormalizedTimer > 0f)
            {

                TotalTimeStunned += Time.deltaTime;
                
                //HarbingerLoader.mls.LogWarning("Total stun: " + TotalTimeStunned);
                ShockVisualEffectsClientRpc();
            }
            
            
            if (TotalTimeStunned > NeededStunDuration)
            {
                OwnedBy.FracturDestroyed(this);
                DestroyFractureSequance();
            }
        }

        [ClientRpc]
        private void ShockVisualEffectsClientRpc()
        {
            foreach(Transform g in visualPlanes)
            {
                if(eye.GetComponent<VisualEffect>() != null)
                {
                    eye.GetComponent<VisualEffect>().Play();
                }
                g.GetComponent<MeshRenderer>().materials[0].SetFloat("_size", 1f - TotalTimeStunned/NeededStunDuration);
            }
        }

        public void DestroyFractureSequance()
        {
            dying = true;
            DeathEffectClientRpc();


            if(FractureDrop.Length != 0 && dropChance != 0f &&  (float)FracturRand.NextDouble() <= dropChance)
            {
                GameObject fractObj = Instantiate(FractureDrop[0], transform.position, transform.rotation, this.transform.parent);
                fractObj.GetComponent<NetworkObject>().Spawn();
                GrabbableObject grabbable = fractObj.GetComponent<GrabbableObject>();
                int value = (int)(RoundManager.Instance.AnomalyRandom.Next(grabbable.itemProperties.minValue, grabbable.itemProperties.maxValue));

                //StartCoroutine(wait());
                setPriceClientRpc(value, fractObj.GetComponent<NetworkObject>());

            }
            

            StartCoroutine(DestroyDelayed());
        }

        private IEnumerator wait()
        {
            yield return new WaitForSeconds(.5f);
        }

        [ClientRpc]
        public void setPriceClientRpc(int value, NetworkObjectReference NetRef)
        {
            if(NetRef.TryGet(out var networkObject))
            {
                networkObject.GetComponent<GrabbableObject>().SetScrapValue(value);
            }
            
        }

        [ClientRpc]
        private void DeathEffectClientRpc()
        {
            StartCoroutine(KillEffects());
        }
        private IEnumerator KillEffects()
        {
            yield return new WaitForSeconds(.2f);

            
        }
        private IEnumerator DestroyDelayed()
        {
             
            yield return new WaitForSeconds(.2f);
            //HarbingerLoader.mls.LogWarning("stunnedby: " + stunnedByPlayer+ " is in mini: " + stunnedByPlayer.inShockingMinigame +  "item: " + stunnedByPlayer.currentlyHeldObjectServer);
            if (stunnedByPlayer != null && stunnedByPlayer.currentlyHeldObjectServer != null)
            {
                HarbingerLoader.mls.LogInfo("Stopping zap-gun");
                //stunnedByPlayer.currentlyHeldObjectServer.GetComponent<PatcherTool>().StopShockingAnomalyOnClient();
                stopShockClientRPC();
            }


            KillEnemyServerRpc(true);



        }

        [ClientRpc]
        public void stopShockClientRPC()
        {
            if(stunnedByPlayer.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                stunnedByPlayer.currentlyHeldObjectServer.GetComponent<PatcherTool>().StopShockingAnomalyOnClient();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && OwnedBy != null)
            {
                HarbingerLoader.mls.LogInfo("Teleport Triggured on fracture");
                NavMeshHit hit = OwnedBy.FindMoveLocation(ExitPosition, new Vector2(.5f, 1f), 25);

                if (hit.hit)
                {
                    //other.GetComponent<PlayerControllerB>().TeleportPlayer(hit.position, true);
                    TeleportPlayerClientRPC(other.GetComponent<PlayerControllerB>().playerClientId, hit.position);
                }
            }

        }


        [ClientRpc]
        public void TeleportPlayerClientRPC(ulong playernum, Vector3 pos)
        {
            //Vector3 pos = new Vector3(1,2,3);
            PlayerControllerB Player = StartOfRound.Instance.allPlayerScripts[playernum];
            Player.GetComponent<PlayerControllerB>().TeleportPlayer(pos, true);
        }
    }
}




/*bool IShockableWithGun.CanBeShocked()
       {
           return true;
       }

       Vector3 IShockableWithGun.GetShockablePosition()
       {
           if (shockPosition != null)
           {
               return shockPosition;
           }
           return base.transform.position + Vector3.up * 0.5f;
       }

       float IShockableWithGun.GetDifficultyMultiplier()
       {
           return stunDifficulty;
       }

       void IShockableWithGun.ShockWithGun(PlayerControllerB shockedByPlayer)
       {
           mainScript.SetEnemyStunned(setToStunned: true, 0.25f, shockedByPlayer);
           mainScript.stunnedIndefinitely++;
       }

       Transform IShockableWithGun.GetShockableTransform()
       {
           return base.transform;
       }

       NetworkObject IShockableWithGun.GetNetworkObject()
       {
           return mainScript.NetworkObject;
       }

       void IShockableWithGun.StopShockingWithGun()
       {
           mainScript.stunnedIndefinitely = Mathf.Clamp(mainScript.stunnedIndefinitely - 1, 0, 100);
       }
       */
