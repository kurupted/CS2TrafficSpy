using Colossal;
using Game.Input;
using Game.Settings;
using System.Collections.Generic;

namespace TrafficSpy.ModSettings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;
        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), ModAssemblyInfo.Title },
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kKeybindingGroup), "Controls" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kVisualsGroup), "Visuals" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.MaxVehicleTraffic)), "Vehicle Traffic Threshold" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.MaxVehicleTraffic)), "The number of vehicles passing through a segment required to reach maximum 'Red' heat intensity." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.MaxPedestrianTraffic)), "Pedestrian Traffic Threshold" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.MaxPedestrianTraffic)), "The number of pedestrians required to reach maximum intensity." },

                // Matches the property name "ToggleToolBinding" in ModSettings.cs
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ToggleToolBinding)), "Activate Traffic Spy" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ToggleToolBinding)), $"Press this key to activate Traffic Spy. Once active, click a road or path segment." },

                // Matches the Action Name in Mod.cs
                { m_Setting.GetBindingKeyLocaleID(Mod.kToggleActionName), "Activation Key" },

                { m_Setting.GetBindingMapLocaleID(), ModAssemblyInfo.Title },
            };
        }

        public void Unload()
        {

        }
    }
}