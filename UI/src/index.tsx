import { ModRegistrar } from "cs2/modding";
import { TransitPanel } from "./TransitPanel";
import { TrafficButton } from "./TrafficButton";

export default ((moduleRegistry) => {

    (window as any).moduleRegistry = moduleRegistry;
    
    moduleRegistry.append('GameTopLeft', TrafficButton);

    /*moduleRegistry.extend(
        "game-ui/game/components/infoviews/infoview-panel/infoview-panel.tsx",
        "InfoviewPanel",
        (VanillaComponent: any) => {
            return (props: any) => (
                <>
                    <VanillaComponent {...props} />
                    <TransitPanel />
                </>
            );
        }
    );*/

    // 2. Safely append to the main Game screen instead of extending a hidden panel
    moduleRegistry.append('Game', TransitPanel);
    
}) as ModRegistrar;