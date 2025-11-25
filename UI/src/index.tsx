import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { SelectedInfoPanelTogglesComponent } from "./SelectedInfoPanelTogglesComponent";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    // 1. Resolver must be first
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // 2. Button
    moduleRegistry.append("GameTopLeft", TrafficButton);

    // 3. Info Panel Extension
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        SelectedInfoPanelTogglesComponent
    );
    
    console.log("[TrafficSpy] UI Registered");
}

export default register;