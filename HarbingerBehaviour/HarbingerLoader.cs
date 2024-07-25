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

namespace HarbingerBehaviour
{ 
    [BepInPlugin(GUID, NAME, VERSION)]
    public class HarbingerLoader : BaseUnityPlugin
    {
        private Harmony harmony = new Harmony(GUID);
        const string GUID = "InstanceWorld.Harbinger";
        const string NAME = "Harbinger Scripts";
        const string VERSION = "1.0.0";

        public static Config HarbConfig;

        private static HarbingerLoader Inst;
        internal static ManualLogSource mls;

        public static AssetBundle HarbBundle;

        public static int SpawnWeight = 25;

        private static EnemyType VoidHarbinger;


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
            
            if(VoidHarbinger == null)
            {
                mls.LogError("EnemyType Missing...");
            }
            
            harmony.PatchAll(typeof(HarbingerAI));
            harmony.PatchAll(typeof(TeleportRing));
            harmony.PatchAll(typeof(Config));

            HarbConfig = new Config(base.Config);
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

            VoidHarbinger.MaxCount = SyncedInstance<Config>.Instance.MaxCount.Value;

            if (TerminalKeyword == null)
            {
                mls.LogError("TerminalNode Missing...");
            }
            if (TNode == null)
            {
                mls.LogError("Keyword Missing...");
            }

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(VoidHarbinger.enemyPrefab);
             
            var (SpawnRateLevelType, SpawnRateCustom) = ConfigParsing(SyncedInstance<Config>.Instance.HarbingerSpawnLocations.Value);
            
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
            return (spawnRateByLevelType: spawnRateByLevelType, spawnRateByCustomLevelType: spawnRateByCustomLevelType);
        }


    }
}
