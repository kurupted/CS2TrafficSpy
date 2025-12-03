using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace TrafficSpy.ModSettings
{
    [FileLocation(nameof(TrafficSpy))]
    [SettingsUIGroupOrder(kKeybindingGroup)]
    [SettingsUIShowGroupName(kKeybindingGroup)]
    // Define the Action. "usages" determines where the key works (e.g. In Game).
    [SettingsUIKeyboardAction(Mod.kToggleActionName, ActionType.Button, usages: new string[] { "TrafficSpy_Usage" }, interactions: new string[] { "UIButton" }, modifierOptions: ModifierOptions.Allow)]
    public class ModSettings : ModSetting
    {
        public const string kSection = "Main";
        public const string kKeybindingGroup = "KeyBinding";

        public ModSettings(IMod mod) : base(mod)
        {

        }

        // Define the default binding (Ctrl + I)
        [SettingsUIKeyboardBinding(BindingKeyboard.I, Mod.kToggleActionName, ctrl: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding ToggleToolBinding { get; set; }

        public override void SetDefaults()
        {
            // No manual code is needed here for KeyBindings. 
            // The [SettingsUIKeyboardBinding] attribute above automatically 
            // defines the default value for the game's input system.
        }
    }
}