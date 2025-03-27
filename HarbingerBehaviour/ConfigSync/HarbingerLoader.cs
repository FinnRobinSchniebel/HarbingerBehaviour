using BepInEx;
using BepInEx.Logging;
using HarbingerBehaviour.AICode;
using HarmonyLib;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using LethalLib.Modules;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Assertions;
using HarbingerBehaviour.Items;

namespace HarbingerBehaviour.ConfigSync
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class HarbingerLoader : BaseUnityPlugin
    {
        private Harmony harmony = new Harmony(GUID);
        const string GUID = "InstanceWorld.Harbinger";
        const string NAME = "Harbinger Scripts";
        const string VERSION = "1.0.0";

        public static Config HarbConfig;

        public static HarbingerLoader Inst;
        internal static ManualLogSource mls;

        public static AssetBundle HarbBundle;

        public static int SpawnWeight = 25;

        public static EnemyType VoidHarbinger;
        public static GameObject HarbingerFractur;
        public static Item RealityFragment;

        public static string[] BlacklistNames;
        public static string[] WhitelistNames;

        void Awake()
        {
            if (Inst == null)
            {
                Inst = this;
            }
            mls = BepInEx.Logging.Logger.CreateLogSource(GUID);
            mls.LogInfo("Starting HarbingerPatcher...");

            HarbBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "harbinger"));

            VoidHarbinger = HarbBundle.LoadAsset<EnemyType>("Assets/LethalCompany/Mods/Harbinger/Harbinger_enemyType.asset");
            HarbingerFractur = HarbBundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/Harbinger/HarbingerFracture.prefab");
            RealityFragment = HarbBundle.LoadAsset<Item>("Assets/LethalCompany/Mods/Harbinger/RealityFractureItem.asset");


            if (VoidHarbinger == null)
            {
                mls.LogError("Harbinger Missing...");
            }
            if (HarbingerFractur == null)
            {
                mls.LogError("Fracture Missing...");
            }
            if (RealityFragment == null)
            {
                mls.LogError("Fragment Missing...");
            }

            harmony.PatchAll(typeof(HarbingerAI));
            harmony.PatchAll(typeof(SpaceFractureEnemy));
            harmony.PatchAll(typeof(TeleportRing));
            harmony.PatchAll(typeof(Config));

            HarbConfig = new Config(Config);
            SetharbValues();
            mls.LogInfo("Patching Networking...");


            //networking
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);

                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            mls.LogInfo("Completed Harbinger Patcher");

        }

        private void SetharbValues()
        {
            TerminalNode TNode = HarbBundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/Harbinger/HarbingerFile.asset");
            TerminalKeyword TerminalKeyword = HarbBundle.LoadAsset<TerminalKeyword>("Assets/LethalCompany/Mods/Harbinger/Harbinger.asset");

            

            if (TerminalKeyword == null)
            {
                mls.LogError("TerminalNode Missing...");
            }
            if (TNode == null)
            {
                mls.LogError("Keyword Missing...");
            }
            //VoidHarbinger.MaxCount = SyncedInstance<Config>.Instance.MaxCount.Value;
            //VoidHarbinger.canDie = SyncedInstance<Config>.Instance.HarbingerCanDie.Value;

            /*HarbingerAI harbingerAI = VoidHarbinger.enemyPrefab.GetComponent<HarbingerAI>();
            //harbingerAI.enemyHP = SyncedInstance<Config>.Instance.HarbingerHealth.Value;
            harbingerAI.enemyHP = SyncedInstance<Config>.Instance.HarbingerHealth.Value;
            harbingerAI.CanTeleportSelf = SyncedInstance<Config>.Instance.CanTeleportSelf.Value;
            harbingerAI.RandomTeleport = SyncedInstance<Config>.Instance.TeleportRandom.Value;
            harbingerAI.teleportOnContact = SyncedInstance<Config>.Instance.TpOnHarbingerTouch.Value;

            harbingerAI.CanCreateFractures = SyncedInstance<Config>.Instance.CanCreateFractures.Value;
            harbingerAI.AllowedSimultaneousFractures = SyncedInstance<Config>.Instance.SimultaneousFractures.Value;
            harbingerAI.CanTeleportSelf = SyncedInstance<Config>.Instance.CanTeleportSelf.Value;

            SpaceFractureEnemy spaceFractureEnemy = HarbingerFractur.GetComponent<SpaceFractureEnemy>();
            spaceFractureEnemy.NeededStunDuration = SyncedInstance<Config>.Instance.StunDuration.Value;
            spaceFractureEnemy.enemyType.stunGameDifficultyMultiplier = SyncedInstance<Config>.Instance.ShockDifficulty.Value;
            spaceFractureEnemy.dropChance = SyncedInstance<Config>.Instance.RealityFractureChance.Value;

            RealityFragment.minValue = SyncedInstance<Config>.Instance.MinFractureValue.Value;
            RealityFragment.maxValue = SyncedInstance<Config>.Instance.MaxFractureValue.Value;
            RealityFragment.spawnPrefab.GetComponent<RealityFracture>().useCooldown = SyncedInstance<Config>.Instance.FractureCooldown.Value;
            RealityFragment.spawnPrefab.GetComponent<RealityFracture>().DamagePlayer = SyncedInstance<Config>.Instance.FractureDamageDelt.Value;*/

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(VoidHarbinger.enemyPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(HarbingerFractur);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RealityFragment.spawnPrefab);
            Utilities.FixMixerGroups(RealityFragment.spawnPrefab);

            var (SpawnRateLevelType, SpawnRateCustom) = ConfigParsing(SyncedInstance<Config>.Instance.HarbingerSpawnLocations.Value);
            BlacklistNames = ParseBlacklist(SyncedInstance<Config>.Instance.BlackList.Value);
            WhitelistNames = ParseBlacklist(SyncedInstance<Config>.Instance.WhiteList.Value);



            if (!WhitelistNames.Contains("none"))
            {
                mls.LogWarning("WhiteList active. Blacklist Disabled");
            }

            Enemies.RegisterEnemy(VoidHarbinger, SpawnRateLevelType, SpawnRateCustom, TNode, TerminalKeyword);


        }
        private static (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) ConfigParsing(string configMoonRarity)
        {
            Dictionary<Levels.LevelTypes, int> spawnRateByLevelType = new Dictionary<Levels.LevelTypes, int>();
            Dictionary<string, int> spawnRateByCustomLevelType = new Dictionary<string, int>();
            foreach (string item in from s in configMoonRarity.Split(',')
                                    select s.Trim())
            {
                string[] entryParts = item.Split(':');
                if (entryParts.Length != 2)
                {
                    continue;
                }
                string name = entryParts[0];
                if (int.TryParse(entryParts[1], out var spawnrate))
                {
                    if (Enum.TryParse<Levels.LevelTypes>(name, ignoreCase: true, out var levelType))
                    {
                        spawnRateByLevelType[levelType] = spawnrate;
                        mls.LogDebug($"Registered spawn rate for level type {levelType} to {spawnrate}");
                    }
                    else
                    {
                        spawnRateByCustomLevelType[name] = spawnrate;
                        mls.LogDebug($"Registered spawn rate for custom level type {name} to {spawnrate}");
                    }
                }
            }
            return (spawnRateByLevelType, spawnRateByCustomLevelType);
        }
        private static string[] ParseBlacklist(string list)
        {
            string[] l = list.Split(",");

            for (int i = 0; i < l.Length; i++)
            {
                l[i] = l[i].ToLower().Trim();
            }
            return l;
        }

    }
}
