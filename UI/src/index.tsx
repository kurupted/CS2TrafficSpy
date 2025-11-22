import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    // 1. Add the button to the top-left game HUD
    moduleRegistry.append("GameTopLeft", TrafficButton);

    // 2. Add your section to the Selected Info Panel list
    // FIX: Use 'append' instead of 'extend' for lists/arrays
    moduleRegistry.append(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        ActivitySection
    );
    
    console.log("Traffic Spy UI Registered");
}

export default register;