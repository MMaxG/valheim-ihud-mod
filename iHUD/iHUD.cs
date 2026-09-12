using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.ace.iHUD", "iHUD", "1.0.0")]
public sealed class iHUDPlugin : BaseUnityPlugin
{
    internal static bool Enabled = true; // Entire plugin enabled/disabled
    private ConfigEntry<KeyboardShortcut> toggleHotkey = null!;
    internal static ConfigEntry<float> HealthBarHideDelay = null!;
    internal static ConfigEntry<float> HealthBarFadeDuration = null!;
    internal static ManualLogSource Log = null!;
    private void Awake()
    {
        toggleHotkey = Config.Bind(
            "General",
            "Toggle Hotkey",
            new KeyboardShortcut(KeyCode.X),
            "Press this key to toggle iHUD on and off.");

        HealthBarFadeDuration = Config.Bind(
            "General",
            "Health bar fade duration",
            1.5f,
            "Fade out animation length in seconds."
        );

        HealthBarHideDelay = Config.Bind(
            "General",
            "Health bar hide delay",
            5f,
            "Seconds at full health before the health bar starts to fade out."
        );

        Log = Logger;

        new Harmony("ace.iHUD").PatchAll();

        Config.Save();

        Logger.LogInfo("iHUD plugin loaded");
    }

    private void Update()
    {
        if (toggleHotkey.Value.IsDown())
        {
            ToggleHUD();
        }
    }

    private void ToggleHUD()
    {
        if (Player.m_localPlayer == null)
        {
            Logger.LogInfo("No active player found.");
            return;
        }

        Enabled = !Enabled;
        Logger.LogInfo($"iHUD {(Enabled ? "enabled" : "disabled")}");
    }

    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    public static class Hud_UpdateCrosshair_Patch
    {
        public static void Postfix(Hud __instance, Player player)
        {
            if (player != Player.m_localPlayer || player == null) return;

            bool hoveringInteractable = player.GetHoverObject() != null;
            bool usingRangedWeapon = IsUsingRangedWeapon(player);

            bool shouldShow = hoveringInteractable || usingRangedWeapon;

            __instance.m_crosshair.enabled = shouldShow;
        }

        // Determines if the player is currently using a ranged weapon by checking if the weapon has a projectile attack or uses ammo.
        // Does this by checking the weapon's shared data for attack and secondary attack projectiles, as well as the ammo type.
        private static bool IsUsingRangedWeapon(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon == null) return false;

            var shared = weapon.m_shared;

            bool primaryIsProjectile = shared.m_attack != null && shared.m_attack.m_attackProjectile != null;
            bool secondaryIsProjectile = shared.m_secondaryAttack != null && shared.m_secondaryAttack.m_attackProjectile != null;

            return primaryIsProjectile || secondaryIsProjectile || shared.m_ammoType != string.Empty;
        }
    }

    [HarmonyPatch(typeof(Hud), "UpdateHealth")]
    public static class Hud_UpdateHealth_Patch
    {
        private static float previousHealth = -1f;
        private static bool wasAtFull = false;
        private static float timeSinceFull = 0f;
        private static CanvasGroup? healthBarCanvasGroup;
        private const float Epsilon = 0.01f;


        public static void Postfix(Hud __instance, Player player)
        {
            if (player != Player.m_localPlayer || player == null) return;

            if (healthBarCanvasGroup == null)
            {
                GameObject healthBarObject = __instance.m_healthBarRoot.gameObject;
                healthBarCanvasGroup = healthBarObject.GetComponent<CanvasGroup>();
                if (healthBarCanvasGroup == null)
                {
                    healthBarCanvasGroup = healthBarObject.AddComponent<CanvasGroup>();
                }
            }

            if (!iHUDPlugin.Enabled)
            {
                healthBarCanvasGroup.alpha = 1f;
                return;
            }

            float currentHealth = player.GetHealth();
            float currentMaxHealth = player.GetMaxHealth();

            if (previousHealth < 0f)
            {
                previousHealth = currentHealth;
            }

            float clampedPreviousHealth = Mathf.Min(previousHealth, currentMaxHealth);
            bool tookRealDamage = currentHealth < clampedPreviousHealth - Epsilon;
            bool atFullHealth = currentHealth >= currentMaxHealth - Epsilon;

            if (tookRealDamage || atFullHealth != wasAtFull)
            {
                iHUDPlugin.Log.LogInfo(
                    $"health={currentHealth:F3} max={currentMaxHealth:F3} prev={previousHealth:F3} " +
                    $"clampedPrev={clampedPreviousHealth:F3} damage={tookRealDamage} full={atFullHealth} " +
                    $"timeSinceFull={timeSinceFull:F2} alpha={healthBarCanvasGroup.alpha:F2}");
                wasAtFull = atFullHealth;
            }
            
            if (tookRealDamage || !atFullHealth)
            {
                timeSinceFull = 0f;
            }
            else
            {
                timeSinceFull += Time.deltaTime;
            }

            bool inventoryOpen = InventoryGui.IsVisible();

            if (inventoryOpen)
            {
                healthBarCanvasGroup.alpha = 1f;
            }
            else
            {
                float fadeStart = iHUDPlugin.HealthBarHideDelay.Value;
                float fadeDuration = iHUDPlugin.HealthBarFadeDuration.Value;

                if (timeSinceFull <= fadeStart)
                {
                    healthBarCanvasGroup.alpha = 1f;
                }
                else if (timeSinceFull >= fadeStart + fadeDuration)
                {
                    healthBarCanvasGroup.alpha = 0f;
                }
                else
                {
                    healthBarCanvasGroup.alpha = 1f - (timeSinceFull - fadeStart) / fadeDuration;
                }
            }

            previousHealth = currentHealth;
        }
    }

    // Debugging patch to dump health-related fields in the Hud class for inspection
    // [HarmonyPatch(typeof(Hud), "Awake")]
    // public static class Hud_Awake_DebugDump_Patch
    // {
    //     public static void Postfix(Hud __instance)
    //     {
    //         var fields = typeof(Hud).GetFields(
    //             System.Reflection.BindingFlags.Instance |
    //             System.Reflection.BindingFlags.Public |
    //             System.Reflection.BindingFlags.NonPublic);

    //         foreach (var field in fields)
    //         {
    //             if (field.Name.ToLower().Contains("health"))
    //             {
    //                 object value = field.GetValue(__instance);
    //                 iHUDPlugin.Log.LogInfo($"{field.Name} ({field.FieldType.Name}) = {value}");
    //             }
    //         }
    //     }
    // }
}
