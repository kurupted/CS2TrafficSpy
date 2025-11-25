import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { SelectedInfoPanelTogglesComponent } from "./SelectedInfoPanelTogglesComponent";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {

    console.log("[TrafficSpy] begin register");
    
    // 1. Initialize Resolver
    VanillaComponentResolver.setRegistry(moduleRegistry);

    console.log("[TrafficSpy] did vanilla");

    // 2. Add Button
    moduleRegistry.append("GameTopLeft", TrafficButton);

    console.log("[TrafficSpy] added button");

    // 3. Register Info Panel Section (The Native Way)
    // We extend the list of components, adding ours to it
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        SelectedInfoPanelTogglesComponent
    );
    
    console.log("[TrafficSpy] UI Registered (Native Method)");
}

export default register;