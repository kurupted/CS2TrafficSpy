import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    moduleRegistry.append("GameTopLeft", TrafficButton);

    // Register the Info Section using the Dictionary method
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        (sections: any) => {
            // "TrafficSpy" must match the 'group' string in TrafficUISystem.cs
            sections["TrafficSpy"] = ActivitySection;
            return sections;
        }
    );
    
    console.log("Traffic Spy UI Registered");
}

export default register;