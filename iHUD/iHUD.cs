using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

[BepInPlugin("iHUD", "iHUD", "1.0.0")]
public sealed class iHUDPlugin : BaseUnityPlugin
{
    internal static bool Enabled = true; // Entire plugin enabled/disabled
    internal static bool minimapHidden = false;
    private static readonly Dictionary<string, float> lastRemaining = new Dictionary<string, float>();
    private ConfigEntry<KeyboardShortcut> toggleHotkey = null!;
    internal static ConfigEntry<KeyboardShortcut> MinimapToggleHotkey = null!;
    internal static ConfigEntry<float> HealthBarHideDelay = null!;
    internal static ConfigEntry<float> HealthBarFadeDuration = null!;
    internal static ConfigEntry<float> FoodHideDelay = null!;
    internal static ConfigEntry<float> FoodFadeDuration = null!;
    internal static ConfigEntry<float> FoodExpiredShowDuration = null!;
    internal static ConfigEntry<float> FoodExpiredFadeDuration = null!;
    internal static ConfigEntry<float> PowerHideDelay = null!;
    internal static ConfigEntry<float> PowerFadeDuration = null!;
    internal static ConfigEntry<float> StatusEffectsHideDelay = null!;
    internal static ConfigEntry<float> StatusEffectsFadeDuration = null!;
    internal static ConfigEntry<bool> StatusEffectsAlwaysHide = null!;
    internal static ConfigEntry<bool> StatusEffectsAlwaysShow = null!;
    internal static ConfigEntry<string> PopupEffectNames = null!;
    internal static ConfigEntry<string> PopupReminderThresholds = null!;
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
            2f,
            "Fade out animation length in seconds.");

        HealthBarHideDelay = Config.Bind(
            "General",
            "Health bar hide delay",
            5f,
            "Seconds at full health before the health bar starts to fade out.");

        FoodHideDelay = Config.Bind(
            "General",
            "Food hide delay",
            5f,
            "Seconds at full food before the food bar starts to fade out.");

        FoodFadeDuration = Config.Bind(
            "General",
            "Food fade duration",
            2f,
            "Fade out animation length in seconds.");

        FoodExpiredShowDuration = Config.Bind(
            "General",
            "Food expired show duration",
            60f,
            "Seconds to show food icons after a food buff runs out.");

        FoodExpiredFadeDuration = Config.Bind(
            "General",
            "Food expired fade duration",
            2f,
            "Fade out animation length after a food buff expires.");

        PowerHideDelay = Config.Bind(
            "General",
            "Power icon hide delay",
            5f,
            "Seconds after using/changing your power before it starts to fade out.");

        PowerFadeDuration = Config.Bind(
            "General",
            "Power icon fade duration",
            4f,
            "Fade out animation length for the power icon.");

        MinimapToggleHotkey = Config.Bind(
            "General",
            "Minimap Toggle Hotkey",
            new KeyboardShortcut(KeyCode.V),
            "Show/hide the minimap");

        StatusEffectsHideDelay = Config.Bind(
            "General",
            "Active effects hide delay",
            8f,
            "Seconds after closing the inventory before active effects start to fade out.");

        StatusEffectsFadeDuration = Config.Bind(
            "General",
            "Active effects fade duration",
            4f,
            "Fade out animation length for active effects.");

        StatusEffectsAlwaysHide = Config.Bind(
            "General",
            "Active effects always hide",
            false,
            "If true, all active effect icons are always hidden.");

        StatusEffectsAlwaysShow = Config.Bind(
            "General",
            "Active effects always show",
            false,
            "If true, all active effect icons are always shown, ignoring duration/inventory state.");

        PopupEffectNames = Config.Bind(
            "General",
            "Popup effect names",
            "Rested",
            "Comma-separated effect names that hide after a delay but reappear briefly at each reminder threshold.");

        PopupReminderThresholds = Config.Bind(
            "General",
            "Popup reminder thresholds",
            "300,60",
            "Comma-separated seconds remaining at which popup effects briefly reappear.");

        Log = Logger;

        new Harmony("ace.iHUD").PatchAll();

        Config.Save();

        Logger.LogInfo("iHUD plugin loaded");
    }

    private void Update()
    {
        if (IsHotkeyDown(toggleHotkey.Value)) ToggleHUD();
        if (IsHotkeyDown(MinimapToggleHotkey.Value)) ToggleMinimap();
    }

    private static bool IsHotkeyDown(KeyboardShortcut shortcut)
    {
        if (!Input.GetKeyDown(shortcut.MainKey)) return false;

        foreach (var modifier in shortcut.Modifiers)
        {
            if (!Input.GetKey(modifier)) return false;
        }

        return true;
    }

    // in iHUDPlugin itself, alongside Update()
    private void LateUpdate()
    {
        if (Minimap.instance != null)
        {
            bool shouldShowMinimap = !Enabled || !minimapHidden;
            if (Minimap.instance.m_smallRoot.activeSelf != shouldShowMinimap)
            {
                Minimap.instance.m_smallRoot.SetActive(shouldShowMinimap);
            }
        }
    }
    private void ToggleHUD()
    {
        if (Player.m_localPlayer == null)
        {
            Logger.LogInfo("No player found.");
            return;
        }

        Enabled = !Enabled;
        Logger.LogInfo($"iHUD {(Enabled ? "enabled" : "disabled")}");

    }

    private void ToggleMinimap()
    {
        if (Minimap.instance == null)
        {
            Logger.LogInfo("No minimap found.");
            return;
        }

        minimapHidden = !minimapHidden;
        Logger.LogInfo($"Minimap {(minimapHidden ? "hidden" : "shown")}");
    }

    private static bool CrossedThreshold(string name, float remaining, float[] thresholds)
    {
        bool crossed = false;
        if (lastRemaining.TryGetValue(name, out float previous))
        {
            foreach (float threshold in thresholds)
            {
                if (previous > threshold && remaining <= threshold) crossed = true;
            }
        }
        lastRemaining[name] = remaining;
        return crossed;
    }

    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    public static class Hud_UpdateCrosshair_Patch
    {
        public static void Postfix(Hud __instance, Player player)
        {
            if (player != Player.m_localPlayer || player == null) return;

            if (!iHUDPlugin.Enabled)
            {
                __instance.m_crosshair.enabled = true;
                return;
            }

            bool hoveringInteractable = player.GetHoverObject() != null;
            bool usingRangedWeapon = IsUsingRangedWeapon(player);

            __instance.m_crosshair.enabled = hoveringInteractable || usingRangedWeapon;
        }

        // Determines if the player is currently using a ranged weapon by checking if the weapon has a projectile attack or uses ammo.
        // Does this by checking the weapon's shared data for attack and secondary attack projectiles, as well as the ammo type.
        private static bool IsUsingRangedWeapon(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon == null) return false;

            var shared = weapon.m_shared;

            bool hasProjectileAttack = shared.m_attack.m_attackType == Attack.AttackType.Projectile;
            bool hasAmmoType = shared.m_ammoType != string.Empty;

            return hasProjectileAttack || hasAmmoType;
        }
    }

    [HarmonyPatch(typeof(Hud), "UpdateHealth")]
    public static class Hud_UpdateHealth_Patch
    {
        private static float previousHealth = -1f;
        private static bool wasAtFull = false;
        private static float timeSinceFull = 0f;
        private static CanvasGroup? healthBarCanvasGroup;
        private static CanvasGroup? healthIconCanvasGroup;
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

            if (healthIconCanvasGroup == null)
            {
                Transform healthIcon = __instance.m_healthBarRoot.transform.parent.Find("healthicon");
                if (healthIcon != null)
                {
                    healthIconCanvasGroup = healthIcon.GetComponent<CanvasGroup>();
                    if (healthIconCanvasGroup == null)
                    {
                        healthIconCanvasGroup = healthIcon.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }

            if (!iHUDPlugin.Enabled)
            {
                healthBarCanvasGroup.alpha = 1f;
                if (healthIconCanvasGroup != null) healthIconCanvasGroup.alpha = 1f;
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
                if (healthIconCanvasGroup != null) healthIconCanvasGroup.alpha = 1f;
            }
            else
            {
                float fadeStart = iHUDPlugin.HealthBarHideDelay.Value;
                float fadeDuration = iHUDPlugin.HealthBarFadeDuration.Value;

                if (timeSinceFull <= fadeStart)
                {
                    healthBarCanvasGroup.alpha = 1f;
                    if (healthIconCanvasGroup != null) healthIconCanvasGroup.alpha = 1f;
                }
                else if (timeSinceFull >= fadeStart + fadeDuration)
                {
                    healthBarCanvasGroup.alpha = 0f;
                    if (healthIconCanvasGroup != null) healthIconCanvasGroup.alpha = 0f;
                }
                else
                {
                    float targetAlpha = 1f - (timeSinceFull - fadeStart) / fadeDuration;
                    healthBarCanvasGroup.alpha = targetAlpha;
                    if (healthIconCanvasGroup != null) healthIconCanvasGroup.alpha = targetAlpha;
                }
            }

            previousHealth = currentHealth;
        }
    }
}

[HarmonyPatch(typeof(Hud), "UpdateFood")]
public static class Hud_UpdateFood_Patch
{
    private static float timeSinceEvent = float.MaxValue;
    private static float activeFadeStart = 0f;
    private static float activeFadeDuration = 0f;
    private static int previousFoodCount = -1;
    private static readonly List<CanvasGroup> foodCanvasGroups = new List<CanvasGroup>();
    private static readonly string[] childNames = { "Food", "FoodText", "foodicon", "foodicon (1)", "food0", "food1", "food2" };

    public static void Postfix(Hud __instance, Player player)
    {
        if (player != Player.m_localPlayer || player == null) return;

        EnsureFoodCanvasGroups(__instance.m_healthBarRoot.parent);

        int currentFoodCount = player.GetFoods().Count;

        if (previousFoodCount >= 0 && currentFoodCount < previousFoodCount)
        {
            NotifyFoodExpired();
        }

        bool anyFoodLow = false;
        foreach (var food in player.GetFoods())
        {
            if (food.m_time <= 60f) anyFoodLow = true;
        }

        if (anyFoodLow)
        {
            SetAlpha(1f);
            return;
        }

        previousFoodCount = currentFoodCount;

        if (!iHUDPlugin.Enabled)
        {
            SetAlpha(1f);
            return;
        }

        timeSinceEvent += Time.deltaTime;

        if (InventoryGui.IsVisible())
        {
            SetAlpha(1f);
            return;
        }

        if (timeSinceEvent <= activeFadeStart)
        {
            SetAlpha(1f);
        }
        else if (timeSinceEvent >= activeFadeStart + activeFadeDuration)
        {
            SetAlpha(0f);
        }
        else
        {
            SetAlpha(1f - (timeSinceEvent - activeFadeStart) / activeFadeDuration);
        }
    }

    public static void NotifyFoodEaten()
    {
        timeSinceEvent = 0f;
        activeFadeStart = iHUDPlugin.FoodHideDelay.Value;
        activeFadeDuration = iHUDPlugin.FoodFadeDuration.Value;
    }

    public static void NotifyFoodExpired()
    {
        timeSinceEvent = 0f;
        activeFadeStart = iHUDPlugin.FoodExpiredShowDuration.Value;
        activeFadeDuration = iHUDPlugin.FoodExpiredFadeDuration.Value;
    }

    private static void SetAlpha(float alpha)
    {
        foreach (CanvasGroup cg in foodCanvasGroups)
        {
            if (cg == null) continue; // Unity's overloaded == safely detects destroyed objects here
            cg.alpha = alpha;
        }
    }

    private static void EnsureFoodCanvasGroups(Transform parent)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            bool needsRefresh = i >= foodCanvasGroups.Count || foodCanvasGroups[i] == null;
            if (!needsRefresh) continue;

            Transform child = parent.Find(childNames[i]);
            if (child == null) continue;

            CanvasGroup cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();

            if (i < foodCanvasGroups.Count) foodCanvasGroups[i] = cg;
            else foodCanvasGroups.Add(cg);
        }
    }
}

[HarmonyPatch(typeof(Player), "EatFood")]
public static class Player_EatFood_Patch
{
    public static void Postfix(Player __instance, bool __result)
    {
        if (__instance != Player.m_localPlayer) return;
        if (!__result) return; // eating failed (e.g. no free food slot), don't trigger

        Hud_UpdateFood_Patch.NotifyFoodEaten();
    }
}

[HarmonyPatch(typeof(Hud), "UpdateGuardianPower")]
public static class Hud_UpdateGuardianPower_Patch
{
    private static float timeSinceEvent = 0f;
    private static bool hasInitialized = false;
    private static CanvasGroup? powerCanvasGroup;

    public static void Postfix(Hud __instance, Player player)
    {
        if (player != Player.m_localPlayer || player == null) return;

        if (!hasInitialized)
        {
            hasInitialized = true;
            timeSinceEvent = 0f;
        }

        if (powerCanvasGroup == null)
        {
            GameObject gpObject = __instance.m_gpRoot.gameObject;
            powerCanvasGroup = gpObject.GetComponent<CanvasGroup>();
            if (powerCanvasGroup == null)
            {
                powerCanvasGroup = gpObject.AddComponent<CanvasGroup>();
            }
        }

        if (!iHUDPlugin.Enabled)
        {
            powerCanvasGroup.alpha = 1f;
            return;
        }

        timeSinceEvent += Time.deltaTime;

        if (InventoryGui.IsVisible())
        {
            powerCanvasGroup.alpha = 1f;
            return;
        }

        float fadeStart = iHUDPlugin.PowerHideDelay.Value;
        float fadeDuration = iHUDPlugin.PowerFadeDuration.Value;

        if (timeSinceEvent <= fadeStart) powerCanvasGroup.alpha = 1f;
        else if (timeSinceEvent >= fadeStart + fadeDuration) powerCanvasGroup.alpha = 0f;
        else powerCanvasGroup.alpha = 1f - (timeSinceEvent - fadeStart) / fadeDuration;
    }

    public static void NotifyPowerEvent()
    {
        timeSinceEvent = 0f;
    }
}

[HarmonyPatch(typeof(SEMan), "AddStatusEffect", typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short))]
public static class SEMan_AddStatusEffect_Patch
{
    public static void Postfix(SEMan __instance, StatusEffect __result)
    {
        if (__result == null) return;
        if (!__result.name.StartsWith("GP_")) return;
        if (Player.m_localPlayer == null || __instance != Player.m_localPlayer.GetSEMan()) return;

        Hud_UpdateGuardianPower_Patch.NotifyPowerEvent();
    }
}

[HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
public static class HotkeyBar_UpdateIcons_Patch
{
    private static CanvasGroup? hotkeyBarCanvasGroup;

    public static void Postfix(HotkeyBar __instance)
    {
        if (hotkeyBarCanvasGroup == null)
        {
            hotkeyBarCanvasGroup = __instance.GetComponent<CanvasGroup>();
            if (hotkeyBarCanvasGroup == null)
            {
                hotkeyBarCanvasGroup = __instance.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (!iHUDPlugin.Enabled)
        {
            hotkeyBarCanvasGroup.alpha = 1f;
            return;
        }

        hotkeyBarCanvasGroup.alpha = InventoryGui.IsVisible() ? 1f : 0f;
    }
}

// A method that overrides the visibility and alpha of status effect icons
[HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
public static class Hud_UpdateStatusEffects_Patch
{
    private const float Epsilon = 0.01f;
    private static int? previousComfort = null;
    private static readonly Dictionary<string, float> effectTimers = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> lastRemaining = new Dictionary<string, float>();
    private static readonly System.Reflection.FieldInfo timeField = typeof(StatusEffect).GetField(
        "m_time", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    // Runs after the original UpdateStatusEffects method in the Hud class. Hud takes a List<StatusEffect> as a parameter
    public static void Postfix(Hud __instance, List<StatusEffect> statusEffects)
    {
        if (Player.m_localPlayer == null) return;

        Transform root = __instance.m_statusEffectListRoot;
        List<Transform> effectIcons = new List<Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == __instance.m_statusEffectTemplate) continue; //  skip the template because it's not an actual effect icon
            effectIcons.Add(child);
        }

        bool globalHide = iHUDPlugin.StatusEffectsAlwaysHide.Value;
        bool globalShow = iHUDPlugin.StatusEffectsAlwaysShow.Value;
        bool inventoryOpen = InventoryGui.IsVisible();
        float fadeStart = iHUDPlugin.StatusEffectsHideDelay.Value;
        float fadeDuration = iHUDPlugin.StatusEffectsFadeDuration.Value;
        string[] popupNames = iHUDPlugin.PopupEffectNames.Value.Split(',');
        float[] popupThresholds = System.Array.ConvertAll(iHUDPlugin.PopupReminderThresholds.Value.Split(','), s => float.Parse(s.Trim()));

        HashSet<string> currentNames = new HashSet<string>();
        foreach (var effect in statusEffects) currentNames.Add(effect.name);
        var staleKeys = new List<string>();
        foreach (var key in effectTimers.Keys)
        {
            if (!currentNames.Contains(key)) staleKeys.Add(key);
        }
        foreach (var key in staleKeys) effectTimers.Remove(key);

        int matchedCount = Mathf.Min(effectIcons.Count, statusEffects.Count);

        // Show comfort when it changes
        CheckComfortChange();

        // Loop through the current status effects icons to update their visibility and alpha
        for (int i = 0; i < effectIcons.Count; i++)
        {
            GameObject iconObject = effectIcons[i].gameObject;

            CanvasGroup cg = iconObject.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = iconObject.AddComponent<CanvasGroup>();
            }

            if (!iHUDPlugin.Enabled) { iconObject.SetActive(true); cg.alpha = 1f; continue; }
            if (i >= matchedCount) { iconObject.SetActive(false); continue; }

            StatusEffect effect = statusEffects[i];

            bool isTimed = effect.m_ttl > Epsilon;

            if (globalHide) { iconObject.SetActive(false); continue; }
            if (globalShow) { iconObject.SetActive(true); cg.alpha = 1f; continue; }

            bool isPopupEffect = System.Array.Exists(popupNames, n => n.Trim() == effect.name);

            if (isPopupEffect)
            {
                float elapsed = (float)timeField.GetValue(effect);
                float remaining = effect.m_ttl - elapsed;
                bool crossed = CrossedThreshold(effect.name, remaining, popupThresholds);
                bool isNew = !effectTimers.ContainsKey(effect.name);

                if (isNew || inventoryOpen || crossed) effectTimers[effect.name] = 0f;
                else effectTimers[effect.name] += Time.deltaTime;

                float popupT = effectTimers[effect.name];
                float popupAlpha;
                if (popupT <= fadeStart) popupAlpha = 1f;
                else if (popupT >= fadeStart + fadeDuration) popupAlpha = 0f;
                else popupAlpha = 1f - (popupT - fadeStart) / fadeDuration;

                iconObject.SetActive(popupAlpha > 0f);
                cg.alpha = popupAlpha;
                continue;
            }

            if (isTimed) { iconObject.SetActive(true); cg.alpha = 1f; continue; }

            bool isNewInfinite = !effectTimers.ContainsKey(effect.name);
            if (isNewInfinite || inventoryOpen) effectTimers[effect.name] = 0f;
            else effectTimers[effect.name] += Time.deltaTime;

            float t = effectTimers[effect.name];
            float alpha;
            if (t <= fadeStart) alpha = 1f;
            else if (t >= fadeStart + fadeDuration) alpha = 0f;
            else alpha = 1f - (t - fadeStart) / fadeDuration;

            iconObject.SetActive(alpha > 0f);
            cg.alpha = alpha;
        }
    }

    private static bool CrossedThreshold(string name, float remaining, float[] thresholds)
    {
        bool crossed = false;
        if (lastRemaining.TryGetValue(name, out float previous))
        {
            foreach (float threshold in thresholds)
            {
                if (previous > threshold && remaining <= threshold) crossed = true;
            }
        }
        lastRemaining[name] = remaining;
        return crossed;
    }

    private static void CheckComfortChange()
    {
        // iHUDPlugin.Log.LogInfo("Checking comfort change");
        Player player = Player.m_localPlayer;
        int currentComfort = player.GetComfortLevel();

        if (previousComfort.HasValue && currentComfort != previousComfort.Value)
        {
            int previous = previousComfort.Value;

            effectTimers["Rested"] = 0f; // To reset rested delay timer

            foreach (var effect in effectTimers.Keys)
                iHUDPlugin.Log.LogInfo($"Effect name: {effect}");

            iHUDPlugin.Log.LogInfo($"Comfort changed: {previous} -> {currentComfort}");
        }

        previousComfort = currentComfort;
    }
}

// [HarmonyPatch(typeof(Player), "Awake")]
// public static class Player_StatusEffectMethodDump_Patch
// {
//     private static bool hasLogged = false;

//     public static void Postfix(Player __instance)
//     {
//         if (hasLogged) return;
//         hasLogged = true;

//         var methods = typeof(Hud).GetMethods(
//             System.Reflection.BindingFlags.Instance |
//             System.Reflection.BindingFlags.Public |
//             System.Reflection.BindingFlags.NonPublic |
//             System.Reflection.BindingFlags.DeclaredOnly);

//         foreach (var method in methods)
//         {
//             string n = method.Name.ToLower();
//             if (n.Contains("status") || n.Contains("effect"))
//             {
//                 iHUDPlugin.Log.LogInfo($"Method: {method.Name}");
//             }
//         }
//     }
// }
