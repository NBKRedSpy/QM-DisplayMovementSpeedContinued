﻿using MGSC;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QM_DisplayMovementSpeedContinued
{
    public class APUiController : MonoBehaviour
    {
        [Header("Manual adjusting")]
        public Vector3 Adjustment = new Vector3(0f, 0.2f, 0f);

        [Header("Components")]
        public TextMeshProUGUI APTextObject;
        public Image AttackTypeImage;
        public Image HealthBar;

        [Header("Images")]
        [SerializeField] private Sprite meleeSprite;
        [SerializeField] private Sprite rangedSprite;
        [SerializeField] private Sprite defaultSprite;

        private RectTransform Root;

        private Monster lastMonster;

        private RectTransform Canvas;

        public void LoadComponents(string bundleName)
        {
            Canvas = GameObject.FindObjectOfType<DungeonUI>().GetComponentInParent<Canvas>().transform as RectTransform;

            APTextObject = this.transform.GetComponentInChildren<TextMeshProUGUI>();

            AttackTypeImage = this.transform.GetComponentsInChildren<Image>()
                                    .Where(x => x.gameObject.name.Equals("Image", StringComparison.CurrentCultureIgnoreCase))
                                    .First();

            HealthBar = this.transform.GetComponentsInChildren<Image>(true)
                                    .Where(x => x.gameObject.name.Equals("Fillbar", StringComparison.CurrentCultureIgnoreCase))
                                    .First();

            Root = this.transform.GetComponentsInChildren<RectTransform>()
                                    .Where(x => x.gameObject.name.Equals("Root", StringComparison.CurrentCultureIgnoreCase))
                                    .First();

            var whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.zero);
            // Use static class to load images for the sprites
            var sprites = DataLoader.LoadFilesFromBundle<Sprite>(bundleName, new List<string> { "melee", "ranged", "default" });
            meleeSprite = sprites[0] != null ? sprites[0] : whiteSprite;
            rangedSprite = sprites[1] != null ? sprites[1] : whiteSprite;
            defaultSprite = sprites[2] != null ? sprites[2] : whiteSprite;

            AttackTypeImage.sprite = defaultSprite;
        }

        public void SetEnemy(Monster monster, Vector3 worldPos)
        {
            if (monster == null) return;

            if (monster.CreatureData.Health.Dead)
            {
                return;
            }

            if (!monster.IsSeenByPlayer) return;

            if (Camera.main != null)
            {
                // Set object world pos to UI!
                // Didn't remember about the concese calculation, so:
                // https://discussions.unity.com/t/how-to-convert-from-world-space-to-canvas-space/117981
                Vector2 viewPortPos = Camera.main.WorldToViewportPoint(worldPos + Vector3.Scale(Camera.main.transform.up, Adjustment));
                Vector2 WorldObject_ScreenPosition = new Vector2(
                            ((viewPortPos.x * Canvas.sizeDelta.x) - (Canvas.sizeDelta.x * 0.5f)),
                            ((viewPortPos.y * Canvas.sizeDelta.y) - (Canvas.sizeDelta.y * 0.5f)));
                //viewPortPos += Adjustment;
                ((RectTransform)transform).anchoredPosition = WorldObject_ScreenPosition;
            }
            else
            {
                Debug.LogError($"Camera.main is null, UI not tracking enemy correctly.");
            }

            if (lastMonster == null || monster != lastMonster)
            {
                AttachToNewMonster(monster);
                ChangeSprite(monster);
            }

            HealthBar.fillAmount = monster.CreatureData.Health.Percent;
            APTextObject.text = $"{monster.ActionPointsLeft}";  //$"{monster.ActionPointsLeft}/{monster.ActionPoints}";

            EnableUI();
        }

        public void AttachToNewMonster(Monster newMonster)
        {
            if (lastMonster != null)
                lastMonster.CreatureData.Health.Killed -= OnAttachedDead;
            newMonster.CreatureData.Health.Killed += OnAttachedDead;
            lastMonster = newMonster;
        }

        private void ChangeSprite(Monster monster)
        {
            Inventory inventory = monster.CreatureData.Inventory;

            bool hasRanged = false;

            if (inventory != null)
            {
                //Assuming that if one ranged weapon is found, it's ranged.
                //Ignoring turrets since they will never be melee.

                hasRanged = inventory.WeaponSlots
                    .Any(x => x.Items
                        .Any(y => y?.Record<WeaponRecord>()?.IsMelee == false)
                    );
            }
            AttackTypeImage.sprite = hasRanged ? rangedSprite : meleeSprite;
        }

        private void OnDestroy()
        {
            lastMonster.CreatureData.Health.Killed -= OnAttachedDead;
        }

        public void DisableUI()
        {
            this.Root.gameObject.SetActive(false);
        }

        private void EnableUI()
        {
            this.Root.gameObject.SetActive(true);
        }

        private void HookToUI()
        {
            // Here we register to events of the UI that hide all other components
        }

        private void UnhookToUI()
        {
            
        }

        private void OnAttachedDead()
        {
            DisableUI();
        }
    }
}
