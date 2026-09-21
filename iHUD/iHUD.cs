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

[BepInPlugin("iHUD", "iHUD", "1.1.2")]
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
    internal static ConfigEntry<bool> HideOtherModHud = null!;
    internal static ConfigEntry<string> OtherModHudNeverHide = null!;
    internal static ConfigEntry<bool> ShipHudHandling = null!;
    internal static ConfigEntry<float> ShipHudHideDelay = null!;
    internal static ConfigEntry<float> ShipHudFadeDuration = null!;
    internal static ConfigEntry<bool> ShipHudHideEntirely = null!;
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

    internal static ConfigEntry<bool> FoodPopupIntervalsEnabled = null!;
    internal static ConfigEntry<string> FoodPopupIntervals = null!;

    // ---------------------------------------------------------------------
    // Status effects
    // ---------------------------------------------------------------------

    internal static ConfigEntry<float> StatusEffectHideDelay = null!;
    internal static ConfigEntry<float> StatusEffectFadeDuration = null!;
    internal static ConfigEntry<float> StatusEffectCriticalTime = null!;
    internal static ConfigEntry<string> StatusEffectPopupIntervals = null!;

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
    }

    private void LateUpdate()
    {
        HotkeyBarController.LateApply();
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
            "General",
            "Toggle HUD Hotkey",
            new KeyboardShortcut(KeyCode.H),
            "Toggle all iHUD functionality on/off.");

        MinimapToggleHotkey = Config.Bind(
            "General",
            "Minimap Toggle Hotkey",
            new KeyboardShortcut(KeyCode.B),
            "Toggle minimap visibility.");

        GamepadToggleModifier = Config.Bind(
            "General",
            "Gamepad Toggle Modifier",
            "BumperL",
            "Gamepad button that must be held for the HUD toggle combo " +
            "(BumperL = LB). Set to None to use a single button. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadToggleButton = Config.Bind(
            "General",
            "Gamepad Toggle Button",
            "DPadLeft",
            "Gamepad button that toggles iHUD. " +
            "Set to None to disable the gamepad toggle. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadMinimapModifier = Config.Bind(
            "General",
            "Gamepad Minimap Modifier",
            "BumperL",
            "Gamepad button that must be held for the minimap toggle combo " +
            "(BumperL = LB). Set to None to use a single button. " +
            "Valid values: " +
            GamepadHotkey.ValidNames);

        GamepadMinimapButton = Config.Bind(
            "General",
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
            "Modules",
            "Health Handling",
            true,
            "Hide the health bar when it is not needed.");

        FoodHandling = Config.Bind(
            "Modules",
            "Food Handling",
            true,
            "Hide the food bar when it is not needed.");

        StatusEffectHandling = Config.Bind(
            "Modules",
            "Status Effect Handling",
            true,
            "Hide and selectively show status effect icons.");

        CrosshairHandling = Config.Bind(
            "Modules",
            "Crosshair Handling",
            true,
            "Hide the crosshair except when it is useful.");

        HotkeyBarHandling = Config.Bind(
            "Modules",
            "Hotkey Bar Handling",
            true,
            "Hide the hotkey bar according to the configured mode.");

        GuardianPowerHandling = Config.Bind(
            "Modules",
            "Guardian Power Handling",
            true,
            "Hide the guardian power bar when it is not needed.");

        MinimapHandling = Config.Bind(
            "Modules",
            "Minimap Handling",
            true,
            "Allow iHUD to control minimap visibility.");

        ShipHudHandling = Config.Bind(
            "Modules",
            "Ship HUD Handling",
            true,
            "Fade the ship HUD (sail/speed setting) when it is not needed.");

        ShowAllOnInventory = Config.Bind(
            "General",
            "Show All On Inventory",
            true,
            "Force all HUD elements to be visible while the inventory is " +
            "open. When off, elements keep following their normal " +
            "hide/fade rules with the inventory open.");

        ToggleMessage = Config.Bind(
            "General",
            "Toggle Message",
            true,
            "Show a short on-screen message when iHUD is toggled on/off.");

        HideDamageNumbers = Config.Bind(
            "General",
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

        FoodPopupIntervalsEnabled = Config.Bind(
            "Food",
            "Popup Intervals Enabled",
            false,
            "If enabled, the food bar pops up when food crosses configured intervals.");

        FoodPopupIntervals = Config.Bind(
            "Food",
            "Popup Intervals",
            "1200,900,600,300",
            "Comma-separated seconds remaining at which the food bar pops up.");

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

        StatusEffectPopupIntervals = Config.Bind(
            "Status Effects",
            "Popup Intervals",
            "1200,900,600,300",
            "Comma-separated seconds remaining at which finite status effects temporarily reappear.");

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
        // Other mods
        // -----------------------------------------------------------------

        HideOtherModHud = Config.Bind(
            "Other Mods",
            "Hide Other Mod HUD",
            false,
            "Try to hide HUD elements that other mods add to the game's " +
            "HUD while iHUD is enabled. Windows and popups that contain " +
            "buttons or inputs are left alone. Toggle iHUD off to see " +
            "everything again. Hidden elements are listed in the BepInEx log.");

        OtherModHudNeverHide = Config.Bind(
            "Other Mods",
            "Never Hide",
            "",
            "Comma-separated name fragments. HUD elements whose name " +
            "contains one of these are never hidden (element names are " +
            "listed in the BepInEx log).");

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

internal enum HotkeyBarMode
{
    AlwaysHidden,
    OnEquipChange
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

        if (iHUDPlugin.FoodPopupIntervalsEnabled.Value)
        {
            CheckPopupIntervals(foods);
        }
    }

    private static void CheckPopupIntervals(List<Player.Food> foods)
    {
        float[] thresholds =
            ParseSeconds(iHUDPlugin.FoodPopupIntervals.Value);

        foreach (Player.Food food in foods)
        {
            float previous = food.m_time + Time.deltaTime;
            float current = food.m_time;

            foreach (float threshold in thresholds)
            {
                if (previous > threshold &&
                    current <= threshold)
                {
                    state.Show();
                }
            }
        }
    }

    public static void NotifyFoodEaten()
    {
        recentlyExpiredTimer = float.MaxValue;
        state.Show();
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

    private static float[] ParseSeconds(string input)
    {
        return input
            .Split(',')
            .Select(s =>
            {
                float value;

                return float.TryParse(
                    s.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                    ? value
                    : -1f;
            })
            .Where(v => v >= 0f)
            .OrderByDescending(v => v)
            .ToArray();
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

    public int NextPopupIndex;

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

        float[] popupIntervals =
            ParseSeconds(
                iHUDPlugin.StatusEffectPopupIntervals.Value);

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
                InitializeState(
                    state,
                    effect,
                    remaining,
                    popupIntervals);

                // Newly acquired effects are shown.
                state.Fade.Show();
            }
            else
            {
                UpdateEffectState(
                    state,
                    effect,
                    remaining,
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
        StatusEffectDisplayState state,
        StatusEffect effect,
        float remaining,
        float[] intervals)
    {
        state.Name = effect.name;
        state.PreviousRemaining = remaining;
        state.Initialized = true;

        state.Critical =
            effect.m_ttl > Epsilon &&
            remaining <= iHUDPlugin.StatusEffectCriticalTime.Value;

        state.NextPopupIndex =
            FindNextPopupIndex(
                remaining,
                intervals);
    }

    private static void UpdateEffectState(
        StatusEffectDisplayState state,
        StatusEffect effect,
        float remaining,
        float[] intervals,
        float criticalTime)
    {
        // -------------------------------------------------------------
        // Infinite / condition-based effects
        // -------------------------------------------------------------

        if (effect.m_ttl <= Epsilon)
        {
            // They simply use the normal Show() timer.
            return;
        }

        // -------------------------------------------------------------
        // Critical
        // -------------------------------------------------------------

        if (remaining <= criticalTime)
        {
            if (!state.Critical)
            {
                state.Critical = true;
                state.Fade.Show();
            }

            return;
        }

        // -------------------------------------------------------------
        // Finite effect, not critical
        // -------------------------------------------------------------

        if (state.Critical)
        {
            // This shouldn't normally happen, but protects against
            // effects being refreshed with more time.
            state.Critical = false;
        }

        while (state.NextPopupIndex < intervals.Length)
        {
            float threshold =
                intervals[state.NextPopupIndex];

            if (state.PreviousRemaining > threshold &&
                remaining <= threshold)
            {
                state.Fade.Show();

                state.NextPopupIndex++;
                break;
            }

            state.NextPopupIndex++;

            if (remaining > threshold)
                break;
        }
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

    private static int FindNextPopupIndex(
        float remaining,
        float[] intervals)
    {
        for (int i = 0; i < intervals.Length; i++)
        {
            if (remaining > intervals[i])
                return i;
        }

        return intervals.Length;
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

    private static float[] ParseSeconds(string input)
    {
        return input
            .Split(',')
            .Select(s =>
            {
                float value;

                return float.TryParse(
                    s.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                    ? value
                    : -1f;
            })
            .Where(v => v >= 0f)
            .OrderByDescending(v => v)
            .ToArray();
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

    // HotkeyBar.UpdateIcons is not guaranteed to run every frame (other
    // mods can make it event-driven), so it is only used to discover the
    // bar. The fade itself is driven by Tick (Update) and LateApply
    // (LateUpdate), which run every frame.
    public static void Postfix(HotkeyBar __instance)
    {
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
            // 0 = empty hand. Only equipping something (a hand that now
            // holds a different item) shows the bar; putting an item away
            // does not.
            bool equipped =
                (rightHash != 0 && rightHash != previousRightHash) ||
                (leftHash != 0 && leftHash != previousLeftHash);

            if (equipped)
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

    private static void UpdatePowerState(Player player)
    {
        if (!initialized)
        {
            initialized = true;

            previousPower =
                GetSelectedPowerName(player);

            previousReady =
                IsGuardianPowerReady(player);

            return;
        }

        string currentPower =
            GetSelectedPowerName(player);

        bool ready =
            IsGuardianPowerReady(player);

        if (currentPower != previousPower)
        {
            Show();
        }

        if (!previousReady && ready)
        {
            ShowReady();
        }

        previousPower = currentPower;
        previousReady = ready;
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
