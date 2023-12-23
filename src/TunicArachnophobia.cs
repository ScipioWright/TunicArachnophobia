using UnityEngine;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace TunicArachnophobia {

    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class TunicArachnophobia : BasePlugin {

        public static ManualLogSource Logger;

        public static Dictionary<string, GameObject> Enemies = new Dictionary<string, GameObject>() { };
        public static Dictionary<string, List<string>> DefeatedEnemyTracker = new Dictionary<string, List<string>>();
        public static string CurrentSceneName;
        public static Dictionary<string, string> EnemiesInCurrentScene = new Dictionary<string, string>() { };

        public override void Load() {
            Logger = Log;
            Logger.LogInfo(PluginInfo.NAME + " v" + PluginInfo.VERSION + " loaded!");

            Harmony Harmony = new Harmony(PluginInfo.GUID);

            Harmony.Patch(AccessTools.Method(typeof(SceneLoader), "OnSceneLoaded"), null, new HarmonyMethod(AccessTools.Method(typeof(TunicArachnophobia), "SceneLoader_OnSceneLoaded_PostfixPatch")));
            //Harmony.Patch(AccessTools.Method(typeof(PlayerCharacter), "Start"), null, new HarmonyMethod(AccessTools.Method(typeof(TunicArachnophobia), "PlayerCharacter_Start_PostfixPatch")));
            Harmony.Patch(AccessTools.Method(typeof(PlayerCharacter), "Update"), null, new HarmonyMethod(AccessTools.Method(typeof(TunicArachnophobia), "PlayerCharacter_Update_PostfixPatch")));
            Harmony.Patch(AccessTools.Method(typeof(Monster._Die_d__77), "MoveNext"), null, new HarmonyMethod(AccessTools.Method(typeof(TunicArachnophobia), "Monster_Die_MoveNext_PostfixPatch")));
            Harmony.Patch(AccessTools.Method(typeof(Campfire), "RespawnAtLastCampfire"), new HarmonyMethod(AccessTools.Method(typeof(TunicArachnophobia), "Campfire_RespawnAtLastCampfire_PrefixPatch")));
        }

        public static void SceneLoader_OnSceneLoaded_PostfixPatch(Scene loadingScene, LoadSceneMode mode, SceneLoader __instance) {
            EnemiesInCurrentScene.Clear();
            string sceneName = loadingScene.name;
            CurrentSceneName = sceneName;
            // Initial loads to grab enemies
            if (sceneName == "Quarry Redux" && !Enemies.ContainsKey("Scavenger Miner")) {
                InitializeEnemy("Scavenger Miner", "_Monsters (DAY)/Scavenger_miner");

                SceneLoader.LoadScene("TitleScreen");
                return;
            }
            if (sceneName == "East Forest Redux" && !Enemies.ContainsKey("Rudeling") && !Enemies.ContainsKey("Rudeling Shield")) {
                InitializeEnemy("Rudeling", "_MONSTERS DAY/Skuladot redux");
                InitializeEnemy("Rudeling Shield", "_MONSTERS DAY/Skuladot redux_shield");

                SceneLoader.LoadScene("Quarry Redux");
                return;
            }
            if (sceneName == "TitleScreen" && Enemies.Count == 0) {

                SceneLoader.LoadScene("East Forest Redux");
                return;
            }

            // Loads while game is actually being played

            if (sceneName == "Fortress Reliquary") { 
                if (GameObject.Find("_Night Encounters") != null) {
                    GameObject.Find("_Night Encounters").SetActive(false);
                }
                if (GameObject.Find("_Day Monsters") != null) {
                    GameObject.Find("_Day Monsters").SetActive(true);
                }
            }
            List<GameObject> SceneEnemies = Resources.FindObjectsOfTypeAll<GameObject>().Where(Monster => Monster.GetComponent<Monster>() != null).ToList();
            int i = 0;
            if (sceneName == "East Forest Redux") {
                foreach (GameObject Enemy in SceneEnemies) {
                    if (Enemy.GetComponent<Spider>() != null) {
                        ReplaceEnemy(Enemy, "Rudeling");
                        i++;
                    }
                }
            } else if (sceneName == "Fortress Basement") {
                foreach (GameObject Enemy in SceneEnemies) {
                    if (Enemy.GetComponent<Spider>() != null) {
                        if (Enemy.name.Contains("Big")) {
                            ReplaceEnemy(Enemy, "Rudeling Shield");
                        } else {
                            ReplaceEnemy(Enemy, "Rudeling");
                        }
                        i++;
                    }
                }
            } else if (sceneName == "ziggurat2020_3") {
                foreach (GameObject Enemy in SceneEnemies) {
                    if (Enemy.GetComponent<Voidling>() != null) {
                        ReplaceEnemy(Enemy, "Scavenger Miner");
                        i++;
                    }
                }
            } else {
                foreach (GameObject Enemy in SceneEnemies) {
                    if (Enemy.GetComponent<Spider>() != null || Enemy.GetComponent<Voidling>() != null) {
                        ReplaceEnemy(Enemy, "Rudeling");
                        i++;
                    }
                }
            }

            void ReplaceEnemy(GameObject oldEnemy, string newEnemy) {
                GameObject NewEnemy = Object.Instantiate(Enemies[newEnemy]);
                NewEnemy.transform.position = oldEnemy.transform.position;
                NewEnemy.transform.rotation = oldEnemy.transform.rotation;
                NewEnemy.transform.parent = oldEnemy.transform.parent;
                NewEnemy.name += $" {i}";
                int MaxId = 0;
                foreach (RuntimeStableID id in Resources.FindObjectsOfTypeAll<RuntimeStableID>()) {
                    if (id.intraSceneID > MaxId) {
                        MaxId = id.intraSceneID;
                    }
                }
                NewEnemy.GetComponent<RuntimeStableID>().intraSceneID = MaxId + i;
                NewEnemy.SetActive(true);
                EnemiesInCurrentScene.Add(NewEnemy.name, NewEnemy.transform.position.ToString());
                GameObject.Destroy(oldEnemy);
            }
        }

        private static void InitializeEnemy(string enemyName, string objectName) {
            Enemies[enemyName] = GameObject.Instantiate(GameObject.Find(objectName));
            GameObject.DontDestroyOnLoad(Enemies[enemyName]);
            Enemies[enemyName].SetActive(false);
            Enemies[enemyName].transform.position = new Vector3(-30000f, -30000f, -30000f);
            Enemies[enemyName].name = $"{enemyName} Prefab";
        }

        //public static void PlayerCharacter_Start_PostfixPatch(PlayerCharacter __instance) {

        //}

        public static void PlayerCharacter_Update_PostfixPatch(PlayerCharacter __instance) {
            foreach (string key in Enemies.Keys) {
                Enemies[key].SetActive(false);
                Enemies[key].transform.position = new Vector3(-30000f, -30000f, -30000f);
            }
        }

        public static void Monster_Die_MoveNext_PostfixPatch(Monster._Die_d__77 __instance, ref bool __result) {
            if (!__result) {
                string SceneName = CurrentSceneName;
                if (SceneName != "Cathedral Arena") {
                    if (!DefeatedEnemyTracker.ContainsKey(SceneName)) {
                        DefeatedEnemyTracker.Add(SceneName, new List<string>());
                    }
                    if (EnemiesInCurrentScene.ContainsKey(__instance.__4__this.name)) {
                        DefeatedEnemyTracker[SceneName].Add(EnemiesInCurrentScene[__instance.__4__this.name]);
                    }
                }
            }
        }

        public static bool Campfire_RespawnAtLastCampfire_PrefixPatch(Campfire __instance) {
            DefeatedEnemyTracker.Clear();
            return true;
        }
    }
}
