import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    moduleRegistry.append("GameTopLeft", TrafficButton);

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        (sections: any) => {
            // FIX: Use the full C# Namespace + Class Name as the key
            // "TrafficSpy.Systems" (Namespace) + "TrafficUISystem" (Class)
            sections["TrafficSpy.Systems.TrafficUISystem"] = ActivitySection;
            
            return sections;
        }
    );
    
    console.log("Traffic Explorer UI Registered");
}

export default register;