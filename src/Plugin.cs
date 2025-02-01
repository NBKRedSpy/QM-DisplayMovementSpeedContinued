using HarmonyLib;
using MGSC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TinyJson;
using TMPro;
using UnityEngine;

namespace QM_DisplayMovementSpeedContinued
{
    public class Plugin
    {
        public const string MoveSpeedTextId = "movementSpeedText";
        public static KeyCode toggleKey = KeyCode.Comma;
        public static bool show = true;

        public static ConfigDirectories ModDirectories = new ConfigDirectories();

        // New
        public static GameObject uiPrefab;
        public static APUiController apController;

        public static string RootFolder => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        #region MGSC Hooks

        [Hook(ModHookType.AfterBootstrap)]
        public static void Bootstrap(IModContext context)
        {
            string configPath = ModDirectories.ConfigPath;


            // thanks NBK_redspy, i just looked at your code because i had no idea how to do this
            // From NBK_RedSpy:  You are welcome ;)
            if (File.Exists(configPath))
            {
                try
                {
                    string fileJson = File.ReadAllText(configPath);
                    Dictionary<string, string> values = fileJson.FromJson<Dictionary<string, string>>();
                    toggleKey = (KeyCode)Enum.Parse(typeof(KeyCode), values["toggleKey"]);
                }
                catch (Exception ex)
                {
                    Debug.Log("DisplayMovementSpeed: Error reading config file");
                    Debug.LogException(ex);
                }
            }
            else
            {
                try
                {

                    Directory.CreateDirectory(ModDirectories.ModPersistenceFolder);

                    var text = "{\"toggleKey\":\"Comma\"}";
                    File.WriteAllText(configPath, text);
                }
                catch (Exception ex)
                {
                    Debug.Log("DisplayMovementSpeed: Error writing to config");
                    Debug.LogException(ex);
                }
            }

            // Plugin startup logic
            var harmony = new Harmony("QM_DisplayMovementSpeedContinued");
            harmony.PatchAll();
        }

        // New
        [Hook(ModHookType.DungeonStarted)]
        public static void SpawnUI(IModContext context)
        {
            var canvasRoot = GameObject.FindObjectOfType<DungeonUI>().transform;
            apController = GameObject.FindObjectOfType<APUiController>();
            uiPrefab = DataLoader.LoadFileFromBundle<GameObject>("apcontrollerbundle", "ControllerPrefab");
            if (uiPrefab == null)
            {
                Debug.LogError($"Could not spawn, UI PREFAB is null");
            }
            else if (canvasRoot != null && apController == null)
            {
                apController = GameObject.Instantiate(uiPrefab, canvasRoot).AddComponent<APUiController>();
                apController.LoadComponents("apcontrollerbundle");
                apController.name = $"[UI] AP Controller";
                apController.DisableUI();
                Debug.Log($"UI for APController has instantiated correctly");
            }
            else
            {
                Debug.LogError($"unsupported error?");
            }
        }

        [Hook(ModHookType.DungeonUpdateBeforeGameLoop)]
        public static void DungeonUpdateBeforeGameLoop(IModContext context)
        {
            if (InputHelper.GetKeyDown(toggleKey))
            {
                show = !show;
            }

        }
        #endregion

        public static void createText(Monster __instance)
        {

            GameObject monsterGameObject = __instance.Creature3dView.gameObject;

            if (monsterGameObject.GetComponent<HideTextMesh>() != null)
            {
                return;
            }

            GameObject textGameObject = new GameObject(MoveSpeedTextId);

            textGameObject.transform.SetParent(monsterGameObject.transform);
            textGameObject.transform.localPosition = new Vector3(0.1f, 0.1f, -1);

            textGameObject.AddComponent(typeof(TextMeshPro));

            TextMeshPro text = textGameObject.GetComponent<TextMeshPro>();

            text.text = GetLabelText(__instance);
            text.fontSize = 1f;
            text.fontStyle = FontStyles.Bold;
            text.lineSpacing = 1;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineColor = Color.black;
            text.outlineWidth = 0.3f;

            HideTextMesh hider = __instance.Creature3dView.gameObject.AddComponent<HideTextMesh>();
        }

        // New
        public static void UpdateUI(CellPosition mapCell, ObjHighlightController __instance)
        {
            Monster monster = __instance._creatures.GetMonster(mapCell.X, mapCell.Y);
            if (monster != null)
            {
                apController.SetEnemy(monster, monster.transform.position);
            }
            else
            {
                apController.DisableUI();
            }
        }

        public static void UpdateText(Monster __instance)
        {
            //After taking damage, update the label in case the enemy lost their weapon due to amputation.
            Component moveComponent = __instance.Creature3dView.gameObject.GetComponentsInChildren(typeof(TMPro.TextMeshPro))
                .ToList()
                .SingleOrDefault(x => x.name == Plugin.MoveSpeedTextId);

            TextMeshPro label = moveComponent?.GetComponent<TextMeshPro>();

            if (label != null)
            {
                label.text = Plugin.GetLabelText(__instance);
            }
        }
        public static string GetLabelText(Monster monster)
        {
            Inventory inventory = monster.CreatureData.Inventory;

            bool hasRanged = false;

            List<string> weaponsList = new List<string>();

            if (inventory != null)
            {
                //Assuming that if one ranged weapon is found, it's ranged.
                //Ignoring turrets since they will never be melee.

                hasRanged = inventory.WeaponSlots
                    .Any(x => x.Items
                        .Any(y => y?.Record<WeaponRecord>()?.IsMelee == false)
                    );

                weaponsList = inventory.WeaponSlots
                    .SelectMany(x =>
                        x.Items
                            .Select(y => y.Record<WeaponRecord>().Id)
                            )
                    .ToList();
            }

            return $"{monster.ActionPointsLeft + monster.ActionPointsProcessed}{(hasRanged ? "" : "M")}";
        }

    }


    // Custom new patch for UI
    [HarmonyPatch(typeof(ObjHighlightController), nameof(ObjHighlightController.Process))]
    public static class Patch_ObjHighlightController_Process
    {
        public static void Postfix(CellPosition cellUnderCursor, ObjHighlightController __instance)
        {
            Plugin.UpdateUI(cellUnderCursor, __instance);
        }
    }

    [HarmonyPatch(typeof(Monster), nameof(Monster.ProcessDamage))]
    public static class Patch_ProcessDamage
    {
        public static void Postfix(Monster __instance)
        {
            Plugin.UpdateText(__instance);
        }

    }


    //Debug - Attempt at handling the initialize

    [HarmonyPatch(typeof(Monster), nameof(Monster.Configure3dView))]
    public static class Monster_Patch_Configure3dView
    {
        public static void Postfix(Monster __instance)
        {
            Plugin.createText(__instance);
        }
    }

    [HarmonyPatch(typeof(Monster), nameof(Monster.Mutate))]
    public static class Patch_OnMutate
    {
        public static void Postfix(Monster __instance)
        {
            Plugin.createText(__instance);
        }
    }

    [HarmonyPatch(typeof(Monster), nameof(Monster.UpdateVisibility), new Type[] { })]
    public static class Patch_CreatureViewOnVisualRefreshed
    {
        public static void Postfix(Monster __instance)
        {
            Plugin.UpdateText(__instance);
        }
    }

}
