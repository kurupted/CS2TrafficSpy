import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import TrafficButton from "./TrafficButton";
import { ActivitySection } from "./ActivitySection";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    
    moduleRegistry.append("GameTopLeft", TrafficButton);

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 
        'selectedInfoSectionComponents', 
        (sections: any) => {
            // Instead of relying on a specific C# system name, we inject our component
            // by wrapping a standard component that ALWAYS appears, like "TitleSection".
            // This guarantees visibility.
            
            const TitleSection = sections["Game.UI.InGame.TitleSection"];
            
            if (TitleSection) {
                const Wrapper = (props: any) => (
                    <>
                        <TitleSection {...props} />
                        <ActivitySection />
                    </>
                );
                sections["Game.UI.InGame.TitleSection"] = Wrapper;
            }
            
            return sections;
        }
    );
    
    console.log("Traffic Explorer UI Registered");
}

export default register;