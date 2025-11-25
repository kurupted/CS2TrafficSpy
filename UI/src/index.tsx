import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { SelectedInfoPanelTogglesComponent } from "./SelectedInfoPanelTogglesComponent";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    try {
        console.log("[TrafficSpy] ===== Starting registration =====");
        
        // 1. Initialize VanillaComponentResolver FIRST
        console.log("[TrafficSpy] Step 1: Setting up VanillaComponentResolver...");
        VanillaComponentResolver.setRegistry(moduleRegistry);
        console.log("[TrafficSpy] ? VanillaComponentResolver ready");

        // 2. Add button to top-left toolbar
        console.log("[TrafficSpy] Step 2: Registering button...");
        moduleRegistry.append("GameTopLeft", TrafficButton);
        console.log("[TrafficSpy] ? Button registered");

        // 3. Extend the Info Panel sections (with extra safety)
        console.log("[TrafficSpy] Step 3: Extending info panel...");
        
        const infoPanelPath = "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
        const infoPanelExport = "selectedInfoSectionComponents";
        
        try {
            // First check if the module exists
            const testModule = moduleRegistry.registry.get(infoPanelPath);
            if (testModule) {
                console.log("[TrafficSpy] Info panel module found, exports:", Object.keys(testModule));
                
                if (testModule[infoPanelExport]) {
                    console.log("[TrafficSpy] Found export:", infoPanelExport);
                    moduleRegistry.extend(
                        infoPanelPath,
                        infoPanelExport,
                        SelectedInfoPanelTogglesComponent
                    );
                    console.log("[TrafficSpy] ? Info panel extended successfully");
                } else {
                    console.error("[TrafficSpy] Export not found:", infoPanelExport);
                    console.log("[TrafficSpy] Available exports:", Object.keys(testModule));
                }
            } else {
                console.error("[TrafficSpy] Module not found:", infoPanelPath);
                console.log("[TrafficSpy] Available modules:", Array.from(moduleRegistry.registry.keys()));
            }
        } catch (extendError) {
            console.error("[TrafficSpy] ? Info panel extension failed:", extendError);
            console.log("[TrafficSpy] Button will still work, but info panel won't show");
        }
        
        console.log("[TrafficSpy] ===== ? Registration Complete =====");
    } catch (error) {
        console.error("[TrafficSpy] ===== ? FATAL: Registration failed =====", error);
    }
}

export default register;