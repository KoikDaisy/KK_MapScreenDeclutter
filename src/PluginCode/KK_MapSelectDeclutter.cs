using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KKAPI.Utilities;

namespace KK_MapSelectDeclutter
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("Koikatu")]
    [BepInProcess("KoikatuVR")]
    [BepInProcess("Koikatsu Party")]
    [BepInProcess("Koikatsu Party VR")]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class KK_MapSelectDeclutter : BaseUnityPlugin
    {
        public const string PluginName = "KK_MapSelectDeclutter";
        public const string GUID = "koikdaisy.kkmapselectdeclutter";
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;

        private ConfigEntry<bool> _enabled;

        private static Texture2D defaultImage = ResourceUtils.GetEmbeddedResource("default.png").LoadTexture();

        private void Awake()
        {
            Logger = base.Logger;

            _enabled = Config.Bind("General", "Enable this plugin", true, "If false, this plugin will do nothing (requires restart)");

            if (_enabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
            }
        }
        





        private static class Hooks
        {
            private static bool inMapScreen;

            private static bool isVR = false;

            //called at the beginning whenever a map is loaded. if VRCharaSelectScene game object exists, we're on the map select screen, and we also know we're in the VR version
            //would have hooked into VRCharaSelectScene if I had access to that type, but I wanted this to be one universal plugin that worked in VR and Desktop
            [HarmonyPostfix]
            [HarmonyPatch(typeof(BaseLoader), "Awake")]
            private static void Post_BaseLoader(BaseLoader __instance)
            {
                inMapScreen = GameObject.Find("VRCharaSelectScene");
                isVR = inMapScreen;
            }
            
            //desktop version's map select is a whole separate scene, which means we need to wait until the scene is loaded to try removing thumbnails
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MapSelectMenuScene), "Start")]
            private static void Post_MapSelectMenuScene(MapSelectMenuScene __instance, ref IEnumerator __result)
            {
                var original = __result;
                __result = new[] { original, Postfix() }.GetEnumerator();

                IEnumerator Postfix()
                {
                    yield return new WaitUntil(() => GameObject.Find("MapSelectMenu/Canvas/Panel/ScrollView/Content/NodeFrame(Clone)/MapButton") != null);
                    DeclutterMapList();
                }
            }

            //start coroutine to make sure the map thumbnail is loaded before we try changing its texture
            [HarmonyPostfix]
            [HarmonyPatch(typeof(FreeHScene), "Start")]
            private static void FreeHStartAsyncPostfix(MapSelectMenuScene __instance, ref IEnumerator __result)
            {
                var original = __result;
                __result = new[] { original, Postfix() }.GetEnumerator();

                IEnumerator Postfix()
                {
                    yield return new WaitUntil(() => GameObject.Find("MapThumbnail") != null);
                    SetMainCanvasThumbnail(isVR, ThumbnailState.Default);
                }
            }

            //whenever a map is loaded, check if it's a map select screen
            [HarmonyPostfix]
            [HarmonyPatch(typeof(BaseMap), "Start")]
            private static void Post_BaseMap(BaseMap __instance)
            {
                if (inMapScreen)
                {
                    DeclutterMapList();
                    SetMainCanvasThumbnail(isVR, ThumbnailState.Default);

                    if(isVR)
                    { 
                        GameObject.Find("MapCanvas/Panel/RightBG/btnEnter").GetComponent<Button>().onClick.AddListener(() => { SetMainCanvasThumbnail(isVR, ThumbnailState.Unset); });
                    }
                    
                }
            }



        }

        private static void ConsoleLog(string value)
        {
            Logger.Log(LogLevel.All, GUID + ": " + value);
        }


        private static void DeclutterMapList()
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
                }

            }

            for (int i = 0; i < nodeFrames.Count - 1; i++)
            {
                //for some reason when i use the expression "i + 1" it gives me an out of range error, so I used another local variable to represent i
                int a = i;

                GameObject nodeFrame = nodeFrames[i];
                GameObject nextNodeFrame = nodeFrames[a + 1];
                while (nodeFrame.transform.childCount < 4 && nextNodeFrame != null && nextNodeFrame.transform.childCount > 0 && nodeFrame.gameObject.activeSelf)
                {

                    if (nextNodeFrame.transform.childCount > 0)
                    {
                        Transform newChild = nextNodeFrame.transform.GetChild(0);
                        newChild.SetParent(nodeFrame.transform);
                        nodeFrame.gameObject.SetActive(true);
                    }

                }
                if (nodeFrame.transform.childCount == 0)
                {
                    nodeFrame.gameObject.SetActive(false);
                }
            }
        }

        private static void SetMainCanvasThumbnail(bool isVR, ThumbnailState thumbnailState)
        {
            
            string gameObjectLookup = isVR ? "MainCanvas/Panel" : "FreeHScene/Canvas/Panel";

            var parent = GameObject.Find(gameObjectLookup);
            Image[] children = parent.transform.GetComponentsInChildren<Image>(true);

            foreach (Image child in children)
            {
                if (child.gameObject.name == "MapThumbnail" && child.transform.parent.gameObject.name != "Dark")
                {
                    if (thumbnailState == ThumbnailState.Default)
                    {
                        if (isVR) child.overrideSprite = defaultImage.ToSprite();
                        child.sprite = defaultImage.ToSprite();
                        child.gameObject.name = child.gameObject.name + " " + GUID;

                    } 
                }
                else if (child.gameObject.name == "MapThumbnail" + " " + GUID && child.transform.parent.gameObject.name != "Dark" && thumbnailState == ThumbnailState.Unset)
                {
                    if (isVR) child.overrideSprite = null;
                }
            }
        }

        private enum ThumbnailState { Default, Unset };

    }
}
