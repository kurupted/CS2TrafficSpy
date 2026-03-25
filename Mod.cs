using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using TrafficSpy.ModSettings;
using TrafficSpy.Systems;

namespace TrafficSpy
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TrafficSpy)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private ModSettings.ModSettings m_Setting;

        public static ProxyAction m_ToggleAction;
        public const string kToggleActionName = "TrafficSpy_Toggle";

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new ModSettings.ModSettings(this);
            ModSettings.ModSettings.Instance = m_Setting;
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            m_Setting.RegisterKeyBindings();

            // Load the specific action defined in ModSettings
            m_ToggleAction = m_Setting.GetAction(kToggleActionName);
            m_ToggleAction.shouldBeEnabled = true;

            AssetDatabase.global.LoadSettings(nameof(TrafficSpy), m_Setting, new ModSettings.ModSettings(this));

            updateSystem.UpdateAt<TrafficSpyToolSystem>(SystemUpdatePhase.ToolUpdate);
            
            updateSystem.UpdateAt<TrafficSpy.Systems.TrafficUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<TrafficSpy.Systems.TrafficHighlightSystem>(SystemUpdatePhase.ToolUpdate);
            
            updateSystem.UpdateAt<SimpleOverlayRendererSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<TrafficRouteSystem>(SystemUpdatePhase.Rendering);
            
            // Register the Color System so the Harmony patch intercepts the colors
            updateSystem.UpdateAt<TrafficSpy.Systems.TrafficColorSystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}