import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    moduleRegistry.append("GameTopLeft", TrafficButton);

    // FIX: Use a callback function that renders the original component AND your new one
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        (Component: any) => (props: any) => {
            return (
                <>
                    <Component {...props} />
                    <ActivitySection />
                </>
            );
        }
    );
    
    console.log("Traffic Explorer UI Registered");
}

export default register;