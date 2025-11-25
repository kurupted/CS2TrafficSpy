import { useValue } from "cs2/api";
import { trigger } from "cs2/api";
import { Button } from "cs2/ui";
import { toolActive } from "./bindings";

// Simple functional component - no imports that could fail
const TrafficButton = () => {
    console.log("[TrafficSpy] TrafficButton rendering...");
    
    try {
        const active = useValue(toolActive);
        
        console.log("[TrafficSpy] Button state:", active);
        
        return (
            <Button 
                src="coui://uil/Standard/GenericVehicles.svg"
                selected={active}
                variant="floating"
                onSelect={() => {
                    console.log("[TrafficSpy] Button clicked, toggling from", active);
                    trigger("TrafficSpy", "setToolActive", !active);
                }}
            />
        );
    } catch (error) {
        console.error("[TrafficSpy] Button render error:", error);
        return null;
    }
};

export default TrafficButton;