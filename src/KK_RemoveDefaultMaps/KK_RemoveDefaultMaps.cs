using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using System.Collections;
using System.Collections.Generic;
using Unity;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;

namespace KK_RemoveDefaultMaps
{
    [BepInPlugin(GUID, PluginName, Version)]
    // Tell BepInEx that this plugin needs KKAPI of at least the specified version.
    // If not found, this plugi will not be loaded and a warning will be shown.
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class KK_RemoveDefaultMaps : BaseUnityPlugin
    {
        /// <summary>
        /// Human-readable name of the plugin. In general, it should be short and concise.
        /// This is the name that is shown to the users who run BepInEx and to modders that inspect BepInEx logs. 
        /// </summary>
        public const string PluginName = "KK_RemoveDefaultMaps";

        /// <summary>
        /// Unique ID of the plugin. Will be used as the default config file name.
        /// This must be a unique string that contains only characters a-z, 0-9 underscores (_) and dots (.)
        /// When creating Harmony patches or any persisting data, it's best to use this ID for easier identification.
        /// </summary>
        public const string GUID = "koikdaisy.kkremovedefaultmaps";

        /// <summary>
        /// Version of the plugin. Must be in form <major>.<minor>.<build>.<revision>.
        /// Major and minor versions are mandatory, but build and revision can be left unspecified.
        /// </summary>
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;

        private ConfigEntry<bool> _exampleConfigEntry;

        private void Awake()
        {
            Logger = base.Logger;

            _exampleConfigEntry = Config.Bind("General", "Enable this plugin", true, "If false, this plugin will do nothing");

            if (_exampleConfigEntry.Value)
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
                //CharacterApi.RegisterExtraBehaviour<MyCustomController>(GUID);
            }
        }

        private static void DoTheThing()
        {
            Image[] images = FindObjectsOfType<Image>();

            GameObject trashcan = new GameObject(GUID);
            trashcan.SetActive(false);

            List<string> thumbNames = new List<string>();

            List<string> assetBundleNames = CommonLib.GetAssetBundleNameListFromPath("map/list/mapinfo");
            foreach (string assetBundleName in assetBundleNames)
            {
                MapInfo[] mapInfoList = AssetBundleManager.LoadAllAsset(assetBundleName, typeof(MapInfo)).GetAllAssets<MapInfo>();
                foreach (MapInfo info in mapInfoList)
                {
                    foreach (MapInfo.Param param in info.param)
                    {
                        thumbNames.Add(param.ThumbnailAsset);
                    }
                }

            }

            List<GameObject> nodeFrames = new List<GameObject>();

            foreach (Image image in images)
            {
                foreach (string thumb in thumbNames)
                {
                    if (image.mainTexture.name == thumb && image.gameObject.name.Contains("MapButton"))
                    {
                        image.gameObject.SetActive(false);
                        if (image.gameObject.transform.parent.childCount == 1)
                        {
                            image.gameObject.transform.parent.gameObject.SetActive(false);
                        }
                        image.gameObject.transform.SetParent(trashcan.transform);
                    }


                }
                if (!nodeFrames.Contains(image.gameObject.transform.parent.gameObject) && image.gameObject.name.Contains("MapButton") && image.gameObject.transform.parent.gameObject.activeSelf && image.transform.parent.name != GUID)
                {
                    GameObject nodeFrame = image.gameObject.transform.parent.gameObject;
                    nodeFrames.Insert(0, image.gameObject.transform.parent.gameObject);
                    //ConsoleLog("Adding nf..... starting child count: " + nodeFrame.transform.childCount + ", enabled: " + nodeFrame.gameObject.activeSelf+", first child: "+nodeFrame.transform.GetChild(0).GetComponent<Image>().mainTexture.name);
                }

            }

            for (int i = 0; i < nodeFrames.Count; i++)
            {

                GameObject nodeFrame = nodeFrames[i];
                GameObject nextNodeFrame = nodeFrames[i + 1];
                //ConsoleLog("-----------nf#" + i + " starting child count: " +nodeFrame.transform.childCount+", enabled: "+nodeFrame.gameObject.activeSelf+"next nf child count: " + nextNodeFrame.transform.childCount);
                while (nodeFrame.transform.childCount < 4 && nextNodeFrame != null && nextNodeFrame.transform.childCount > 0 && nodeFrame.gameObject.activeSelf)
                {

                    nextNodeFrame.transform.GetChild(0).SetParent(nodeFrame.transform);
                    nodeFrame.gameObject.SetActive(true);
                    //ConsoleLog("nodeframe #" + i + " child count: " + nodeFrame.transform.childCount);
                }
                if (nodeFrame.transform.childCount == 0)
                {
                    //ConsoleLog("nodeframe #" + i + " disabled.");
                    nodeFrame.gameObject.SetActive(false);
                }
            }

        }

        private static void ConsoleLog(string value)
        {
            Logger.Log(LogLevel.All, GUID + ": " + value);
        }

        private static class Hooks
        {
            static bool inMapScreen = false;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(VRMapSelectMenuScene), "Start")]
            private static void PostfixVRMapSelectMenuSceneStart(VRMapSelectMenuScene __instance)
            {
                inMapScreen = true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(FreeHScene), "Start")]
            private static void PostFixFreeHSceneStart(FreeHScene __instance)
            {
                ConsoleLog("attempting start");
                inMapScreen = true;
            }
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MapSelectMenuScene), "Start")]
            private static void PostFixMapSelectMenuSceneStart(MapSelectMenuScene __instance)
            {
                ConsoleLog("attempting start");
                inMapScreen = true;
                DoTheThing();
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(BaseMap), "Start")]
            private static void PostFixObservableUpdateTriggerStart(BaseMap __instance)
            {
                ConsoleLog("attempting start");
                if (inMapScreen)
                {

                    DoTheThing();
                }
            }

            // [HarmonyPrefix]
            // [HarmonyPatch(typeof(SomeClass), nameof(SomeClass.SomeInstanceMethod))]
            // private static void SomeMethodPrefix(SomeClass __instance, int someParameter, ref int __result)
            // {
            //     ...
            // }
        }
    }
}
