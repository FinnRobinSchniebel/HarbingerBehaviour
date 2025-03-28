﻿using BepInEx.Configuration;
using GameNetcodeStuff;
using HarbingerBehaviour.ConfigSync;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using static UnityEngine.GraphicsBuffer;

namespace HarbingerBehaviour.AICode
{
    internal class HarbingerAI : EnemyAI
    {
        public enum HarbingerStates
        {
            Moving,
            Standing,
            Teleporting,
            TeleportingSelf,
            TeleportItem,
            stunned,
        }

        private Vector3 mainEntrancePosition; //usefull later when I add the crown (maybe)

        public float MaxDistanceToPlayerBeforeTeleport = 20f;

        public HarbingerStates state = HarbingerStates.Standing;

        public float movementspeed = 5f;

        private System.Random enemyRandom;

        public Vector2 MinMaxMovement = new Vector2(5, 10);

        public GameObject debugPoint;

        public GameObject NextPos;

        public float stationaryTime = 2f;

        public float ActionStart = 0;

        public float MovementTimeout = 6f;

        private NavMeshPath nPath;

        [Header("Teleport Other")]
        public TeleportRing tpRing;
        public float TPOtherCooldown = 15f;
        
        private float TPOtherInitCooldown;
        private System.Random TeleportRandom;
        private bool TeleportValid = false;
        private bool aligned = false;
        private Quaternion lookRotation;
        
        
        [Header("TeleportSelf")]
        public Vector2 TeleportSelfArriveRadius = new Vector2(4, 6);
        public float SelfTeleportCooldown = 40f;
        public AudioSource PositionBeforeSelfTeleport;
        private float lastTeleportTime = 0.0f;
        private List<EnemyAI> Alreadyused = new List<EnemyAI>();
        private float IntialCooldown;
        private bool TPSelfBehaviourOverridable = false;


        [Header("TeleportApparatus")]
        public GrabbableObject LungProp;
        public float ResetToStealCooldown = 5;
        public float StealCooldown = 5;
        private static List<GrabbableObject> grabbableObjectsInMap = new List<GrabbableObject>();
        private float startTeleporItemTimer = 0;

        [Header("Stuned")]
        public Material StunnMaterial;
        private float LastAnimationSpeed = 0;
        private Material[] TempMaterial;
        public VisualEffect[] EffectsToPause;
        public Light[] LightsToTurnOff;
        public Rigidbody RigidStun;
        public CapsuleCollider RigidCollider;
        public Vector3 prestunTransform;
        public Quaternion prestunRot;
        public SkinnedMeshRenderer Smr;
        public GameObject harbingerMapNode;

        [Header("HarbingerFractures")]
        public GameObject FracturGameObject;
        public bool CanCreateFractures = true;
        //public bool CanDie = true;
        public List<SpaceFractureEnemy> FractureRef;
        public int DestroyedFractures = 0;
        public int NumberOfFracturesToKill = 3;
        public int AllowedSimultaneousFractures = 3;
        //public bool ReduceAllowedPerDestroyed = true;
        int MaxHealth = 3;

        [Header("Extra Configurations")]
        public bool RandomTeleport = false;
        public bool teleportOnContact = true;
        public bool CanTeleportSelf = true;



        public override void Start()
        {

            base.Start();
            movingTowardsTargetPlayer = true;
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            TeleportRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            mainEntrancePosition = RoundManager.FindMainEntrancePosition();
            ActionStart = Time.time;
            NumberOfFracturesToKill = enemyHP;
            lastTeleportTime = Time.time;
            PositionBeforeSelfTeleport.transform.SetParent(this.transform.transform.parent);
            tpRing.transform.SetParent(this.transform.transform.parent);
            tpRing.HarbingerOwner = this;
            nPath = new NavMeshPath();
            MaxHealth = enemyHP;
            rigidDisableClientRpc();
            RefreshGrabbableObjectsInMapList();

            enemyHP = SyncedInstance<Config>.Instance.HarbingerHealth.Value;

            if (!RoundManager.Instance.IsHost)
            {
                return;
            }

            HarbingerLoader.mls.LogWarning("Testing TP Speed: " + SyncedInstance<Config>.Instance.TPSelfCooldown.Value);
            HostConfigApply(SyncedInstance<Config>.Instance.TeleportSpeed.Value, Math.Max(SyncedInstance<Config>.Instance.TPSelfCooldown.Value, 5), Math.Max(SyncedInstance<Config>.Instance.TPOtherCooldown.Value, 5));
            

            
        }

        [ClientRpc]
        public void rigidDisableClientRpc()
        {
            RigidStun.isKinematic = true;
            
            RigidStun.useGravity = false;
            
            prestunTransform = RigidStun.transform.localPosition;
            prestunRot = RigidStun.transform.localRotation;

        }

        public static void RefreshGrabbableObjectsInMapList()
        {
            grabbableObjectsInMap.Clear();
            GrabbableObject[] array = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
            HarbingerLoader.mls.LogInfo($"objects in scene!! : {array.Length}");
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].grabbableToEnemies && array[i].GetComponent<LungProp>() != null)
                {
                    grabbableObjectsInMap.Add(array[i].GetComponent<GrabbableObject>());
                }
            }
            HarbingerLoader.mls.LogInfo($"Picked : {grabbableObjectsInMap.Count}");
        }

        public void HostConfigApply(float tpSpeedMult, float teleportSelfCooldown, float teleportOthersCooldown)
        {
            creatureAnimator.SetFloat("TeleportMultipier", tpSpeedMult);
            //cooldowns
            SelfTeleportCooldown = teleportSelfCooldown;
            IntialCooldown = SelfTeleportCooldown;

            TPOtherCooldown = teleportOthersCooldown;
            TPOtherInitCooldown = TPOtherCooldown;

        }


        public override void OnDestroy()
        {

            Destroy(tpRing.gameObject);
            Destroy(PositionBeforeSelfTeleport.gameObject);
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }

            while (FractureRef.Count > 0)
            {
                FractureRef[0].KillEnemy(true);
                FractureRef.Remove(FractureRef[0]);
            }

        }

        public override void DoAIInterval()
        {
            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }
            if (StartOfRound.Instance.livingPlayers == 0)
            {
                if (moveTowardsDestination)
                {
                    agent.SetDestination(destination);
                }
                return;
            }
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }
            if (state == HarbingerStates.stunned || isEnemyDead)
            {
                return;
            }

            PlayerControllerB ClosestPlayer = GetClosestPlayer(false, true, true);

            testInPickupDistance();

            if (LungProp != null)
            {
                HarbingerLoader.mls.LogInfo("Has Appi In Options");
            }

            if (HarbingerLoader.HarbConfig.CanTPItems.Value && state != HarbingerStates.TeleportingSelf && state != HarbingerStates.Teleporting && StealCooldown == 0 && LungProp != null && !LungProp.GetComponent<LungProp>().isHeld)
            {
                HarbingerLoader.mls.LogWarning("Meet Requirnments");
                if (state != HarbingerStates.TeleportItem)
                {
                    NewStateClientRpc(HarbingerStates.TeleportItem);
                }
                if (startTeleporItemTimer != 0)
                {
                    agent.speed = 0;
                }
                else
                {
                    agent.speed = movementspeed + 2;
                }
                if (Vector3.Distance(transform.position, LungProp.transform.position) < 4)
                {
                    agent.speed = 0f;
                    TeleportItem();

                }
                
            }
            else if(state == HarbingerStates.TeleportItem)
            {

                NewStateClientRpc(HarbingerStates.Standing);
            }

            


            //only enter if the player is not teleporting something else and the cooldown is finished
            if (state != HarbingerStates.Teleporting && SelfTeleportCooldown == 0 && CanTeleportSelf)
            {
                
                //self Teleport if a player is to far away & in the facility
                if (ClosestPlayer != null && ClosestPlayer.isInsideFactory && ClosestPlayer.isInsideFactory && Vector3.Distance(ClosestPlayer.transform.position, transform.position) > MaxDistanceToPlayerBeforeTeleport)
                {
                    if(state != HarbingerStates.TeleportingSelf)
                    {
                        NewStateClientRpc(HarbingerStates.TeleportingSelf);
                    }
                    
                }
                //teleport if no player is in the facility and you get a 1 in 3
                else if (ClosestPlayer == null || !ClosestPlayer.isInsideFactory)
                {
                    if (state != HarbingerStates.TeleportingSelf && UnityEngine.Random.Range(0, 2) == 1)
                    {
                        NewStateClientRpc(HarbingerStates.TeleportingSelf);
                    }
                    else // add cooldown
                    {
                        SelfTeleportCooldown += 5;
                    }
                }
                //interrupt teleport if player is too close and a player is in the facility
                else if(TPSelfBehaviourOverridable && state == HarbingerStates.TeleportingSelf)
                {
                    //cancel teleport if a player reentered the area
                    TPSelfBehaviourOverridable = false;
                    StopCoroutine(TeleportSelf());
                    NewStateClientRpc(HarbingerStates.Moving);
                }   
                //failsafe
                else
                {
                    SelfTeleportCooldown += 5;
                }

            }
            //test teleport other if you arnt teleporting
            if(state != HarbingerStates.TeleportingSelf && state != HarbingerStates.TeleportItem && TPOtherCooldown == 0)
            {
                //teleport other
                if (ClosestPlayer != null && ClosestPlayer.isInsideFactory && Vector3.Distance(ClosestPlayer.transform.position, transform.position) < MaxDistanceToPlayerBeforeTeleport)
                {
                    if (state != HarbingerStates.Teleporting)
                    {
                        NewStateClientRpc(HarbingerStates.Teleporting);
                    }
                }
                // only cancel the teleport if it hasnt been started yet
                else if(!TeleportValid && state == HarbingerStates.Teleporting)
                {
                    //cancel teleport if a player reentered the area
                    StopCoroutine(PrepTeleportOther());
                    NewStateClientRpc(HarbingerStates.Moving);
                }
                else
                {
                    TPOtherCooldown += 5;
                }
            }

        }

        public void testInPickupDistance()
        {
            float lowest = float.MaxValue;
            foreach(var g in grabbableObjectsInMap)
            {
                float dist = Vector3.Distance(g.transform.position, transform.position);
                if (dist < 15 && lowest > dist)
                {
                    lowest = dist;
                    LungProp = g;                    
                }
            }
            if (lowest >= float.MaxValue-1)
            {
                LungProp = null;
            }
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead)
            {
                return;
            }
            if(StealCooldown > 0)
            {
                StealCooldown = Math.Max(0, StealCooldown - Time.deltaTime);
            }

            if(SelfTeleportCooldown > 0)
            {
                SelfTeleportCooldown = Math.Max(0, SelfTeleportCooldown - Time.deltaTime);
            }
            if (TPOtherCooldown > 0)
            {
                TPOtherCooldown = Math.Max(0, TPOtherCooldown - Time.deltaTime);
            }
            if(startTeleporItemTimer > 0)
            {
                startTeleporItemTimer = Math.Max(0, startTeleporItemTimer - Time.deltaTime);
            }
            if(stunNormalizedTimer > 0f && state != HarbingerStates.stunned)
            {
                NewStateClientRpc(HarbingerStates.stunned);
                HarbingerLoader.mls.LogWarning("In possition");
            }
            
            if(state != HarbingerStates.stunned && ( Vector3.Distance(RigidStun.transform.localPosition, prestunTransform) > 0.001f || Quaternion.Angle(RigidStun.transform.localRotation, prestunRot) > 0.001f))
            {               
                RigidStun.transform.localPosition = Vector3.MoveTowards(RigidStun.transform.localPosition, prestunTransform, .5f*Time.deltaTime);
                RigidStun.transform.localRotation = Quaternion.RotateTowards(RigidStun.transform.localRotation, prestunRot, 120f * Time.deltaTime);
            }


            switch (state) 
            { 
                case HarbingerStates.Standing:
                    if(ActionStart + stationaryTime < Time.time)
                    {
                        NewStateClientRpc(HarbingerStates.Moving);
                    }
                    break;
                case HarbingerStates.Moving:
                    if(Vector3.Distance(transform.position, destination) < .5f || MovementTimeout + ActionStart < Time.time)
                    {
                        NewStateClientRpc(HarbingerStates.Standing);
                    }
                    break;                
                case HarbingerStates.Teleporting:
                    if (TeleportValid)
                    {
                        /*if(ActionStart + 20f < Time.time && )
                        {
                            HarbingerLoader.mls.LogError("TeleportTimeout switching behaviour");
                            CancelTeleportClientRpc();
                            
                            NewStateClientRpc(HarbingerStates.Standing);
                        }*/

                        if(!aligned && Math.Abs(agent.transform.eulerAngles.y - lookRotation.eulerAngles.y) < 5f)
                        {
                            aligned = true;
                            HarbingerLoader.mls.LogWarning("In possition");
                            StartTeleportEffectsClientRpc();
                            SwitchToBehaviourState(2);

                        }
                        else
                        {
                            //HarbingerLoader.mls.LogError("Rotating?");
                            agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation, lookRotation, Math.Abs(180f) * Time.deltaTime);
                        }
                        
                    }
                    break;
                case HarbingerStates.TeleportingSelf:
                    break;
                case HarbingerStates.stunned:
                    if(stunNormalizedTimer <= 0f)
                    {
                        
                        NewStateClientRpc(HarbingerStates.Standing);
                    }
                    
                    break;
                default:
                    break;

            }


        }

        

        public void TeleportItem()
        {
            if(LungProp is LungProp)
            {

                LungProp L = ((LungProp)LungProp);
                if (L.isLungDocked)
                {
                    EquipAppiClientRpc(L.NetworkObjectId);
                }
                L.ChangeOwnershipOfProp(NetworkManager.LocalClient.ClientId);

                int nextLoc = TeleportRandom.Next(allAINodes.Length);
                bool canReach = false;
                int attempts = 0;
                while (!canReach && attempts < 10)
                {
                    if (agent.CalculatePath(allAINodes[nextLoc].transform.position, nPath))
                    {
                        canReach = true;
                    }
                    else
                    {
                        nextLoc = TeleportRandom.Next(allAINodes.Length);
                        attempts++;
                    }                    
                }                
                if(attempts == 10)
                {
                    HarbingerLoader.mls.LogWarning("Can't TP the Item");
                }
                if(canReach)
                {
                    TPItemClientRpc(L.NetworkObjectId, nextLoc);
                }
                

                //L.startFallingPosition = base.transform.parent.InverseTransformPoint(newLoc);
                //L.targetFloorPosition = base.transform.parent.InverseTransformPoint(newLoc);
                /*L.EnablePhysics(enable: false);
                
                L.EnablePhysics(enable: true);*/
                

            }
            NewStateClientRpc(HarbingerStates.Standing);
        }

        [ClientRpc]
        public void EquipAppiClientRpc(ulong Item)
        {
            LungProp l = (NetworkManager.Singleton.SpawnManager.SpawnedObjects[Item]).GetComponent<LungProp>();
            
            if (l.isLungDocked)
            {
                var TargetPrivate = AccessTools.Method(typeof(LungProp), "DisconnectFromMachinery");
                StartCoroutine((IEnumerator)TargetPrivate.Invoke(l, null));
                l.isLungDocked = false;
            }
            
        }

        [ClientRpc]
        public void TPItemClientRpc(ulong Item, int Location)
        {
            NetworkObject Obj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[Item];
            LungProp L =  Obj.GetComponent<LungProp>();
            Vector3 newLoc = allAINodes[Location].transform.position;
            L.transform.position = newLoc;
            L.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
            L.EnablePhysics(enable: true);
            L.fallTime = 0f;
            L.startFallingPosition = L.transform.parent.InverseTransformPoint(newLoc);
            L.targetFloorPosition = L.transform.parent.InverseTransformPoint(L.GetItemFloorPosition());
            L.floorYRot = -1;
            L.FallToGround();
        }


        [ClientRpc]
        public void NewStateClientRpc(HarbingerStates newstate)
        {
            HarbingerLoader.mls.LogInfo("NewState " + newstate);

            if(state == HarbingerStates.TeleportItem && newstate != state)
            {
                StealCooldown = ResetToStealCooldown;
            }

            if(state == HarbingerStates.Teleporting && newstate != state)
            {
                aligned = false;
                TeleportValid = false;
                Alreadyused.Clear();
            }
            if(newstate == HarbingerStates.stunned)
            {
                if(state == HarbingerStates.Teleporting && tpRing.enabled == true)
                {
                    tpRing.Cancel();
                }
            }
            if(state == HarbingerStates.stunned && newstate != HarbingerStates.stunned)
            {
                StunClientRpc(false);
            }
            
            state = newstate;
            ActionStart = Time.time;

            switch (state)
            {
                case HarbingerStates.Standing:
                    //HarbingerLoader.mls.LogInfo("Standing");
                    agent.speed = 0f; 
                    SwitchToBehaviourState(0);
                    break;
                case HarbingerStates.Moving:

                    NavMeshHit h = FindMoveLocation(transform.position, MinMaxMovement, 50, 1, true, 2.5f);

                    SetDestinationToPosition(h.position);
                    agent.speed = movementspeed;
                    SwitchToBehaviourState(1);
                    break;
                case HarbingerStates.Teleporting:
                    agent.speed = 0f;
                    if (!RoundManager.Instance.IsHost)
                    {
                        break;
                    }
                    StartCoroutine(PrepTeleportOther());
                    break;
                case HarbingerStates.TeleportingSelf:
                    agent.speed = 0f;                    
                    StartCoroutine(TeleportSelf());                   

                    break;
                case HarbingerStates.TeleportItem:
                    SwitchToBehaviourState(4);
                    startTeleporItemTimer = 0.5f;
                    agent.speed = movementspeed + 2;
                    if (!RoundManager.Instance.IsHost)
                    {
                        return;
                    }
                    //NavMeshHit h = FindMoveLocation(transform.position, MinMaxMovement, 50, 1);
                    Ray r = new Ray(LungProp.transform.position, Vector3.down);
                    RaycastHit Rhit;
                    Vector3 pos = LungProp.transform.position;
                    if (Physics.Raycast(r, out Rhit, 7f))
                    {
                        pos = Rhit.point;
                    }

                    NavMeshHit hit;
                    if(NavMesh.SamplePosition(pos, out hit, 10, NavMesh.AllAreas))
                    {
                        HarbingerLoader.mls.LogInfo("Found Destination");
                        SetDestinationToPosition(hit.position);
                    }
                    else
                    {
                        HarbingerLoader.mls.LogWarning("Cant Find Location");
                        NewStateClientRpc(HarbingerStates.Standing);
                    }
                    
                    break;
                case HarbingerStates.stunned:
                    StunClientRpc(true);                    
                    break;
                default:
                    break;

            }
        }

        [ClientRpc]
        public void StunClientRpc(bool isStunned)
        {
            if (isStunned)
            {
                HarbingerLoader.mls.LogInfo("Harbinger stunned for: " + (stunNormalizedTimer * enemyType.stunTimeMultiplier));
                LastAnimationSpeed = creatureAnimator.speed;
                creatureAnimator.speed = 0f;
                agent.speed = 0;
                Material[] TempMat = Smr.materials;
                TempMaterial = new Material[TempMat.Length];
                TempMat.CopyTo(TempMaterial, 0);
                TempMat[0] = StunnMaterial;
                Smr.materials = TempMat;

                eye.GetComponent<MeshRenderer>().enabled = false;
                EffectsToPause[2].Reinit();
                foreach (VisualEffect e in EffectsToPause)
                {
                    e.Stop();
                }
                
                foreach( Light l in LightsToTurnOff)
                {
                    l.enabled = false;
                }
                if (harbingerMapNode != null)
                {
                    harbingerMapNode.GetComponent<MeshRenderer>().enabled = false;
                }
                
                //rigid
                prestunTransform = RigidStun.transform.localPosition;
                prestunRot = RigidStun.transform.localRotation;
                
                RigidStun.isKinematic = false;                     
                RigidStun.useGravity = true;
                

                StopAllCoroutines();
            }
            else
            {
                agent.speed = movementspeed;
                Smr.materials = TempMaterial;
                //corrects the material if a fractur was destroyed during a stun
                if(Smr.materials != null)
                {
                    Smr.materials[0].SetFloat("_Damage", 1f - (float)enemyHP / MaxHealth);
                }

                eye.GetComponent<MeshRenderer>().enabled = true;
                if (harbingerMapNode != null)
                {
                    harbingerMapNode.GetComponent<MeshRenderer>().enabled = true;
                }
                foreach (VisualEffect e in EffectsToPause)
                {
                    e.Play();
                }
                foreach (Light l in LightsToTurnOff)
                {
                    l.enabled = true;
                }
                creatureAnimator.speed = LastAnimationSpeed;
                //rigid
                //RigidStun.transform.localPosition = prestunTransform;
                //RigidStun.transform.localRotation = prestunRot;



                RigidStun.isKinematic = true;                
                RigidStun.useGravity = false;
                StartCoroutine(RepositionTimeout());
                
            }
            
        }

        private IEnumerator RepositionTimeout()
        {
            yield return new WaitForSeconds(15f);
            if((Vector3.Distance(RigidStun.transform.localPosition, prestunTransform) > 0.001f)){
                RigidStun.transform.localPosition = prestunTransform;
            }
        }

        private IEnumerator TeleportSelf()
        {
            creatureAnimator.SetTrigger("CheckingTeleport");
            TPSelfBehaviourOverridable = true;
            yield return new WaitForSeconds(2f);
            
            List<Transform> validTeleportPlayers = new List<Transform>();
            //add all players inside facility
            foreach (PlayerControllerB a in StartOfRound.Instance.allPlayerScripts)
            {
                if (a.isInsideFactory == true && !a.isPlayerDead)
                {
                    validTeleportPlayers.Add(a.transform);
                }
            }

            if (RoundManager.Instance.IsHost)
            {
                Vector3 tpLocation;
                
                if (validTeleportPlayers.Count != 0)
                {
                    tpLocation = validTeleportPlayers[TeleportRandom.Next(validTeleportPlayers.Count)].position;
                    
                }
                else if(allAINodes.Length > 0)
                {
                    //if no player is alive you teleport somewhere random to avoid it signaling players being alive.
                    tpLocation = allAINodes[UnityEngine.Random.Range(0, allAINodes.Length)].transform.position;
                }
                else
                {
                    //if no nodes are available teleport to the same location you are in.
                    tpLocation = this.transform.position;
                }
                NavMeshHit h = FindMoveLocation(tpLocation, TeleportSelfArriveRadius, 15, 1, true, 2f);
                if (h.hit)
                {
                    Vector3 LastPos = transform.position;
                    Quaternion LastRot = transform.rotation;
                    LastLocationUpdateClientRpc(transform.position); //, validTeleportPlayers[player].NetworkObjectId
                    SwitchToBehaviourState(3);
                    yield return new WaitForSeconds(.1f);
                    agent.Warp(h.position);
                    lastTeleportTime = Time.time;
                    SelfTeleportCooldown = IntialCooldown;
                    if(TPOtherCooldown < 1f)
                    {
                        TPOtherCooldown = 1f;
                    }
                    if (CanCreateFractures)
                    {
                        CreateFracture(LastPos, LastRot, transform.position);
                    }
                    
                }
                
            }
            TPSelfBehaviourOverridable = false;
            yield return new WaitForSeconds(.3f);
            NewStateClientRpc(HarbingerStates.Moving);
        }


        [ClientRpc]
        public void LastLocationUpdateClientRpc(Vector3 newLocation) //ulong targetPlayer
        {
            PositionBeforeSelfTeleport.transform.position = newLocation;
            if (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                PositionBeforeSelfTeleport.Play();
            }
            
        }
        

        private IEnumerator PrepTeleportOther()
        {
            creatureAnimator.SetTrigger("CheckingTeleport");
            //ensure this is only done by the server
            if (!RoundManager.Instance.IsHost)
            {
                yield break;
            }

            yield return new WaitForSeconds(.5f);
            List<EnemyAI> validEnemies = new List<EnemyAI>(); 
            foreach(EnemyAI Ai in RoundManager.Instance.SpawnedEnemies){
                if (Ai.isOutside == this.isOutside && !Ai.isEnemyDead && Vector3.Distance(Ai.transform.position, transform.position) > 20f && MonsterBlackList(Ai)) 
                {
                    validEnemies.Add(Ai);
                }
            }

            if(validEnemies.Count == 0)
            {
                HarbingerLoader.mls.LogInfo("No valid Targets");
                TPOtherCooldown += 5;
                NewStateClientRpc(HarbingerStates.Standing);
                yield break;
            }

            EnemyAI a = validEnemies[TeleportRandom.Next(validEnemies.Count)];
            Alreadyused.Add(a);
                        
            NavMeshHit h = FindMoveLocation(transform.position, MinMaxMovement, 15, 1);

            if (!h.hit)
            {
                NewStateClientRpc(HarbingerStates.Standing);
                yield break;
            }


            //give all clients the same enemy and location
            SettUpTPClientRpc(a.NetworkObjectId, h.position);


            Vector3 RotateToTeleportionDirection = (tpRing.transform.position - agent.transform.position).normalized;
            RotateToTeleportionDirection.y = 0;

            lookRotation = Quaternion.LookRotation(RotateToTeleportionDirection);

            TeleportValid = true;
        }

        [ClientRpc]
        public void SettUpTPClientRpc(ulong EnemyNetID, Vector3 loc)
        {
            NetworkObject en = NetworkManager.Singleton.SpawnManager.SpawnedObjects[EnemyNetID];
            tpRing.setup(en.GetComponent<EnemyAI>(), loc);
        }

        public NavMeshHit FindMoveLocation(Vector3 SourceLocation, Vector2 distanceRange, int maxTries = 15, float maxDistance = 2, bool hasVertical = false, float VerticalAmount = 0)
        {

            NavMeshHit hit = new NavMeshHit();
            int tries = 0;
            bool success = false;
            while (!success && tries < maxTries)
            {
                //Vector3 target = EnemyLocation + new Vector3(UnityEngine.Random.Range(.01f, .5f), 0, UnityEngine.Random.Range(.01f, .5f));
                Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * distanceRange;
                float Vertical = 0;
                if (hasVertical)
                {
                    Vertical = UnityEngine.Random.Range(-VerticalAmount, VerticalAmount);
                }
                Vector3 TpPos = SourceLocation + new Vector3(RandomPoint.x, Vertical, RandomPoint.y);

                NavMesh.SamplePosition(TpPos, out hit, maxDistance, NavMesh.AllAreas);
                                
                if (hit.hit && allAINodes.Length != 0 && NavMesh.CalculatePath(hit.position, ChooseClosestNodeToPosition(hit.position, false, 0).position, agent.areaMask, nPath))
                {
                    success = true;
                }    
                else if(hit.hit && allAINodes.Length != 0)
                {
                    HarbingerLoader.mls.LogWarning("Harbinger cannot find any AI nodes to test movement against");
                    success = true;
                }


                tries++;
            }
            return hit;
        }

        //called by animation
        public void TeleportEvent()
        {
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }
            lastTeleportTime = Time.time;
            TPOtherCooldown += TPOtherInitCooldown;
            TeleportClientRpc();
            StartCoroutine(EndTP());

        }

        [ClientRpc]
        public void TeleportClientRpc()
        {
            tpRing.CompleteTeleport();
            tpRing.BurstEffects();
        }

        private IEnumerator EndTP()
        {
            yield return new WaitForSeconds(1.5f);
            NewStateClientRpc(HarbingerStates.Moving);
        }

        [ClientRpc]
        public void StartTeleportEffectsClientRpc()
        {
            tpRing.VisualEffectsActive();
        }
        
        [ClientRpc]
        public void CancelTeleportClientRpc()
        {
            tpRing.Cancel();
        }

        


        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force,playerWhoHit,playHitSFX, hitID);
            if (playerWhoHit == null || !playerWhoHit.IsOwner)
            {
                return;
            }
            if (RandomTeleport)
            {
               RandomLocationTeleport(playerWhoHit);
            }
            else
            {
                PlayerToMonsterTeleport(playerWhoHit);
            }

            /*List<EnemyAI> validEnemies = new List<EnemyAI>();
            foreach (EnemyAI Ai in RoundManager.Instance.SpawnedEnemies)
            {
                if (Ai.isOutside == this.isOutside && !Ai.isEnemyDead && Vector3.Distance(Ai.transform.position, transform.position) > 10f)
                {
                    validEnemies.Add(Ai);
                }
            }
            if(validEnemies.Count > 0)
            {
                
                Vector3 enemypos = validEnemies[UnityEngine.Random.Range(0, validEnemies.Count - 1)].transform.position;
                Ray r = new Ray(enemypos, Vector3.down);
                RaycastHit Rhit;
                
                if (Physics.Raycast(r, out Rhit, 5f))
                {
                    enemypos = Rhit.point;
                }

                NavMeshHit hit = new NavMeshHit();
                int tries = 0;
                while (!hit.hit && tries < 10)
                {
                    Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * UnityEngine.Random.Range(3, 6);
                    Vector3 TpPos = enemypos + new Vector3(RandomPoint.x, 0, RandomPoint.y);
                    //HarbingerLoader.mls.LogInfo("Pos: " + TpPos);
                    NavMesh.SamplePosition(TpPos, out hit, .5f, NavMesh.AllAreas);
                    HarbingerLoader.mls.LogInfo("Nav Pos: " + hit.position);

                    tries++;
                }
                if(tries < 10)
                {
                    playerWhoHit.TeleportPlayer(hit.position, false);
                }
                else
                {
                    playerWhoHit.TeleportPlayer(ChooseFarthestNodeFromPosition(mainEntrancePosition).position, false);
                }

            }
            else
            {
                playerWhoHit.TeleportPlayer(ChooseFarthestNodeFromPosition(mainEntrancePosition).position, false);
            }*/
            
        }

        public static bool MonsterBlackList(EnemyAI toTest)
        {
            if(toTest is SpaceFractureEnemy)
            {
                return false;
            }
            if (!HarbingerLoader.WhitelistNames.Contains("none"))
            {
                return MonsterWhiteList(toTest);
            }
            if (toTest.agent == null)
            {
                return false;
            }
            if (toTest == null)
            {
                return false;
            }
            else if (toTest is DressGirlAI || toTest is CentipedeAI || toTest is SandSpiderAI)
            {
                return false;
            }
            else if(toTest is CaveDwellerAI && (((CaveDwellerAI)toTest).holdingBaby || ((CaveDwellerAI)toTest).isOutside))
            {
                return false;
            }
            else if ( HarbingerLoader.BlacklistNames.Contains( StandardiesMonsterNames(toTest.enemyType.enemyName) ) )
            { 
                return false;
            }
            else
            {
                HarbingerLoader.mls.LogMessage("Teleport BL Result is good: " + toTest.enemyType.enemyName);
            }
            return true;
        }
        private static bool MonsterWhiteList(EnemyAI toTest)
        {
            if ( HarbingerLoader.WhitelistNames.Contains( StandardiesMonsterNames(toTest.enemyType.enemyName) ) )
            {
                HarbingerLoader.mls.LogMessage("Teleport WL Result is good: " + toTest.enemyType.enemyName);
                return true;
            }
            return false;
        }
        private static string StandardiesMonsterNames(String Monster)
        {
            return Monster.ToLower().Trim();
        }




        ///Section for Fractures
        ///
        

        public void CreateFracture(Vector3 position, Quaternion rotation, Vector3 ExitPosition)
        {
            //check distance from all others
            foreach( SpaceFractureEnemy f in FractureRef)
            {
                if (Vector3.Distance(position, f.transform.position) < 10f)
                {
                    return;
                }
            }
            //check if max has been reached
            if(AllowedSimultaneousFractures <= FractureRef.Count() && FractureRef.Count() != 0)
            {
                FractureRef[0].thisNetworkObject.Despawn(true);
                FractureRef.RemoveAt(0);
            }
            //create fractur, change its parent
            GameObject fractObj = Instantiate(FracturGameObject, position, rotation, this.transform.parent);
            fractObj.GetComponent<NetworkObject>().Spawn();

            SpaceFractureEnemy SpaceFrac = fractObj.GetComponent<SpaceFractureEnemy>();
            SpaceFrac.OwnedBy = this;
            SpaceFrac.ExitPosition = ExitPosition;
            FractureRef.Add(SpaceFrac);

        }

        public void FracturDestroyed(SpaceFractureEnemy DestroyedFracture)
        {
            
            
            FractureRef.Remove(DestroyedFracture);
            damageEffectClientRpc();

            //AllowedSimultaneousFractures -= 1;
            HarbingerLoader.mls.LogMessage("Harbinger Took damage: Destroyed: " + DestroyedFractures + "/"+ NumberOfFracturesToKill);
            if (DestroyedFractures >= NumberOfFracturesToKill)
            {
                KillHarby();
            }

        }

        [ClientRpc]
        private void damageEffectClientRpc()
        {
            DestroyedFractures += 1;
            enemyHP--;
            if (state != HarbingerStates.stunned && stunNormalizedTimer <= 0f && enemyType.canDie)
            {
                Smr.materials[0].SetFloat("_Damage", 1f - (float)enemyHP / MaxHealth);
            }
        }

        public void KillHarby()
        {
            if (!enemyType.canDie)
            {
                return;
            }

            HarbingerLoader.mls.LogMessage("Harbinger has died");
            KillEnemyServerRpc(false);

            if (state == HarbingerStates.Teleporting)
            {
                endEffectsClientRPC();
            }

            StunClientRpc(true);
            // destroy all other fractures
            while(FractureRef.Count > 0)
            {
                FractureRef[0].KillEnemy(true);
                FractureRef.Remove(FractureRef[0]);
            }
        }
        [ClientRpc]
        public void endEffectsClientRPC()
        {
            tpRing.Cancel();
        }




        public void PlayerTouched(PlayerControllerB  other)
        {
            if (RoundManager.Instance.IsHost && teleportOnContact && state != HarbingerStates.stunned)
            {
                if (RandomTeleport)
                {
                    RandomLocationTeleport(other);
                }
                else
                {
                    PlayerToMonsterTeleport(other);
                }
                
            }
        }

        public void RandomLocationTeleport(PlayerControllerB ToTP)
        {
            if (!RoundManager.Instance.IsHost)
            {
                return;
            }
            int tries = 5;
            while (tries > 0)
            {


                int nextLoc = TeleportRandom.Next(allAINodes.Length);
                Vector3 ExitPosition = allAINodes[nextLoc].transform.position;

                NavMeshHit hit = FindMoveLocation(ExitPosition, new Vector2(.5f, 1f), 5);

                if (hit.hit)
                {
                    if (ToTP.GetComponent<PlayerControllerB>() != null)
                    {
                        TeleportPlayerClientRPC(ToTP.playerClientId, hit.position);
                        //ToTP.GetComponent<PlayerControllerB>().TeleportPlayer(hit.position, true);
                    }
                    /*else if(ToTP.GetComponent<EnemyAI>() != null)
                    {
                        ToTP.GetComponent<EnemyAI>().TeleportPlayer(hit.position, true);
                    }*/
                    break;
                }

                tries--;
            }

            
        }
        public void PlayerToMonsterTeleport(PlayerControllerB ToTP)
        {
            HarbingerLoader.mls.LogInfo("Player teleport triggured");
            List<EnemyAI> validEnemies = new List<EnemyAI>();
            foreach (EnemyAI Ai in RoundManager.Instance.SpawnedEnemies)
            {
                if (Ai.isOutside == this.isOutside && !Ai.isEnemyDead && Vector3.Distance(Ai.transform.position, transform.position) > 10f)
                {
                    validEnemies.Add(Ai);
                }
            }
            if (validEnemies.Count > 0)
            {

                Vector3 enemypos = validEnemies[UnityEngine.Random.Range(0, validEnemies.Count - 1)].transform.position;
                Ray r = new Ray(enemypos, Vector3.down);
                RaycastHit Rhit;

                if (Physics.Raycast(r, out Rhit, 5f))
                {
                    enemypos = Rhit.point;
                }

                NavMeshHit hit = new NavMeshHit();
                int tries = 0;
                while (!hit.hit && tries < 10)
                {
                    Vector2 RandomPoint = UnityEngine.Random.insideUnitCircle.normalized.normalized * UnityEngine.Random.Range(3, 6);
                    Vector3 TpPos = enemypos + new Vector3(RandomPoint.x, 0, RandomPoint.y);
                    //HarbingerLoader.mls.LogInfo("Pos: " + TpPos);
                    NavMesh.SamplePosition(TpPos, out hit, .5f, NavMesh.AllAreas);
                    HarbingerLoader.mls.LogInfo("Nav Pos: " + hit.position);

                    tries++;
                }
                if (tries < 10)
                {
                    TeleportPlayerClientRPC(ToTP.playerClientId, hit.position);
                    //ToTP.TeleportPlayer(hit.position, false);
                }
                else
                {

                    TeleportPlayerClientRPC(ToTP.playerClientId, ChooseFarthestNodeFromPosition(mainEntrancePosition).position);
                    
                }

            }
            else
            {
                TeleportPlayerClientRPC(ToTP.playerClientId, ChooseFarthestNodeFromPosition(mainEntrancePosition).position);
            }
        }


        [ClientRpc]
        public void TeleportPlayerClientRPC(ulong playernum, Vector3 pos)
        {
            //Vector3 pos = new Vector3(1,2,3);
            PlayerControllerB Player = StartOfRound.Instance.allPlayerScripts[playernum];
            if (!Player.IsOwner)
            {
                return;
            }
            
            Player.GetComponent<PlayerControllerB>().TeleportPlayer(pos, true);
        }
    }
}
