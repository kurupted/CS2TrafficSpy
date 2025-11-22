import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";


const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {   // Added ': ModuleRegistry' type to the argument
    
    moduleRegistry.append("GameTopLeft", TrafficButton);

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        ActivitySection
    );
    
    console.log("Traffic Explorer UI Registered");
}

export default register;