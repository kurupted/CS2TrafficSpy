import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { SelectedInfoPanelTogglesComponent } from "./SelectedInfoPanelTogglesComponent";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    // 1. Setup Resolver
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // 2. Add Toolbar Button
    moduleRegistry.append("GameTopLeft", TrafficButton);

    // 3. Extend Info Panel
    // This path is standard for recent CS2 versions
    const infoPanelPath = "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
    const infoPanelExport = "selectedInfoSectionComponents";
    
    try {
        moduleRegistry.extend(
            infoPanelPath,
            infoPanelExport,
            SelectedInfoPanelTogglesComponent
        );
        console.log("[TrafficSpy] UI Registered Successfully");
    } catch (e) {
        console.error("[TrafficSpy] Failed to register UI extensions", e);
    }
}

export default register;