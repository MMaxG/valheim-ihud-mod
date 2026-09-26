using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

[BepInPlugin("iHUD", "iHUD", "1.1.0")]
public sealed class iHUDPlugin : BaseUnityPlugin
{
    internal static bool Enabled = true;

    internal static ManualLogSource Log = null!;
    internal static iHUDPlugin Instance { get; private set; } = null!;


    // ---------------------------------------------------------------------
    // General
    // ---------------------------------------------------------------------

    internal static ConfigEntry<KeyboardShortcut> ToggleHotkey = null!;
    internal static ConfigEntry<KeyboardShortcut> MinimapToggleHotkey = null!;

    internal static ConfigEntry<string> GamepadToggleModifier = null!;
    internal static ConfigEntry<string> GamepadToggleButton = null!;
    internal static ConfigEntry<string> GamepadMinimapModifier = null!;
    internal static ConfigEntry<string> GamepadMinimapButton = null!;

    // ---------------------------------------------------------------------
    // Module enable/disable
    // ---------------------------------------------------------------------

    internal static ConfigEntry<bool> HealthHandling = null!;
    internal static ConfigEntry<bool> FoodHandling = null!;
    internal static ConfigEntry<bool> StatusEffectHandling = null!;
    internal static ConfigEntry<bool> CrosshairHandling = null!;
    internal static ConfigEntry<bool> HotkeyBarHandling = null!;
    internal static ConfigEntry<bool> GuardianPowerHandling = null!;
    internal static ConfigEntry<bool> MinimapHandling = null!;
    internal static ConfigEntry<bool> ShowAllOnInventory = null!;
    internal static ConfigEntry<bool> ToggleMessage = null!;
    internal static ConfigEntry<bool> HideDamageNumbers = null!;
    internal static ConfigEntry<bool> LinkModUIs = null!;
    internal static ConfigEntry<bool> LogAllNewHudObjects = null!;
    internal static ConfigEntry<bool> ShipHudHandling = null!;
    internal static ConfigEntry<float> ShipHudHideDelay = null!;
    internal static ConfigEntry<float> ShipHudFadeDuration = null!;
    internal static ConfigEntry<bool> ShipHudHideEntirely = null!;

    // -----------------------------------------------------------------
    // Combat
    // -----------------------------------------------------------------

    internal static ConfigEntry<bool> CombatHandling = null!;
    internal static ConfigEntry<string> CombatIncludedModules = null!;
    internal static ConfigEntry<CombatDetectionMode> CombatDetection = null!;
    internal static ConfigEntry<float> CombatDetectionInterval = null!;
    internal static ConfigEntry<CombatShowMode> CombatShow = null!;
    internal static ConfigEntry<float> CombatShowDuration = null!;
    internal static ConfigEntry<float> CombatFadeInDuration = null!;
    internal static ConfigEntry<float> CombatFadeOutDuration = null!;
    internal static ConfigEntry<bool> ShipHudSpeedIndicatorHandling = null!;

    // ---------------------------------------------------------------------
    // Health
    // ---------------------------------------------------------------------

    internal static ConfigEntry<float> HealthHideDelay = null!;
    internal static ConfigEntry<float> HealthFadeDuration = null!;

    // ---------------------------------------------------------------------
    // Food
    // ---------------------------------------------------------------------

    internal static ConfigEntry<float> FoodHideDelay = null!;
    internal static ConfigEntry<float> FoodFadeDuration = null!;
    internal static ConfigEntry<float> FoodCriticalTime = null!;
    internal static ConfigEntry<float> FoodRecentlyExpiredTime = null!;

    internal static ConfigEntry<PopupIntervalMode> FoodPopupMode = null!;
    internal static ConfigEntry<string> FoodPopupIntervalsSeconds = null!;
    internal static ConfigEntry<string> FoodPopupIntervalsPercentage = null!;

    // ---------------------------------------------------------------------
    // Status effects
    // ---------------------------------------------------------------------

    internal static ConfigEntry<float> StatusEffectHideDelay = null!;
    internal static ConfigEntry<float> StatusEffectFadeDuration = null!;
    internal static ConfigEntry<float> StatusEffectCriticalTime = null!;
    internal static ConfigEntry<PopupIntervalMode> StatusEffectPopupMode = null!;
    internal static ConfigEntry<string> StatusEffectPopupIntervalsSeconds = null!;
    internal static ConfigEntry<string> StatusEffectPopupIntervalsPercentage = null!;

    // ---------------------------------------------------------------------
    // Hotkey bar
    // ---------------------------------------------------------------------

    internal static ConfigEntry<HotkeyBarMode> HotkeyBarModeSetting = null!;
    internal static ConfigEntry<float> HotkeyBarHideDelay = null!;
    internal static ConfigEntry<float> HotkeyBarFadeDuration = null!;

    // ---------------------------------------------------------------------
    // Guardian power
    // ---------------------------------------------------------------------

    internal static ConfigEntry<float> GuardianPowerHideDelay = null!;
    internal static ConfigEntry<float> GuardianPowerFadeDuration = null!;
    internal static ConfigEntry<float> GuardianPowerReadyShowDuration = null!;

    // ---------------------------------------------------------------------
    // Minimap
    // ---------------------------------------------------------------------

    internal static ConfigEntry<bool> MinimapVisible = null!;

    // ---------------------------------------------------------------------

    private void Awake()
    {
        Instance = this;

        Log = Logger;

        BindConfig();

        new Harmony("ace.iHUD").PatchAll();

        Logger.LogInfo("iHUD loaded.");
    }

    private void Update()
    {
        HandleInventoryState();
        HandleHotkeys();

        if (!Enabled)
            return;

        if (Player.m_localPlayer == null)
            return;

        HudDisplayManager.Tick();
        CombatController.Tick();
    }

    private void LateUpdate()
    {
        HotkeyBarController.LateApply();
        CombatController.LateApply();
        ModUiLinker.LateApply();
    }

    private bool previousInventoryState;

    private void HandleInventoryState()
    {
        bool current = InventoryGui.IsVisible();

        if (current != previousInventoryState)
        {
            if (current)
                HudDisplayManager.InventoryOpened();
            else
                HudDisplayManager.InventoryClosed();

            previousInventoryState = current;
        }
    }

    private static bool IsTextInputActive()
    {
        return (Chat.instance != null && Chat.instance.HasFocus()) ||
               global::Console.IsVisible() ||
               TextInput.IsVisible() ||
               Minimap.InTextInput();
    }

    private void HandleHotkeys()
    {
        // Do not react to X / V while the player types (chat, console,
        // sign text, map pin name).
        if (IsTextInputActive())
            return;

        // Every source is evaluated each frame so keyboard and gamepad can
        // be swapped freely. Keyboard is read through BepInEx (legacy input)
        // and through the Input System, in case the game suppresses one of
        // them while a gamepad is active.
        bool togglePressed =
            ToggleHotkey.Value.IsDown() |
            GamepadHotkey.KeyboardDown(ToggleHotkey.Value) |
            GamepadHotkey.ToggleDown();

        if (Debounce(ref lastToggleTime, togglePressed))
        {
            SetEnabled(!Enabled);
        }

        bool minimapPressed =
            MinimapToggleHotkey.Value.IsDown() |
            GamepadHotkey.KeyboardDown(MinimapToggleHotkey.Value) |
            GamepadHotkey.MinimapDown();

        if (Debounce(ref lastMinimapTime, minimapPressed))
        {
            if (Enabled && MinimapHandling.Value)
                MinimapController.Toggle();
        }
    }

    private static float lastToggleTime = -10f;
    private static float lastMinimapTime = -10f;

    // The same key press can be reported by legacy input and by the Input
    // System in slightly different frames. Ignore repeats within 0.25 s.
    private static bool Debounce(ref float lastTime, bool pressed)
    {
        if (!pressed)
            return false;

        float now = Time.unscaledTime;

        if (now - lastTime < 0.25f)
            return false;

        lastTime = now;
        return true;
    }

    internal static void SetEnabled(bool enabled)
    {
        Enabled = enabled;

        if (!enabled)
        {
            HudDisplayManager.RestoreDefault();
        }
        else
        {
            HudDisplayManager.ApplyCurrentState();
        }

        Log.LogInfo($"iHUD {(enabled ? "enabled" : "disabled")}");

        if (ToggleMessage.Value && Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.TopLeft,
                enabled ? "iHUD enabled" : "iHUD disabled");
        }
    }

    private void BindConfig()
    {
        // -----------------------------------------------------------------
        // General
        // -----------------------------------------------------------------

        ToggleHotkey = Config.Bind(
            "1. General",
            "Toggle HUD Hotkey",
            new KeyboardShortcut(KeyCode.H),
            "Toggle all iHUD functionality on/off.");

        MinimapToggleHotkey = Config.Bind(
            "1. General",
            "Minimap Toggle Hotkey",
            new KeyboardShortcut(KeyCode.B),
            "Toggle minimap visibility.");

        GamepadToggleModifier = Config.Bind(
            "1. General",
            "Gamepad Toggle Modifier",
            "BumperL",
            "Gamepad button that must be held for the HUD toggle combo " +
            "(BumperL = LB). Set to None to use a single button. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadToggleButton = Config.Bind(
            "1. General",
            "Gamepad Toggle Button",
            "DPadLeft",
            "Gamepad button that toggles iHUD. " +
            "Set to None to disable the gamepad toggle. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadMinimapModifier = Config.Bind(
            "1. General",
            "Gamepad Minimap Modifier",
            "BumperL",
            "Gamepad button that must be held for the minimap toggle combo " +
            "(BumperL = LB). Set to None to use a single button. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadMinimapButton = Config.Bind(
            "1. General",
            "Gamepad Minimap Button",
            "DPadRight",
            "Gamepad button that toggles the minimap. " +
            "Set to None to disable the gamepad minimap toggle. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        // -----------------------------------------------------------------
        // Modules
        // -----------------------------------------------------------------

        HealthHandling = Config.Bind(
            "2. Modules",
            "Health Handling",
            true,
            "Hide the health bar when it is not needed.");

        FoodHandling = Config.Bind(
            "2. Modules",
            "Food Handling",
            true,
            "Hide the food bar when it is not needed.");

        StatusEffectHandling = Config.Bind(
            "2. Modules",
            "Status Effect Handling",
            true,
            "Hide and selectively show status effect icons.");

        CrosshairHandling = Config.Bind(
            "2. Modules",
            "Crosshair Handling",
            true,
            "Hide the crosshair except when it is useful.");

        HotkeyBarHandling = Config.Bind(
            "2. Modules",
            "Hotkey Bar Handling",
            true,
            "Hide the hotkey bar according to the configured mode.");

        GuardianPowerHandling = Config.Bind(
            "2. Modules",
            "Guardian Power Handling",
            true,
            "Hide the guardian power bar when it is not needed.");

        MinimapHandling = Config.Bind(
            "2. Modules",
            "Minimap Handling",
            true,
            "Allow iHUD to control minimap visibility.");

        ShipHudHandling = Config.Bind(
            "2. Modules",
            "Ship HUD Handling",
            true,
            "Fade the ship HUD (sail/speed setting) when it is not needed.");

        CombatHandling = Config.Bind(
            "2. Modules",
            "Combat Handling",
            false,
            "While in combat, force the included modules to stay visible " +
            "(see the Combat section for its settings).");

        ShowAllOnInventory = Config.Bind(
            "1. General",
            "Show All On Inventory",
            true,
            "Force all HUD elements to be visible while the inventory is " +
            "open. When off, elements keep following their normal " +
            "hide/fade rules with the inventory open.");

        ToggleMessage = Config.Bind(
            "1. General",
            "Toggle Message",
            false,
            "Show a short on-screen message when iHUD is toggled on/off.");

        LinkModUIs = Config.Bind(
            "1. General",
            "Link Mod UIs",
            false,
            "Find HUD elements that other mods add to the game's HUD and " +
            "list them in the '3. Mod Linking' section. By default every " +
            "found element is hidden while iHUD is enabled (toggle iHUD off " +
            "to see them). In '3. Mod Linking' you can attach each element " +
            "to one or more iHUD modules so it shows and hides together " +
            "with them.");

        HideDamageNumbers = Config.Bind(
            "1. General",
            "Hide Damage Numbers",
            false,
            "Hide the floating damage numbers (damage dealt and taken, " +
            "healing, blocked, etc.). Only applies while iHUD is enabled.");

        // -----------------------------------------------------------------
        // Health
        // -----------------------------------------------------------------

        HealthHideDelay = Config.Bind(
            "Health",
            "Hide Delay",
            5f,
            "Seconds health remains fully visible before fading.");

        HealthFadeDuration = Config.Bind(
            "Health",
            "Fade Duration",
            4f,
            "Seconds used to fade the health bar.");

        // -----------------------------------------------------------------
        // Food
        // -----------------------------------------------------------------

        FoodHideDelay = Config.Bind(
            "Food",
            "Hide Delay",
            5f,
            "Seconds food remains fully visible before fading.");

        FoodFadeDuration = Config.Bind(
            "Food",
            "Fade Duration",
            4f,
            "Seconds used to fade the food bar.");

        FoodCriticalTime = Config.Bind(
            "Food",
            "Critical Time",
            60f,
            "Seconds remaining at which any food makes the food bar stay visible.");

        FoodRecentlyExpiredTime = Config.Bind(
            "Food",
            "Recently Expired Time",
            60f,
            "Seconds the food bar remains visible after food expires.");

        FoodPopupMode = Config.Bind(
            "Food",
            "Popup Mode",
            PopupIntervalMode.Percentage,
            "Off: no popups from Popup Intervals. Seconds: the food bar " +
            "pops up when a food's remaining time crosses one of the " +
            "'Popup Intervals Seconds' values. Percentage: it pops up " +
            "when a food's remaining time crosses one of the 'Popup " +
            "Intervals Percentage' values, as a percent of that food's " +
            "total duration.");

        FoodPopupIntervalsSeconds = Config.Bind(
            "Food",
            "Popup Intervals Seconds",
            "1200,900,600,300",
            "Comma-separated seconds remaining at which the food bar " +
            "pops up. Only used when 'Popup Mode' is Seconds.");

        FoodPopupIntervalsPercentage = Config.Bind(
            "Food",
            "Popup Intervals Percentage",
            "50",
            "Comma-separated percent of total duration remaining at " +
            "which the food bar pops up (e.g. 50,25,10). Only used when " +
            "'Popup Mode' is Percentage.");

        // -----------------------------------------------------------------
        // Status effects
        // -----------------------------------------------------------------

        StatusEffectHideDelay = Config.Bind(
            "Status Effects",
            "Hide Delay",
            5f,
            "Seconds a non-critical status effect remains fully visible before fading.");

        StatusEffectFadeDuration = Config.Bind(
            "Status Effects",
            "Fade Duration",
            4f,
            "Seconds used to fade status effect icons.");

        StatusEffectCriticalTime = Config.Bind(
            "Status Effects",
            "Critical Time",
            60f,
            "Seconds remaining below which a finite status effect stays visible until expiration.");

        StatusEffectPopupMode = Config.Bind(
            "Status Effects",
            "Popup Mode",
            PopupIntervalMode.Seconds,
            "Off: finite status effects only reappear via 'Critical Time'. " +
            "Seconds: they also reappear when remaining time crosses one " +
            "of the 'Popup Intervals Seconds' values. Percentage: they " +
            "reappear when remaining time crosses one of the 'Popup " +
            "Intervals Percentage' values, as a percent of the effect's " +
            "total duration.");

        StatusEffectPopupIntervalsSeconds = Config.Bind(
            "Status Effects",
            "Popup Intervals Seconds",
            "1200,900,600,300",
            "Comma-separated seconds remaining at which finite status " +
            "effects reappear. Only used when 'Popup Mode' is Seconds.");

        StatusEffectPopupIntervalsPercentage = Config.Bind(
            "Status Effects",
            "Popup Intervals Percentage",
            "50,25,10",
            "Comma-separated percent of total duration remaining at " +
            "which finite status effects reappear. Only used when " +
            "'Popup Mode' is Percentage.");

        // -----------------------------------------------------------------
        // Hotkey bar
        // -----------------------------------------------------------------

        HotkeyBarModeSetting = Config.Bind(
            "Hotkey Bar",
            "Mode",
            HotkeyBarMode.OnEquipChange,
            "AlwaysHidden hides the hotkey bar. OnEquipChange shows it when your equipped item changes.");

        HotkeyBarHideDelay = Config.Bind(
            "Hotkey Bar",
            "Hide Delay",
            2.25f,
            "Seconds the hotkey bar remains fully visible before fading.");

        HotkeyBarFadeDuration = Config.Bind(
            "Hotkey Bar",
            "Fade Duration",
            0.75f,
            "Seconds used to fade the hotkey bar.");

        // -----------------------------------------------------------------
        // Guardian power
        // -----------------------------------------------------------------

        GuardianPowerHideDelay = Config.Bind(
            "Guardian Power",
            "Hide Delay",
            5f,
            "Seconds the guardian power bar remains fully visible before fading.");

        GuardianPowerFadeDuration = Config.Bind(
            "Guardian Power",
            "Fade Duration",
            4f,
            "Seconds used to fade the guardian power bar.");

        GuardianPowerReadyShowDuration = Config.Bind(
            "Guardian Power",
            "Ready Show Duration",
            5f,
            "Seconds the guardian power bar remains visible when a power comes off cooldown.");

        // -----------------------------------------------------------------
        // Ship HUD
        // -----------------------------------------------------------------

        ShipHudHideDelay = Config.Bind(
            "Ship HUD",
            "Hide Delay",
            5f,
            "Seconds the ship HUD remains fully visible after the sail setting changes.");

        ShipHudFadeDuration = Config.Bind(
            "Ship HUD",
            "Fade Duration",
            4f,
            "Seconds used to fade the ship HUD.");

        ShipHudHideEntirely = Config.Bind(
            "Ship HUD",
            "Hide Entirely",
            false,
            "If enabled, the ship HUD is never shown (it does not appear when the sail setting changes).");

        ShipHudSpeedIndicatorHandling = Config.Bind(
            "Ship HUD",
            "Speed Indicator Handling",
            false,
            "If enabled, the speed arrows above the steering wheel also " +
            "fade when they are not needed. Off by default so the current " +
            "speed setting stays visible.");

        // -----------------------------------------------------------------
        // Mod linking (per-element entries are added at runtime)
        // -----------------------------------------------------------------

        LogAllNewHudObjects = Config.Bind(
            "3. Mod Linking",
            "Log All New HUD Objects",
            false,
            "Debug. Logs every object other mods add anywhere under the " +
            "game's HUD to the BepInEx log, with its full path. Useful " +
            "when a mod's UI is not found by 'Link Mod UIs'.");

        // -----------------------------------------------------------------
        // Combat
        // -----------------------------------------------------------------

        CombatIncludedModules = Config.Bind(
            "Combat",
            "Included Modules",
            "Health,Food,StatusEffects,Crosshair,Hotbar,GuardianPower,ShipHud",
            "Comma-separated list of modules to force visible during " +
            "combat. Valid values: Health, Food, StatusEffects, " +
            "Crosshair, Hotbar, GuardianPower, ShipHud. " +
            "The minimap is not affected by this module.");

        CombatDetection = Config.Bind(
            "Combat",
            "Detection Mode",
            CombatDetectionMode.Aggroed,
            "Aggroed triggers when an enemy has spotted you (red " +
            "exclamation icon). Alerted triggers earlier, as soon as an " +
            "enemy notices you and starts searching (yellow icon), so it " +
            "also includes aggroed enemies. Not limited by distance: any " +
            "loaded, active enemy counts, the same as the game's own " +
            "combat music check.");

        CombatDetectionInterval = Config.Bind(
            "Combat",
            "Detection Interval",
            1f,
            "Seconds between combat checks. Lower is more responsive " +
            "but costs a bit more performance.");

        CombatShow = Config.Bind(
            "Combat",
            "Show Mode",
            CombatShowMode.WhileInCombat,
            "WhileInCombat keeps elements visible for as long as combat " +
            "is detected, then fades them out over 'Combat Fade Out Duration'. " +
            "FixedDuration shows them for 'Show Duration' seconds after " +
            "combat starts, then fades them out even if combat continues.");

        CombatShowDuration = Config.Bind(
            "Combat",
            "Show Duration",
            8f,
            "Seconds elements stay visible after combat starts. Only " +
            "used when 'Show Mode' is FixedDuration.");

        CombatFadeInDuration = Config.Bind(
            "Combat",
            "Combat Fade In Duration",
            0.3f,
            "Seconds the included modules take to fade in when combat " +
            "starts. 0 = show instantly.");

        CombatFadeOutDuration = Config.Bind(
            "Combat",
            "Combat Fade Out Duration",
            1f,
            "Seconds the included modules take to fade out once the " +
            "combat show window ends. Starts right away, no hide delay. " +
            "0 = hide instantly.");

        // -----------------------------------------------------------------
        // Minimap
        // -----------------------------------------------------------------

        MinimapVisible = Config.Bind(
            "Minimap",
            "Visible",
            true,
            "Whether the minimap should be visible when iHUD is enabled.");

        Config.Save();
    }

    private void OnDestroy()
    {
        HudDisplayManager.RestoreDefault();
    }
}

// Reads the gamepad through Unity's Input System (the layer the game's own
// ZInput sits on). Types are accessed by reflection so the mod needs no
// reference to Unity.InputSystem.dll.
internal static class GamepadHotkey
{
    internal const string ValidNames =
        "BumperL, BumperR, TriggerL, TriggerR, StickL, StickR, " +
        "DPadLeft, DPadRight, DPadUp, DPadDown, " +
        "ButtonNorth (Y), ButtonSouth (A), ButtonWest (X), ButtonEast (B), " +
        "Start, Select";

    // Config name -> Input System control path on Gamepad.
    private static readonly Dictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BumperL", "leftShoulder" },
            { "LeftShoulder", "leftShoulder" },
            { "BumperR", "rightShoulder" },
            { "RightShoulder", "rightShoulder" },
            { "TriggerL", "leftTrigger" },
            { "LeftTrigger", "leftTrigger" },
            { "TriggerR", "rightTrigger" },
            { "RightTrigger", "rightTrigger" },
            { "StickL", "leftStickButton" },
            { "StickR", "rightStickButton" },
            { "DPadLeft", "dpad.left" },
            { "DPadRight", "dpad.right" },
            { "DPadUp", "dpad.up" },
            { "DPadDown", "dpad.down" },
            { "ButtonNorth", "buttonNorth" },
            { "ButtonY", "buttonNorth" },
            { "ButtonSouth", "buttonSouth" },
            { "ButtonA", "buttonSouth" },
            { "ButtonWest", "buttonWest" },
            { "ButtonX", "buttonWest" },
            { "ButtonEast", "buttonEast" },
            { "ButtonB", "buttonEast" },
            { "Start", "startButton" },
            { "Select", "selectButton" }
        };

    private static PropertyInfo? currentProperty;
    private static int lookupAttempts;
    private static bool gaveUp;

    private static Combo? toggleCombo;
    private static Combo? minimapCombo;

    private static Type? keyEnumType;
    private static PropertyInfo? keyboardCurrentProperty;
    private static PropertyInfo? keyboardIndexer;
    private static int keyboardLookupAttempts;
    private static bool keyboardGaveUp;

    // Keyboard shortcut read through the Input System (Keyboard.current).
    internal static bool KeyboardDown(KeyboardShortcut shortcut)
    {
        if (!EnsureKeyboard())
            return false;

        object? keyboard = keyboardCurrentProperty!.GetValue(null, null);

        if (keyboard == null)
            return false;

        object? main = GetKey(keyboard, shortcut.MainKey);

        if (main == null || !ReadBool(main, "wasPressedThisFrame"))
            return false;

        foreach (KeyCode modifierCode in shortcut.Modifiers)
        {
            object? modifier = GetKey(keyboard, modifierCode);

            if (modifier == null || !ReadBool(modifier, "isPressed"))
                return false;
        }

        return true;
    }

    private static object? GetKey(object keyboard, KeyCode code)
    {
        if (keyEnumType == null || keyboardIndexer == null)
            return null;

        string name = code.ToString();

        // KeyCode names that differ from Input System Key names.
        if (name.StartsWith("Alpha"))
            name = "Digit" + name.Substring(5);
        else if (name.StartsWith("Keypad"))
            name = "Numpad" + name.Substring(6);
        else if (name == "LeftControl")
            name = "LeftCtrl";
        else if (name == "RightControl")
            name = "RightCtrl";
        else if (name == "Return")
            name = "Enter";

        object key;

        try
        {
            key = Enum.Parse(keyEnumType, name, false);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return keyboardIndexer.GetValue(keyboard, new object[] { key });
    }

    private static bool EnsureKeyboard()
    {
        if (keyboardCurrentProperty != null && keyboardIndexer != null)
            return true;

        if (keyboardGaveUp)
            return false;

        Type? keyboardType =
            FindInputSystemType("UnityEngine.InputSystem.Keyboard");

        keyEnumType =
            FindInputSystemType("UnityEngine.InputSystem.Key");

        if (keyboardType != null && keyEnumType != null)
        {
            keyboardCurrentProperty = keyboardType.GetProperty(
                "current",
                BindingFlags.Public | BindingFlags.Static);

            keyboardIndexer = keyboardType.GetProperty(
                "Item",
                new Type[] { keyEnumType });
        }

        if (keyboardCurrentProperty != null && keyboardIndexer != null)
            return true;

        if (++keyboardLookupAttempts >= 300)
        {
            keyboardGaveUp = true;

            iHUDPlugin.Log.LogWarning(
                "Input System keyboard not found. " +
                "Using legacy keyboard input only.");
        }

        return false;
    }

    private static Type? FindInputSystemType(string fullName)
    {
        try
        {
            Type? type = Type.GetType(
                fullName + ", Unity.InputSystem",
                false);

            if (type != null)
                return type;

            foreach (Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);

                if (type != null)
                    return type;
            }
        }
        catch (Exception)
        {
            // Try again next frame.
        }

        return null;
    }

    internal static bool ToggleDown()
    {
        if (toggleCombo == null)
        {
            toggleCombo = new Combo(
                iHUDPlugin.GamepadToggleModifier,
                iHUDPlugin.GamepadToggleButton);
        }

        return WasPressed(toggleCombo);
    }

    internal static bool MinimapDown()
    {
        if (minimapCombo == null)
        {
            minimapCombo = new Combo(
                iHUDPlugin.GamepadMinimapModifier,
                iHUDPlugin.GamepadMinimapButton);
        }

        return WasPressed(minimapCombo);
    }

    private static bool WasPressed(Combo combo)
    {
        if (!EnsureInputSystem())
            return false;

        object? pad = currentProperty!.GetValue(null, null);

        // No gamepad connected.
        if (pad == null)
            return false;

        return combo.WasPressed(pad);
    }

    // One "hold modifier + press button" binding. Re-resolves and validates
    // the configured names when the config values change.
    private sealed class Combo
    {
        private readonly ConfigEntry<string> modifierEntry;
        private readonly ConfigEntry<string> buttonEntry;

        private string? cachedModifier;
        private string? cachedButton;
        private string? modifierPath;
        private string? buttonPath;

        public Combo(
            ConfigEntry<string> modifier,
            ConfigEntry<string> button)
        {
            modifierEntry = modifier;
            buttonEntry = button;
        }

        public bool WasPressed(object pad)
        {
            Refresh(pad);

            if (buttonPath == null)
                return false;

            if (modifierPath != null)
            {
                object? modifier = GetControl(pad, modifierPath);

                if (modifier == null ||
                    !ReadBool(modifier, "isPressed"))
                {
                    return false;
                }
            }

            object? button = GetControl(pad, buttonPath);

            return button != null &&
                   ReadBool(button, "wasPressedThisFrame");
        }

        private void Refresh(object pad)
        {
            if (modifierEntry.Value == cachedModifier &&
                buttonEntry.Value == cachedButton)
            {
                return;
            }

            cachedModifier = modifierEntry.Value;
            cachedButton = buttonEntry.Value;

            modifierPath = ResolveAndValidate(
                pad,
                cachedModifier,
                modifierEntry.Definition.Key);

            buttonPath = ResolveAndValidate(
                pad,
                cachedButton,
                buttonEntry.Definition.Key);
        }
    }

    private static string? ResolveAndValidate(
        object pad,
        string configured,
        string settingName)
    {
        string name = (configured ?? "").Trim();

        if (name.Length == 0 ||
            name.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path =
            Aliases.TryGetValue(name, out string? alias)
                ? alias
                : name;

        if (GetControl(pad, path) == null)
        {
            iHUDPlugin.Log.LogError(
                $"Unknown gamepad button '{configured}' " +
                $"for '{settingName}'. Valid values: {ValidNames}");

            return null;
        }

        return path;
    }

    private static object? GetControl(object pad, string path)
    {
        object? current = pad;

        foreach (string part in path.Split('.'))
        {
            PropertyInfo? property =
                current.GetType().GetProperty(
                    part,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            if (property == null)
                return null;

            current = property.GetValue(current, null);

            if (current == null)
                return null;
        }

        return current;
    }

    private static bool ReadBool(object control, string member)
    {
        PropertyInfo? property =
            control.GetType().GetProperty(
                member,
                BindingFlags.Public | BindingFlags.Instance);

        return property != null &&
               property.GetValue(control, null) is bool value &&
               value;
    }

    private static bool EnsureInputSystem()
    {
        if (currentProperty != null)
            return true;

        if (gaveUp)
            return false;

        Type? gamepadType = null;

        try
        {
            gamepadType = Type.GetType(
                "UnityEngine.InputSystem.Gamepad, Unity.InputSystem",
                false);

            if (gamepadType == null)
            {
                foreach (Assembly assembly in
                         AppDomain.CurrentDomain.GetAssemblies())
                {
                    gamepadType = assembly.GetType(
                        "UnityEngine.InputSystem.Gamepad",
                        false);

                    if (gamepadType != null)
                        break;
                }
            }
        }
        catch (Exception)
        {
            // Try again next frame.
        }

        currentProperty = gamepadType?.GetProperty(
            "current",
            BindingFlags.Public | BindingFlags.Static);

        if (currentProperty != null)
            return true;

        // The assembly may not be loaded yet right after startup.
        if (++lookupAttempts >= 300)
        {
            gaveUp = true;

            iHUDPlugin.Log.LogError(
                "Unity Input System (Gamepad) not found. " +
                "Gamepad hotkey disabled.");
        }

        return false;
    }
}

internal enum CombatDetectionMode
{
    Aggroed,
    Alerted
}

internal enum PopupIntervalMode
{
    Off,
    Seconds,
    Percentage
}

internal enum CombatShowMode
{
    WhileInCombat,
    FixedDuration
}

internal enum HotkeyBarMode
{
    AlwaysHidden,
    OnEquipChange
}

// Shared logic for the Food/Status Effects "popup at X remaining" feature.
internal static class PopupIntervals
{
    public static float[] Active(
        PopupIntervalMode mode, string secondsCsv, string percentCsv)
    {
        return Parse(
            mode == PopupIntervalMode.Percentage ? percentCsv : secondsCsv);
    }

    public static float[] Parse(string csv)
    {
        return csv.Split(',')
            .Select(p => float.TryParse(p.Trim(), out float v) ? v : -1f)
            .Where(v => v >= 0f)
            .ToArray();
    }

    // Converts a remaining-time value into whatever unit the thresholds
    // are configured in.
    public static float ToValue(
        float remaining, float totalDuration, PopupIntervalMode mode)
    {
        return mode == PopupIntervalMode.Percentage && totalDuration > 0.01f
            ? remaining / totalDuration * 100f
            : remaining;
    }

    // True if the value dropped from above one of the thresholds to at or
    // below it this frame.
    public static bool Crossed(
        float previous, float current, float[] thresholds)
    {
        foreach (float threshold in thresholds)
        {
            if (previous > threshold && current <= threshold)
                return true;
        }

        return false;
    }
}

internal sealed class HudFadeState
{
    private float elapsed = float.MaxValue;

    public bool IsShowing => elapsed != float.MaxValue;

    public void Show()
    {
        elapsed = 0f;
    }

    public void Hide()
    {
        elapsed = float.MaxValue;
    }

    public void Tick()
    {
        if (elapsed != float.MaxValue)
            elapsed += Time.deltaTime;
    }

    public float GetAlpha(float delay, float fadeDuration)
    {
        if (elapsed == float.MaxValue)
            return 0f;

        if (elapsed <= delay)
            return 1f;

        if (fadeDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(
            1f - ((elapsed - delay) / fadeDuration));
    }
}
internal static class HudAlpha
{
    public static CanvasGroup Get(GameObject obj)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();

        if (group == null)
            group = obj.AddComponent<CanvasGroup>();

        return group;
    }

    public static void Set(GameObject obj, float alpha)
    {
        Get(obj).alpha = alpha;
    }
}
internal static class HudDisplayManager
{
    private static bool inventoryOpen;

    public static void Tick()
    {
        if (!iHUDPlugin.Enabled)
            return;

        HealthController.Tick();
        FoodController.Tick();
        StatusEffectController.Tick();
        HotkeyBarController.Tick();
        GuardianPowerController.Tick();
        CrosshairController.Tick();
        MinimapController.Tick();
    }

    public static void InventoryOpened()
    {
        inventoryOpen = true;

        if (!iHUDPlugin.Enabled || !iHUDPlugin.ShowAllOnInventory.Value)
            return;

        HealthController.ShowImmediately();
        FoodController.ShowImmediately();
        StatusEffectController.ShowImmediately();
        HotkeyBarController.ShowImmediately();
        GuardianPowerController.ShowImmediately();
        ShipHudController.ShowImmediately();
        CrosshairController.ShowImmediately();

        // Minimap intentionally excluded.
    }

    public static void InventoryClosed()
    {
        inventoryOpen = false;

        if (!iHUDPlugin.Enabled || !iHUDPlugin.ShowAllOnInventory.Value)
            return;

        // Inventory closing hides everything immediately.
        HealthController.HideImmediately();
        FoodController.HideImmediately();
        StatusEffectController.HideImmediately();
        HotkeyBarController.HideImmediately();
        GuardianPowerController.HideImmediately();
        ShipHudController.HideImmediately();
        CrosshairController.HideImmediately();
    }

    // True only while the inventory is open AND the player wants all
    // elements forced visible ("Show All On Inventory"). Controllers use
    // this to skip their normal fade logic.
    public static bool IsInventoryOpen =>
        inventoryOpen && iHUDPlugin.ShowAllOnInventory.Value;

    public static void RestoreDefault()
    {
        HealthController.RestoreDefault();
        FoodController.RestoreDefault();
        StatusEffectController.RestoreDefault();
        HotkeyBarController.RestoreDefault();
        GuardianPowerController.RestoreDefault();
        ShipHudController.RestoreDefault();
        CrosshairController.RestoreDefault();
        MinimapController.RestoreDefault();
    }

    public static void ApplyCurrentState()
    {
        MinimapController.ApplySavedState();

        // Everything else will be evaluated by its normal Harmony update.
    }
}
// Forces a configurable set of modules to stay visible while the player is
// in combat, as an alternative to waiting for each module's own fade rules.
// Detection is a periodic (not per-frame) search of nearby characters for
// ones actively hunting ("aggroed") or merely alerted to the player,
// depending on config. No per-mod patches: it only calls each controller's
// existing public ShowImmediately(), the same entry point used for
// "inventory open".
internal static class CombatController
{
    // Config module name -> the controller's own ShowImmediately(). Kept in
    // the same order as HudDisplayManager.InventoryOpened for consistency.
    // The minimap intentionally has no entry: it is a visibility toggle,
    // not a fading element, so "forcing it visible" would not make sense.
    private static readonly (string Name, Action<float> Apply)[] AllModules =
    {
        ("Health", HealthController.ApplyCombatAlpha),
        ("Food", FoodController.ApplyCombatAlpha),
        ("StatusEffects", StatusEffectController.ApplyCombatAlpha),
        ("Crosshair", CrosshairController.ApplyCombatAlpha),
        ("Hotbar", HotkeyBarController.ApplyCombatAlpha),
        ("GuardianPower", GuardianPowerController.ApplyCombatAlpha),
        ("ShipHud", ShipHudController.ApplyCombatAlpha)
    };

    private static string cachedIncludedConfig = "";
    private static readonly List<Action<float>> includedApplyActions =
        new List<Action<float>>();

    // Combat's own fade value (0..1). Modules keep their own fade state;
    // combat only raises their alpha to at least this value.
    private static float combatAlpha;

    // BaseAI.GetAllInstances(), BaseAI.IsEnemy(a, b), BaseAI.IsAlerted() and
    // BaseAI.GetTargetCreature() are public game API (confirmed against
    // decompiled source), so no reflection is needed here. GetAllInstances
    // only contains currently loaded/active creatures, so this is naturally
    // limited to whatever the game itself keeps simulated - no separate
    // radius is applied.
    private static float nextScanTime;
    private static bool wasDetected;
    private static float forcedUntil;

    // Called every frame from iHUDPlugin.Update.
    public static void Tick()
    {
        if (!iHUDPlugin.CombatHandling.Value || !iHUDPlugin.Enabled)
        {
            forcedUntil = 0f;
            wasDetected = false;
            return;
        }

        Player? player = Player.m_localPlayer;

        if (player == null)
            return;

        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime =
            Time.unscaledTime +
            Mathf.Max(0.1f, iHUDPlugin.CombatDetectionInterval.Value);

        bool detected = DetectCombat();

        if (iHUDPlugin.CombatShow.Value == CombatShowMode.WhileInCombat)
        {
            // Refresh a short rolling window every time combat is still
            // detected. Once detection stops, the window runs out within
            // roughly one scan interval and each module resumes its own
            // fade rules from there.
            if (detected)
            {
                forcedUntil =
                    Time.unscaledTime +
                    Mathf.Max(
                        1.5f,
                        iHUDPlugin.CombatDetectionInterval.Value * 1.5f);
            }
        }
        else
        {
            // Only start (or restart) the fixed window on a fresh
            // transition into combat. Once it runs out, it is not
            // refreshed again until combat is left and re-entered, even
            // if the fight is still ongoing.
            if (detected && !wasDetected)
            {
                forcedUntil =
                    Time.unscaledTime +
                    Mathf.Max(0f, iHUDPlugin.CombatShowDuration.Value);
            }
        }

        wasDetected = detected;
    }

    // Called every frame from iHUDPlugin.LateUpdate, after every included
    // controller has already computed and applied its own alpha for this
    // frame, so the forced "visible" wins for as long as it is active.
    public static float GetAlpha()
    {
        return combatAlpha;
    }

    public static void LateApply()
    {
        if (!iHUDPlugin.CombatHandling.Value || !iHUDPlugin.Enabled)
        {
            combatAlpha = 0f;
            return;
        }

        bool active = Time.unscaledTime < forcedUntil;
        float target = active ? 1f : 0f;

        float duration = active
            ? iHUDPlugin.CombatFadeInDuration.Value
            : iHUDPlugin.CombatFadeOutDuration.Value;

        if (duration <= 0f)
            combatAlpha = target;
        else
            combatAlpha = Mathf.MoveTowards(
                combatAlpha,
                target,
                Time.unscaledDeltaTime / duration);

        if (combatAlpha <= 0f)
            return;

        RefreshIncludedModules();

        foreach (Action<float> apply in includedApplyActions)
            apply(combatAlpha);
    }

    private static void RefreshIncludedModules()
    {
        string config = iHUDPlugin.CombatIncludedModules.Value;

        if (config == cachedIncludedConfig)
            return;

        cachedIncludedConfig = config;
        includedApplyActions.Clear();

        HashSet<string> wanted =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in config.Split(','))
        {
            string name = part.Trim();

            if (name.Length > 0)
                wanted.Add(name);
        }

        foreach ((string Name, Action<float> Apply) module in AllModules)
        {
            if (wanted.Contains(module.Name))
                includedApplyActions.Add(module.Apply);
        }
    }

    private static bool DetectCombat()
    {
        Player player = Player.m_localPlayer;
        bool useAlerted =
            iHUDPlugin.CombatDetection.Value == CombatDetectionMode.Alerted;

        foreach (BaseAI ai in BaseAI.GetAllInstances())
        {
            if (ai == null)
                continue;

            Character character = ai.GetComponent<Character>();

            if (character == null || !BaseAI.IsEnemy(player, character))
                continue;

            // Enemy has the player as target: it heard/sensed the player
            // (yellow icon) or has fully spotted the player (red icon).
            if (ai.GetTargetCreature() != player)
                continue;

            // Alerted: any enemy targeting the player, yellow or red.
            if (useAlerted)
                return true;

            // Aggroed: enemy has seen the player (red exclamation icon).
            if (ai.IsAlerted())
                return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Hud), "UpdateHealth")]
internal static class HealthController
{
    private const float Epsilon = 0.01f;

    private static readonly HudFadeState state = new HudFadeState();

    private static CanvasGroup? healthBar;
    private static CanvasGroup? healthIcon;

    private static Hud? hud;

    public static void Postfix(Hud __instance, Player player)
    {
        if (player != Player.m_localPlayer || player == null)
            return;

        hud = __instance;

        if (!iHUDPlugin.HealthHandling.Value)
        {
            RestoreDefault();
            return;
        }

        EnsureGroups(__instance);

        if (!iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        float health = player.GetHealth();
        float maxHealth = player.GetMaxHealth();

        if (health < maxHealth - Epsilon)
            state.Show();

        state.Tick();

        float alpha = state.GetAlpha(
            iHUDPlugin.HealthHideDelay.Value,
            iHUDPlugin.HealthFadeDuration.Value);

        healthBar!.alpha = alpha;

        if (healthIcon != null)
            healthIcon.alpha = alpha;
    }

    public static float GetAlpha()
    {
        float alpha = -1f;

        if (healthBar != null)
            alpha = Mathf.Max(alpha, healthBar.alpha);

        if (healthIcon != null)
            alpha = Mathf.Max(alpha, healthIcon.alpha);

        return alpha < 0f ? 1f : alpha;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        if (healthBar != null)
            healthBar.alpha = Mathf.Max(healthBar.alpha, alpha);

        if (healthIcon != null)
            healthIcon.alpha = Mathf.Max(healthIcon.alpha, alpha);
    }

    public static void ShowImmediately()
    {
        state.Show();

        if (healthBar != null)
            healthBar.alpha = 1f;

        if (healthIcon != null)
            healthIcon.alpha = 1f;
    }

    public static void HideImmediately()
    {
        state.Hide();

        if (healthBar != null)
            healthBar.alpha = 0f;

        if (healthIcon != null)
            healthIcon.alpha = 0f;
    }

    public static void RestoreDefault()
    {
        state.Hide();

        if (healthBar != null)
            healthBar.alpha = 1f;

        if (healthIcon != null)
            healthIcon.alpha = 1f;
    }

    public static void Tick()
    {
        // Health is updated through Hud.UpdateHealth.
    }

    private static void EnsureGroups(Hud hud)
    {
        if (healthBar == null)
        {
            healthBar = HudAlpha.Get(
                hud.m_healthBarRoot.gameObject);
        }

        if (healthIcon == null)
        {
            Transform? icon =
                hud.m_healthBarRoot.transform.parent.Find("healthicon");

            if (icon != null)
                healthIcon = HudAlpha.Get(icon.gameObject);
        }
    }
}
[HarmonyPatch(typeof(Hud), "UpdateFood")]
internal static class FoodController
{
    private const float Epsilon = 0.01f;

    private static readonly HudFadeState state = new HudFadeState();

    private static readonly List<CanvasGroup> groups =
        new List<CanvasGroup>();

    private static readonly string[] childNames =
    {
        "Food",
        "FoodText",
        "foodicon",
        "foodicon (1)",
        "food0",
        "food1",
        "food2"
    };

    private static int previousFoodCount = -1;

    private static float recentlyExpiredTimer =
        float.MaxValue;

    private static Dictionary<float, bool> popupThresholds =
        new Dictionary<float, bool>();

    public static void Postfix(Hud __instance, Player player)
    {
        if (player != Player.m_localPlayer || player == null)
            return;

        EnsureGroups(__instance.m_healthBarRoot.parent);

        if (!iHUDPlugin.FoodHandling.Value)
        {
            RestoreDefault();
            return;
        }

        if (!iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        UpdateFoodState(player);

        state.Tick();

        recentlyExpiredTimer += Time.deltaTime;

        float alpha = state.GetAlpha(
            iHUDPlugin.FoodHideDelay.Value,
            iHUDPlugin.FoodFadeDuration.Value);

        SetAlpha(alpha);

        previousFoodCount = player.GetFoods().Count;
    }

    private static void UpdateFoodState(Player player)
    {
        List<Player.Food> foods = player.GetFoods();

        int count = foods.Count;

        if (previousFoodCount >= 0 && count < previousFoodCount)
        {
            recentlyExpiredTimer = 0f;
            state.Show();
        }

        bool criticalFood = false;

        foreach (Player.Food food in foods)
        {
            if (food.m_time <= iHUDPlugin.FoodCriticalTime.Value)
            {
                criticalFood = true;
                break;
            }
        }

        if (criticalFood)
        {
            state.Show();
        }

        if (recentlyExpiredTimer <=
            iHUDPlugin.FoodRecentlyExpiredTime.Value)
        {
            state.Show();
        }

        if (iHUDPlugin.FoodPopupMode.Value != PopupIntervalMode.Off)
        {
            CheckPopupIntervals(foods);
        }
    }

    private static void CheckPopupIntervals(List<Player.Food> foods)
    {
        PopupIntervalMode mode = iHUDPlugin.FoodPopupMode.Value;

        float[] thresholds = PopupIntervals.Active(
            mode,
            iHUDPlugin.FoodPopupIntervalsSeconds.Value,
            iHUDPlugin.FoodPopupIntervalsPercentage.Value);

        foreach (Player.Food food in foods)
        {
            float total = food.m_item?.m_shared?.m_foodBurnTime ?? 0f;

            float previous = PopupIntervals.ToValue(
                food.m_time + Time.deltaTime, total, mode);

            float current =
                PopupIntervals.ToValue(food.m_time, total, mode);

            if (PopupIntervals.Crossed(previous, current, thresholds))
                state.Show();
        }
    }

    public static void NotifyFoodEaten()
    {
        recentlyExpiredTimer = float.MaxValue;
        state.Show();
    }

    public static float GetAlpha()
    {
        float alpha = -1f;

        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                alpha = Mathf.Max(alpha, group.alpha);
        }

        return alpha < 0f ? 1f : alpha;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, alpha);
        }
    }

    public static void ShowImmediately()
    {
        state.Show();
        SetAlpha(1f);
    }

    public static void HideImmediately()
    {
        state.Hide();
        SetAlpha(0f);
    }

    public static void RestoreDefault()
    {
        state.Hide();
        SetAlpha(1f);
    }

    public static void Tick()
    {
        // Food is updated through Hud.UpdateFood.
    }

    private static void SetAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }

    private static void EnsureGroups(Transform parent)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            Transform? child = parent.Find(childNames[i]);

            if (child == null)
                continue;

            CanvasGroup group =
                HudAlpha.Get(child.gameObject);

            if (!groups.Contains(group))
                groups.Add(group);
        }
    }

}
[HarmonyPatch(typeof(Player), "EatFood")]
internal static class Player_EatFood_Patch
{
    public static void Postfix(Player __instance, bool __result)
    {
        if (__instance != Player.m_localPlayer)
            return;

        if (!__result)
            return;

        FoodController.NotifyFoodEaten();
    }
}
internal sealed class StatusEffectDisplayState
{
    public string Name = "";

    public float PreviousRemaining;

    public bool Initialized;

    public bool Critical;

    public readonly HudFadeState Fade =
        new HudFadeState();
}
[HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
internal static class StatusEffectController
{
    private const float Epsilon = 0.01f;

    private static readonly Dictionary<string, StatusEffectDisplayState>
        states =
            new Dictionary<string, StatusEffectDisplayState>();

    private static readonly Dictionary<string, CanvasGroup>
        iconGroups =
            new Dictionary<string, CanvasGroup>();

    private static int? lastComfortLevel;

    private static readonly FieldInfo? TimeField =
        typeof(StatusEffect).GetField(
            "m_time",
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);

    public static void Postfix(
        Hud __instance,
        List<StatusEffect> statusEffects)
    {
        if (Player.m_localPlayer == null)
            return;

        if (!iHUDPlugin.StatusEffectHandling.Value)
        {
            RestoreDefault();
            ResetTracking();
            return;
        }

        // While the mod is toggled off we keep tracking fade timers so the
        // previous state resumes correctly when it is toggled on again.
        // Only the alpha is not applied (icons stay fully visible).
        bool enabled = iHUDPlugin.Enabled;

        UpdateComfort();

        if (enabled && HudDisplayManager.IsInventoryOpen)
        {
            ShowAll(statusEffects, __instance);
            return;
        }

        PopupIntervalMode popupMode =
            iHUDPlugin.StatusEffectPopupMode.Value;

        float[] popupIntervals = PopupIntervals.Active(
            popupMode,
            iHUDPlugin.StatusEffectPopupIntervalsSeconds.Value,
            iHUDPlugin.StatusEffectPopupIntervalsPercentage.Value);

        float criticalTime =
            iHUDPlugin.StatusEffectCriticalTime.Value;

        HashSet<string> currentNames =
            new HashSet<string>(
                statusEffects.Select(e => e.name));

        RemoveStaleStates(currentNames);

        Dictionary<string, Transform> iconByName =
            FindIcons(__instance, statusEffects);

        foreach (StatusEffect effect in statusEffects)
        {
            StatusEffectDisplayState state =
                GetOrCreateState(effect);

            float remaining =
                GetRemaining(effect);

            if (!state.Initialized)
            {
                InitializeState(state, effect, remaining);

                // Newly acquired effects are shown.
                state.Fade.Show();
            }
            else
            {
                UpdateEffectState(
                    state,
                    effect,
                    remaining,
                    popupMode,
                    popupIntervals,
                    criticalTime);
            }

            state.PreviousRemaining = remaining;
            state.Initialized = true;

            if (!iconByName.TryGetValue(
                effect.name,
                out Transform? icon))
            {
                continue;
            }

            CanvasGroup group =
                HudAlpha.Get(icon.gameObject);

            iconGroups[effect.name] = group;

            float alpha;

            if (state.Critical)
            {
                alpha = 1f;
            }
            else
            {
                state.Fade.Tick();

                alpha = state.Fade.GetAlpha(
                    iHUDPlugin.StatusEffectHideDelay.Value,
                    iHUDPlugin.StatusEffectFadeDuration.Value);
            }

            group.alpha = enabled ? alpha : 1f;
        }
    }

    private static void UpdateComfort()
    {
        Player player = Player.m_localPlayer;

        if (player == null)
            return;

        int currentComfort =
            player.GetComfortLevel();

        if (lastComfortLevel.HasValue &&
            currentComfort != lastComfortLevel.Value)
        {
            // Resting is special:
            // whenever comfort changes, show it again.
            ShowEffect("Resting");
        }

        lastComfortLevel = currentComfort;
    }

    private static void InitializeState(
        StatusEffectDisplayState state, StatusEffect effect, float remaining)
    {
        state.Name = effect.name;
        state.PreviousRemaining = remaining;
        state.Initialized = true;

        state.Critical =
            effect.m_ttl > Epsilon &&
            remaining <= iHUDPlugin.StatusEffectCriticalTime.Value;
    }

    private static void UpdateEffectState(
        StatusEffectDisplayState state,
        StatusEffect effect,
        float remaining,
        PopupIntervalMode mode,
        float[] intervals,
        float criticalTime)
    {
        // Infinite / condition-based effects simply use the normal Show()
        // timer.
        if (effect.m_ttl <= Epsilon)
            return;

        if (remaining <= criticalTime)
        {
            if (!state.Critical)
            {
                state.Critical = true;
                state.Fade.Show();
            }

            return;
        }

        // Finite effect, not critical. Protects against effects being
        // refreshed with more time.
        state.Critical = false;

        if (mode == PopupIntervalMode.Off)
            return;

        float previousValue =
            PopupIntervals.ToValue(state.PreviousRemaining, effect.m_ttl, mode);

        float currentValue =
            PopupIntervals.ToValue(remaining, effect.m_ttl, mode);

        if (PopupIntervals.Crossed(previousValue, currentValue, intervals))
            state.Fade.Show();
    }

    private static void ShowEffect(string name)
    {
        if (!states.TryGetValue(
            name,
            out StatusEffectDisplayState? state))
        {
            return;
        }

        state.Fade.Show();

        if (name == "Resting")
        {
            // Resting is not necessarily critical.
            // We only want to reset its display timer.
            state.Critical = false;
        }
    }

    private static float GetRemaining(StatusEffect effect)
    {
        if (effect.m_ttl <= Epsilon)
            return float.PositiveInfinity;

        if (TimeField == null)
            return effect.m_ttl;

        float elapsed =
            (float)TimeField.GetValue(effect);

        return effect.m_ttl - elapsed;
    }

    private static StatusEffectDisplayState GetOrCreateState(
        StatusEffect effect)
    {
        if (!states.TryGetValue(
            effect.name,
            out StatusEffectDisplayState? state))
        {
            state = new StatusEffectDisplayState
            {
                Name = effect.name
            };

            states[effect.name] = state;
        }

        return state;
    }

    private static void RemoveStaleStates(
        HashSet<string> currentNames)
    {
        List<string> stale =
            states.Keys
                .Where(k => !currentNames.Contains(k))
                .ToList();

        foreach (string name in stale)
        {
            states.Remove(name);
            iconGroups.Remove(name);
        }
    }

    // Hud keeps its icon instances in a private list. Hud.UpdateStatusEffects
    // fills it in the same order as the statusEffects list it receives, so
    // icons[i] belongs to effects[i].
    private static readonly FieldInfo? IconListField =
        AccessTools.Field(typeof(Hud), "m_statusEffects");

    private static Dictionary<string, Transform> FindIcons(
        Hud hud,
        List<StatusEffect> effects)
    {
        Dictionary<string, Transform> result =
            new Dictionary<string, Transform>();

        List<RectTransform>? icons =
            IconListField?.GetValue(hud) as List<RectTransform>;

        if (icons == null)
            return result;

        int count = Math.Min(icons.Count, effects.Count);

        for (int i = 0; i < count; i++)
        {
            if (icons[i] != null)
                result[effects[i].name] = icons[i];
        }

        return result;
    }

    private static void ShowAll(
        List<StatusEffect> effects,
        Hud hud)
    {
        Transform root =
            hud.m_statusEffectListRoot;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child =
                root.GetChild(i);

            if (child == hud.m_statusEffectTemplate)
                continue;

            HudAlpha.Set(
                child.gameObject,
                1f);
        }
    }

    public static float GetAlpha()
    {
        float alpha = -1f;

        foreach (CanvasGroup group in iconGroups.Values)
        {
            if (group != null)
                alpha = Mathf.Max(alpha, group.alpha);
        }

        return alpha < 0f ? 0f : alpha;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        foreach (CanvasGroup group in iconGroups.Values)
        {
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, alpha);
        }
    }

    public static void ShowImmediately()
    {
        foreach (CanvasGroup group in iconGroups.Values)
        {
            if (group != null)
                group.alpha = 1f;
        }
    }

    public static void HideImmediately()
    {
        foreach (CanvasGroup group in iconGroups.Values)
        {
            if (group != null)
                group.alpha = 0f;
        }
    }

    public static void RestoreDefault()
    {
        foreach (CanvasGroup group in iconGroups.Values)
        {
            if (group != null)
                group.alpha = 1f;
        }
    }

    private static void ResetTracking()
    {
        states.Clear();
        lastComfortLevel = null;
    }

    public static void Tick()
    {
        // Status effects are updated by Hud.UpdateStatusEffects.
    }

}
[HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
internal static class CrosshairController
{
    private static CanvasGroup? group;

    public static void Postfix(
        Hud __instance,
        Player player)
    {
        if (player != Player.m_localPlayer ||
            player == null)
            return;

        GameObject obj =
            __instance.m_crosshair.gameObject;

        group = HudAlpha.Get(obj);

        if (!iHUDPlugin.CrosshairHandling.Value ||
            !iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        bool interactable =
            player.GetHoverObject() != null;

        bool ranged =
            IsUsingRangedWeapon(player);

        group.alpha =
            interactable || ranged
                ? 1f
                : 0f;
    }

    public static float GetAlpha()
    {
        return group != null ? group.alpha : 1f;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        if (group != null)
            group.alpha = Mathf.Max(group.alpha, alpha);
    }

    public static void ShowImmediately()
    {
        if (group != null)
            group.alpha = 1f;
    }

    public static void HideImmediately()
    {
        if (group != null)
            group.alpha = 0f;
    }

    public static void RestoreDefault()
    {
        if (group != null)
            group.alpha = 1f;
    }

    public static void Tick()
    {
    }

    private static bool IsUsingRangedWeapon(
        Player player)
    {
        ItemDrop.ItemData weapon =
            player.GetCurrentWeapon();

        if (weapon == null)
            return false;

        var shared = weapon.m_shared;

        bool projectile =
            shared.m_attack.m_attackType ==
            Attack.AttackType.Projectile;

        // Spears throw with their secondary attack.
        bool secondaryProjectile =
            shared.m_secondaryAttack != null &&
            shared.m_secondaryAttack.m_attackType ==
            Attack.AttackType.Projectile;

        bool ammo =
            shared.m_ammoType != string.Empty;

        return projectile || secondaryProjectile || ammo;
    }
}
// The hotbar number keys (1-9) toggle equip/unequip through this method.
// Sheathing with R goes through a different path and is not affected.
// Any postfix run means the player pressed a hotbar key, whether or not it
// changed what's equipped, so the hotbar should show either way.
[HarmonyPatch]
internal static class Player_UseHotbarItem_Patch
{
    private static readonly MethodBase? target =
        AccessTools.Method(typeof(Player), "UseHotbarItem", new[] { typeof(int) });

    private static bool Prepare()
    {
        if (target == null)
        {
            iHUDPlugin.Log.LogWarning(
                "Player.UseHotbarItem(int) not found. Hotbar keys will " +
                "only show the hotkey bar when they cause an item to be " +
                "equipped.");
        }

        return target != null;
    }

    private static MethodBase TargetMethod()
    {
        return target!;
    }

    public static void Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
            return;

        HotkeyBarController.NotifyHotbarKeyPressed();
    }
}

[HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
internal static class HotkeyBarController
{
    private static readonly HudFadeState state =
        new HudFadeState();

    // Every HotkeyBar we have seen. Other mods can add extra bars.
    private static readonly List<CanvasGroup> groups =
        new List<CanvasGroup>();

    // Hand items (weapons, tools, shields, torches...). Humanoid keeps them
    // in protected fields, so they are read by reflection.
    private static readonly FieldInfo? RightItemField =
        AccessTools.Field(typeof(Humanoid), "m_rightItem");

    private static readonly FieldInfo? LeftItemField =
        AccessTools.Field(typeof(Humanoid), "m_leftItem");

    private static bool hasPreviousItems;
    private static int previousRightHash;
    private static int previousLeftHash;

    // True while iHUD is driving the bar's alpha. Used to restore the
    // default once (instead of every frame) when iHUD stops controlling it,
    // so other mods can control the bar in that case.
    private static bool controlling;

    // Set by Player_UseHotbarItem_Patch whenever the player presses a 1-9
    // hotbar key, whether that equips or unequips the item. Consumed (and
    // reset) on the next Apply(), regardless of mode or module state, so it
    // never carries over to a later frame.
    private static bool hotbarKeyPressed;

    // HotkeyBar keeps the selected slot (the controller's D-pad cursor) in a
    // private field. A change means the player is swapping items.
    private static readonly FieldInfo? SelectedField =
        AccessTools.Field(typeof(HotkeyBar), "m_selected");

    private static readonly Dictionary<HotkeyBar, int> previousSelected =
        new Dictionary<HotkeyBar, int>();

    private static void DetectSelectionChange()
    {
        if (SelectedField == null)
            return;

        List<HotkeyBar> dead = previousSelected.Keys
            .Where(b => b == null)
            .ToList();

        foreach (HotkeyBar bar in dead)
            previousSelected.Remove(bar);

        foreach (HotkeyBar bar in bars)
        {
            if (bar == null ||
                !(SelectedField.GetValue(bar) is int selected))
            {
                continue;
            }

            if (previousSelected.TryGetValue(bar, out int previous) &&
                previous != selected)
            {
                hotbarKeyPressed = true;
            }

            previousSelected[bar] = selected;
        }
    }

    private static readonly List<HotkeyBar> bars =
        new List<HotkeyBar>();

    internal static void NotifyHotbarKeyPressed()
    {
        hotbarKeyPressed = true;
    }

    // HotkeyBar.UpdateIcons is not guaranteed to run every frame (other
    // mods can make it event-driven), so it is only used to discover the
    // bar. The fade itself is driven by Tick (Update) and LateApply
    // (LateUpdate), which run every frame.
    public static void Postfix(HotkeyBar __instance)
    {
        bars.RemoveAll(b => b == null);

        if (!bars.Contains(__instance))
            bars.Add(__instance);

        Register(
            HudAlpha.Get(
                __instance.gameObject));

        Apply();
    }

    // Called every frame from iHUDPlugin.Update via HudDisplayManager.
    public static void Tick()
    {
        state.Tick();
        Apply();
    }

    // Called every frame from iHUDPlugin.LateUpdate, so our alpha is applied
    // after other mods have run their own Update code.
    public static void LateApply()
    {
        Apply();
    }

    private static void Register(CanvasGroup group)
    {
        groups.RemoveAll(g => g == null);

        if (!groups.Contains(group))
            groups.Add(group);
    }

    private static void Apply()
    {
        // Always consumed, even if nothing below ends up using it, so a key
        // press while the bar is hidden/disabled never leaks into a later
        // frame once handling resumes.
        DetectSelectionChange();

        bool hotkeyPressed = hotbarKeyPressed;
        hotbarKeyPressed = false;

        if (groups.Count == 0)
            return;

        if (!iHUDPlugin.HotkeyBarHandling.Value ||
            !iHUDPlugin.Enabled)
        {
            if (controlling)
            {
                RestoreDefault();
                controlling = false;
            }

            return;
        }

        controlling = true;

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        Player? player =
            Player.m_localPlayer;

        if (player == null)
            return;

        if (iHUDPlugin.HotkeyBarModeSetting.Value ==
            HotkeyBarMode.AlwaysHidden)
        {
            SetAlpha(0f);
            return;
        }

        GetHandItemHashes(player, out int rightHash, out int leftHash);

        if (!hasPreviousItems)
        {
            hasPreviousItems = true;
        }
        else if (rightHash != previousRightHash ||
                 leftHash != previousLeftHash)
        {
            // 0 = empty hand. Equipping something (a hand that now holds a
            // different item) shows the bar. Sheathing (R) does not. A
            // hotbar key (1-9) always shows the bar, even when it results
            // in unequipping, since it's a direct hotbar action.
            bool equipped =
                (rightHash != 0 && rightHash != previousRightHash) ||
                (leftHash != 0 && leftHash != previousLeftHash);

            if (equipped || hotkeyPressed)
                state.Show();
        }
        else if (hotkeyPressed)
        {
            // Pressing a hotbar key for the item already equipped (or one
            // that fails to equip) is still a hotbar action.
            state.Show();
        }

        previousRightHash = rightHash;
        previousLeftHash = leftHash;

        SetAlpha(
            state.GetAlpha(
                iHUDPlugin.HotkeyBarHideDelay.Value,
                iHUDPlugin.HotkeyBarFadeDuration.Value));
    }

    private static void GetHandItemHashes(
        Player player,
        out int rightHash,
        out int leftHash)
    {
        if (RightItemField == null || LeftItemField == null)
        {
            // Fallback: only the current weapon is tracked.
            ItemDrop.ItemData? weapon =
                player.GetCurrentWeapon();

            rightHash =
                weapon != null && weapon.m_equipped
                    ? ItemHash(weapon)
                    : 0;

            leftHash = 0;
            return;
        }

        rightHash =
            ItemHash(RightItemField.GetValue(player) as ItemDrop.ItemData);

        leftHash =
            ItemHash(LeftItemField.GetValue(player) as ItemDrop.ItemData);
    }

    private static int ItemHash(ItemDrop.ItemData? item)
    {
        return item == null
            ? 0
            : item.m_shared.m_name.GetHashCode();
    }

    private static void SetAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }

    public static float GetAlpha()
    {
        float alpha = -1f;

        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                alpha = Mathf.Max(alpha, group.alpha);
        }

        return alpha < 0f ? 1f : alpha;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, alpha);
        }
    }

    public static void ShowImmediately()
    {
        state.Show();
        SetAlpha(1f);
    }

    public static void HideImmediately()
    {
        state.Hide();
        SetAlpha(0f);
    }

    public static void RestoreDefault()
    {
        state.Hide();
        SetAlpha(1f);
    }
}
// Suppresses the floating damage numbers. Both locally caused and remote
// numbers arrive through the same game method, so one hook covers all.
[HarmonyPatch]
internal static class DamageTextController
{
    // Tried in order; the first one that exists in this game version wins.
    private static readonly string[] CandidateNames =
    {
        "AddInworldText",
        "RPC_DamageText"
    };

    private static readonly MethodBase? target =
        FindTarget();

    private static MethodBase? FindTarget()
    {
        foreach (string name in CandidateNames)
        {
            try
            {
                MethodBase? method =
                    AccessTools.Method(typeof(DamageText), name);

                if (method != null)
                    return method;
            }
            catch (AmbiguousMatchException)
            {
                // Try the next candidate.
            }
        }

        return null;
    }

    // Skip this patch cleanly if the game version has no such method.
    private static bool Prepare()
    {
        if (target == null)
        {
            iHUDPlugin.Log.LogWarning(
                "DamageText method not found. " +
                "Hide Damage Numbers disabled.");
        }

        return target != null;
    }

    private static MethodBase TargetMethod()
    {
        return target!;
    }

    // Returning false skips the original method (no number is created).
    public static bool Prefix()
    {
        return !(iHUDPlugin.Enabled &&
                 iHUDPlugin.HideDamageNumbers.Value);
    }
}

// Fades the ship's power icon (the oar / half sail / full sail icon under the
// minimap) while steering. The wind indicator, the steering wheel and (by
// default) the speed arrows stay untouched. The icon is shown when boarding
// and whenever the sail setting changes, then fades out.
[HarmonyPatch]
internal static class ShipHudController
{
    // Name of the container (child of the ship HUD root) that holds the
    // oar / half sail / full sail icons. Faded by default.
    private const string PowerIconName = "PowerIcon";

    // Used if the container is not found: its individual icons.
    private static readonly string[] PowerIconFieldNames =
    {
        "m_rudder",
        "m_halfSail",
        "m_fullSail"
    };

    // The speed arrows above the steering wheel. Only faded when
    // "Speed Indicator Handling" is enabled.
    private static readonly string[] SpeedFieldNames =
    {
        "m_rudderSlow",
        "m_rudderForward",
        "m_rudderFastForward",
        "m_rudderBackward"
    };

    private static readonly FieldInfo? ShipHudRootField =
        AccessTools.Field(typeof(Hud), "m_shipHudRoot");

    private static readonly HudFadeState state =
        new HudFadeState();

    private static readonly List<CanvasGroup> groups =
        new List<CanvasGroup>();

    private static Hud? lastHud;
    private static bool builtSpeedHandling;

    private static int previousSpeed = -1;

    // Skip this patch cleanly if the game version has no such method.
    private static bool Prepare()
    {
        bool found =
            AccessTools.Method(typeof(Hud), "UpdateShipHud") != null;

        if (!found)
        {
            iHUDPlugin.Log.LogWarning(
                "Hud.UpdateShipHud not found. Ship HUD handling disabled.");
        }

        return found;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Hud), "UpdateShipHud");
    }

    public static void Postfix(Hud __instance)
    {
        if (__instance != lastHud ||
            builtSpeedHandling !=
                iHUDPlugin.ShipHudSpeedIndicatorHandling.Value)
        {
            Rebuild(__instance);
        }

        if (groups.Count == 0)
            return;

        if (!iHUDPlugin.ShipHudHandling.Value ||
            !iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        Player? player =
            Player.m_localPlayer;

        Ship? ship =
            player != null
                ? player.GetControlledShip()
                : null;

        if (ship == null)
        {
            // Not steering: forget the last setting so the next boarding
            // counts as a change.
            previousSpeed = -1;
            return;
        }

        if (iHUDPlugin.ShipHudHideEntirely.Value)
        {
            SetAlpha(0f);
            return;
        }

        int speed =
            (int)ship.GetSpeedSetting();

        if (speed != previousSpeed)
            state.Show();

        previousSpeed = speed;

        state.Tick();

        SetAlpha(
            state.GetAlpha(
                iHUDPlugin.ShipHudHideDelay.Value,
                iHUDPlugin.ShipHudFadeDuration.Value));
    }

    private static void Rebuild(Hud hud)
    {
        // Restore anything that was faded before the rebuild.
        SetAlpha(1f);

        lastHud = hud;
        builtSpeedHandling =
            iHUDPlugin.ShipHudSpeedIndicatorHandling.Value;

        groups.Clear();

        List<string> found = new List<string>();
        List<string> missing = new List<string>();
        List<string> wanted = new List<string>();

        GameObject? root =
            ShipHudRootField?.GetValue(hud) as GameObject;

        Transform? powerIcon =
            root != null
                ? root.transform.Find(PowerIconName)
                : null;

        if (powerIcon != null)
        {
            groups.Add(HudAlpha.Get(powerIcon.gameObject));
            found.Add(PowerIconName);
        }
        else
        {
            wanted.AddRange(PowerIconFieldNames);
        }

        if (builtSpeedHandling)
            wanted.AddRange(SpeedFieldNames);

        foreach (string name in wanted)
        {
            FieldInfo? field = AccessTools.Field(typeof(Hud), name);
            GameObject? obj = field != null
                ? ToGameObject(field.GetValue(hud))
                : null;

            if (obj == null)
            {
                missing.Add(name);
                continue;
            }

            groups.Add(HudAlpha.Get(obj));
            found.Add(name);
        }

        if (found.Count > 0)
        {
            iHUDPlugin.Log.LogInfo(
                "Ship HUD: fading " + string.Join(", ", found));
        }

        if (missing.Count > 0)
        {
            iHUDPlugin.Log.LogWarning(
                "Ship HUD: objects not found: " +
                string.Join(", ", missing));
        }
    }

    private static GameObject? ToGameObject(object? value)
    {
        return value as GameObject ??
               (value as Component)?.gameObject;
    }

    private static void SetAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }

    public static float GetAlpha()
    {
        float alpha = -1f;

        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                alpha = Mathf.Max(alpha, group.alpha);
        }

        return alpha < 0f ? 1f : alpha;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, alpha);
        }
    }

    public static void ShowImmediately()
    {
        state.Show();
        SetAlpha(1f);
    }

    public static void HideImmediately()
    {
        state.Hide();
        SetAlpha(0f);
    }

    public static void RestoreDefault()
    {
        state.Hide();
        previousSpeed = -1;
        SetAlpha(1f);
    }
}
[HarmonyPatch(typeof(Hud), "UpdateGuardianPower")]
internal static class GuardianPowerController
{
    private static readonly HudFadeState state =
        new HudFadeState();

    private static CanvasGroup? group;

    private static bool initialized;
    private static bool previousReady;

    private static string previousPower = "";

    private static float readyDisplayTimer =
        float.MaxValue;

    public static void Postfix(
        Hud __instance,
        Player player)
    {
        if (player != Player.m_localPlayer ||
            player == null)
            return;

        group = HudAlpha.Get(
            __instance.m_gpRoot.gameObject);

        if (!iHUDPlugin.GuardianPowerHandling.Value ||
            !iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        if (HudDisplayManager.IsInventoryOpen)
        {
            ShowImmediately();
            return;
        }

        UpdatePowerState(player);

        state.Tick();

        readyDisplayTimer += Time.deltaTime;

        // Hold the bar visible while the "ready" window is open.
        if (readyDisplayTimer <=
            iHUDPlugin.GuardianPowerReadyShowDuration.Value)
        {
            state.Show();
        }

        float alpha =
            state.GetAlpha(
                iHUDPlugin.GuardianPowerHideDelay.Value,
                iHUDPlugin.GuardianPowerFadeDuration.Value);

        group.alpha = alpha;
    }

    private static float previousCooldown;

    private static bool CrossedBelow(
        float previous,
        float current,
        float threshold)
    {
        return previous > threshold && current <= threshold;
    }

    private static void UpdatePowerState(Player player)
    {
        // Same call Hud.UpdateGuardianPower uses: effect is null when no
        // power is selected, cooldown is seconds remaining.
        player.GetGuardianPowerHUD(
            out StatusEffect powerEffect,
            out float cooldown);

        bool hasPower = powerEffect != null;
        bool ready = hasPower && cooldown <= 0f;
        float remaining = hasPower ? Mathf.Max(cooldown, 0f) : 0f;

        if (!initialized)
        {
            initialized = true;

            previousPower =
                GetSelectedPowerName(player);

            previousReady = ready;
            previousCooldown = remaining;

            return;
        }

        string currentPower =
            GetSelectedPowerName(player);

        if (currentPower != previousPower)
        {
            Show();
        }

        if (!previousReady && ready)
        {
            ShowReady();
        }

        if (hasPower && remaining > 0f)
        {
            // Pop up when 10 and 5 minutes are left, then hide normally.
            if (CrossedBelow(previousCooldown, remaining, 600f) ||
                CrossedBelow(previousCooldown, remaining, 300f))
            {
                Show();
            }

            // Under a minute left: stay visible until the power is ready.
            if (remaining < 60f)
            {
                Show();
            }
        }

        previousPower = currentPower;
        previousReady = ready;
        previousCooldown = remaining;
    }

    public static void NotifyActivationAttempt()
    {
        Show();
    }

    private static void Show()
    {
        readyDisplayTimer = float.MaxValue;
        state.Show();
    }

    private static void ShowReady()
    {
        state.Show();
        readyDisplayTimer = 0f;
    }

    private static string GetSelectedPowerName(Player player)
    {
        return player.GetGuardianPowerName() ?? "";
    }

    private static bool IsGuardianPowerReady(Player player)
    {
        // Same call Hud.UpdateGuardianPower uses. Returns void in this game
        // version: effect is null when no power is selected, cooldown is
        // seconds remaining.
        player.GetGuardianPowerHUD(
            out StatusEffect powerEffect,
            out float cooldown);

        return powerEffect != null &&
               cooldown <= 0f;
    }

    public static float GetAlpha()
    {
        return group != null ? group.alpha : 1f;
    }

    public static void ApplyCombatAlpha(float alpha)
    {
        if (group != null)
            group.alpha = Mathf.Max(group.alpha, alpha);
    }

    public static void ShowImmediately()
    {
        state.Show();

        if (group != null)
            group.alpha = 1f;
    }

    public static void HideImmediately()
    {
        state.Hide();

        if (group != null)
            group.alpha = 0f;
    }

    public static void RestoreDefault()
    {
        state.Hide();
        initialized = false;

        if (group != null)
            group.alpha = 1f;
    }

    public static void Tick()
    {
    }
}
[HarmonyPatch(typeof(Player), "StartGuardianPower")]
internal static class Player_StartGuardianPower_Patch
{
    public static void Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
            return;

        GuardianPowerController.NotifyActivationAttempt();
    }
}
[HarmonyPatch(typeof(Minimap), "Update")]
internal static class MinimapController
{
    private static Minimap? minimap;

    public static void Postfix(Minimap __instance)
    {
        minimap = __instance;

        if (!iHUDPlugin.MinimapHandling.Value)
        {
            RestoreDefault();
            return;
        }

        if (!iHUDPlugin.Enabled)
        {
            RestoreDefault();
            return;
        }

        ApplySavedState();
    }

    public static void Toggle()
    {
        iHUDPlugin.MinimapVisible.Value =
            !iHUDPlugin.MinimapVisible.Value;

        iHUDPlugin.Instance.Config.Save();

        ApplySavedState();
    }

    public static void ApplySavedState()
    {
        if (minimap == null)
            return;

        if (minimap.m_smallRoot == null)
            return;

        // The game hides the small map while the large map is open.
        if (Minimap.IsOpen())
            return;

        minimap.m_smallRoot.SetActive(
            iHUDPlugin.MinimapVisible.Value);
    }

    public static void RestoreDefault()
    {
        if (minimap == null)
            return;

        if (minimap.m_smallRoot == null)
            return;

        if (Minimap.IsOpen())
            return;

        minimap.m_smallRoot.SetActive(true);
    }

    public static void Tick()
    {
    }
}

// Finds HUD elements that other mods add to the game's HUD and lets each one
// be attached to iHUD modules.
//
// Vanilla objects are recorded in a Hud.Awake prefix, before any mod can add
// to the HUD. Anything that later appears under the HUD (at any depth) and
// is not in that record was added by a mod. Each such element gets its own
// entry in the '3. Mod Linking' config section. The entry holds a
// comma-separated list of modules (Health, Food, StatusEffects, Crosshair,
// Hotbar, GuardianPower, ShipHud, Combat). The element then follows those
// modules' alpha. With 'None' (the default) it is hidden while iHUD is
// enabled, so toggling iHUD off shows it again.
[HarmonyPatch(typeof(Hud), "Awake")]
internal static class ModUiLinker
{
    private const string Section = "3. Mod Linking";

    private static readonly string[] ModuleNames =
    {
        "Health",
        "Food",
        "StatusEffects",
        "Crosshair",
        "Hotbar",
        "GuardianPower",
        "ShipHud",
        "Combat"
    };

    private sealed class Linked
    {
        public Transform Transform = null!;
        public CanvasGroup Group = null!;
        public ConfigEntry<string> Entry = null!;
        public string CachedValue = "\0";
        public List<string> Modules = new List<string>();
        public bool Touched;
    }

    private static readonly HashSet<int> vanillaIds =
        new HashSet<int>();

    private static readonly Dictionary<int, Linked> linked =
        new Dictionary<int, Linked>();

    private static readonly HashSet<int> ignored =
        new HashSet<int>();

    private static readonly HashSet<string> keysInUse =
        new HashSet<string>();

    private static readonly HashSet<int> debugLogged =
        new HashSet<int>();

    private static Hud? hud;
    private static float nextScanTime;

    public static void Prefix(Hud __instance)
    {
        hud = __instance;

        vanillaIds.Clear();
        linked.Clear();
        ignored.Clear();
        keysInUse.Clear();
        debugLogged.Clear();
        decisions.Clear();
        nextScanTime = 0f;

        foreach (Transform root in GetRoots(__instance))
        {
            foreach (Transform t in
                     root.GetComponentsInChildren<Transform>(true))
            {
                vanillaIds.Add(t.gameObject.GetInstanceID());
            }
        }
    }

    private static List<Transform> GetRoots(Hud instance)
    {
        List<Transform> roots = new List<Transform> { instance.transform };

        if (instance.m_rootObject != null &&
            instance.m_rootObject.transform != instance.transform)
        {
            roots.Add(instance.m_rootObject.transform);
        }

        return roots;
    }

    // Called every frame from iHUDPlugin.LateUpdate, after every module has
    // applied its alpha for the frame.
    public static void LateApply()
    {
        if (hud == null)
        {
            linked.Clear();
            return;
        }

        bool active =
            iHUDPlugin.LinkModUIs.Value &&
            iHUDPlugin.Enabled;

        if (active && Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + 0.25f;
            Scan(hud);
        }

        bool inventoryOpen = HudDisplayManager.IsInventoryOpen;
        List<int>? dead = null;

        foreach (KeyValuePair<int, Linked> pair in linked)
        {
            Linked item = pair.Value;

            if (item.Transform == null || item.Group == null)
            {
                dead ??= new List<int>();
                dead.Add(pair.Key);
                continue;
            }

            if (!active || inventoryOpen)
            {
                if (item.Touched)
                {
                    item.Group.alpha = 1f;
                    item.Touched = false;
                }

                continue;
            }

            ParseModules(item);

            float alpha = 0f;

            foreach (string module in item.Modules)
                alpha = Mathf.Max(alpha, GetModuleAlpha(module));

            item.Group.alpha = alpha;
            item.Touched = true;
        }

        if (dead != null)
        {
            foreach (int id in dead)
                linked.Remove(id);
        }
    }

    private static bool scanErrorLogged;

    private static readonly Dictionary<int, string> decisions =
        new Dictionary<int, string>();

    private static void Scan(Hud instance)
    {
        try
        {
            foreach (Transform root in GetRoots(instance))
                Walk(root);

            if (iHUDPlugin.LogAllNewHudObjects.Value)
            {
                foreach (Transform root in GetRoots(instance))
                    DebugWalk(root);
            }
        }
        catch (Exception e)
        {
            if (!scanErrorLogged)
            {
                scanErrorLogged = true;
                iHUDPlugin.Log.LogError($"Mod UI scan failed: {e}");
            }
        }
    }

    // With 'Log All New HUD Objects' on, logs why an object was or was not
    // picked up (once per object and reason).
    private static void Note(int id, Transform element, string reason)
    {
        if (!iHUDPlugin.LogAllNewHudObjects.Value)
            return;

        if (decisions.TryGetValue(id, out string? previous) &&
            previous == reason)
        {
            return;
        }

        decisions[id] = reason;

        iHUDPlugin.Log.LogInfo(
            $"Mod UI candidate '{element.name}': {reason}");
    }

    // Finds the top-most new objects anywhere under the HUD. Vanilla creates
    // its own dynamic UI by instantiating prefabs (hotbar slots, status
    // effect icons, map pins, build menu icons), and those keep the
    // "(Clone)" name, so objects with that name are ignored.
    private static void Walk(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            int id = child.gameObject.GetInstanceID();

            if (vanillaIds.Contains(id))
            {
                Walk(child);
                continue;
            }

            if (child.name.EndsWith("(Clone)"))
            {
                Note(id, child, "skipped, vanilla-style clone");
                continue;
            }

            Track(child, id);
        }
    }

    private static void DebugWalk(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            int id = child.gameObject.GetInstanceID();

            if (!vanillaIds.Contains(id) && debugLogged.Add(id))
            {
                iHUDPlugin.Log.LogInfo(
                    $"New HUD object: {GetPath(child)}");
            }

            DebugWalk(child);
        }
    }

    private static void Track(Transform element, int id)
    {
        if (linked.ContainsKey(id) || ignored.Contains(id))
            return;

        // Extra hotbars (a quiver bar, for example) are always accepted,
        // whether by having Valheim's own HotkeyBar component, or, since a
        // mod may use its own script instead, by having "hotkeybar" in its
        // name.
        bool looksLikeHotbar =
            element.GetComponentInChildren<HotkeyBar>(true) != null ||
            element.name.IndexOf(
                "hotkeybar", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!looksLikeHotbar)
        {
            // Anything else must show something, and windows and popups
            // with buttons or inputs are left alone.
            if (IsInteractive(element))
            {
                Note(id, element, "ignored, has buttons/inputs");
                ignored.Add(id);
                return;
            }

            // Nothing visible (yet). Checked again on the next scan.
            if (element.GetComponentInChildren<UnityEngine.UI.Graphic>(
                    true) == null)
            {
                Note(id, element, "waiting, nothing visible in it yet");
                return;
            }
        }

        Note(id, element, "accepted");

        string modName = GuessModName(element);

        string baseName = element.name.Replace("(Clone)", "").Trim();

        if (baseName.Length == 0)
            baseName = "Unnamed";

        string key = MakeKey(baseName);

        for (int n = 2; keysInUse.Contains(key); n++)
            key = MakeKey(baseName) + " " + n;

        keysInUse.Add(key);

        ConfigEntry<string> entry =
            iHUDPlugin.Instance.Config.Bind(
                Section,
                key,
                "None",
                "Mod: " + modName + ". Modules this HUD element " +
                "follows, comma separated: " +
                string.Join(", ", ModuleNames) + ". None = always " +
                "hidden while iHUD is enabled. Path: " + GetPath(element));

        linked[id] = new Linked
        {
            Transform = element,
            Group = HudAlpha.Get(element.gameObject),
            Entry = entry
        };

        iHUDPlugin.Instance.Config.Save();

        iHUDPlugin.Log.LogInfo(
            $"Found mod HUD element '{key}' (path: {GetPath(element)})");
    }

    private static void ParseModules(Linked item)
    {
        string value = item.Entry.Value;

        if (value == item.CachedValue)
            return;

        item.CachedValue = value;
        item.Modules.Clear();

        foreach (string part in value.Split(','))
        {
            string name = part.Trim();

            foreach (string known in ModuleNames)
            {
                if (string.Equals(
                        name,
                        known,
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.Modules.Add(known);
                }
            }
        }
    }

    private static float GetModuleAlpha(string module)
    {
        switch (module)
        {
            case "Health": return HealthController.GetAlpha();
            case "Food": return FoodController.GetAlpha();
            case "StatusEffects": return StatusEffectController.GetAlpha();
            case "Crosshair": return CrosshairController.GetAlpha();
            case "Hotbar": return HotkeyBarController.GetAlpha();
            case "GuardianPower": return GuardianPowerController.GetAlpha();
            case "ShipHud": return ShipHudController.GetAlpha();
            case "Combat": return CombatController.GetAlpha();
            default: return 0f;
        }
    }

    // Looks for a script belonging to another mod's assembly, on the
    // element itself, its children, or its ancestors up to the HUD. Most
    // mod-added HUD elements are driven by one of the mod's own
    // MonoBehaviours somewhere in that range.
    private static string GuessModName(Transform element)
    {
        Component? found =
            SearchAssembly(element) ??
            SearchAncestors(element);

        return found != null
            ? found.GetType().Assembly.GetName().Name
            : "unknown";
    }

    private static Component? SearchAssembly(Transform element)
    {
        foreach (Component c in
                 element.GetComponentsInChildren<Component>(true))
        {
            if (c != null && IsModAssembly(c.GetType().Assembly))
                return c;
        }

        return null;
    }

    private static Component? SearchAncestors(Transform element)
    {
        for (Transform? t = element.parent; t != null; t = t.parent)
        {
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c != null && IsModAssembly(c.GetType().Assembly))
                    return c;
            }
        }

        return null;
    }

    private static bool IsModAssembly(System.Reflection.Assembly assembly)
    {
        string name = assembly.GetName().Name;

        return name != "Assembly-CSharp" &&
               name != "iHUD" &&
               !name.StartsWith("UnityEngine") &&
               !name.StartsWith("Unity.") &&
               name != "0Harmony" &&
               name != "mscorlib" &&
               !name.StartsWith("System");
    }

    private static string MakeKey(string name)
    {
        char[] invalid = { '\n', '\t', '"', '\'', '[', ']', '=' };

        foreach (char c in invalid)
            name = name.Replace(c, '_');

        return name;
    }

    private static bool IsInteractive(Transform element)
    {
        return element.GetComponentInChildren<UnityEngine.UI.Selectable>(true)
                   != null ||
               element.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true)
                   != null;
    }

    private static string GetPath(Transform element)
    {
        string path = element.name;

        for (Transform? parent = element.parent;
             parent != null;
             parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
