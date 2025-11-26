import { getModule } from "cs2/modding";
import { Theme } from "cs2/bindings";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { activityData } from "./bindings"; // This is your data binding!
import { useValue } from "cs2/api"; //
import { useMemo } from "react";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

// Get game components
const InfoSectionTheme: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
    "classes"
);

const InfoRowTheme: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
);

const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

/*const SectionTitle: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/section-title/section-title.tsx",
    "SectionTitle"
);*/

export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {

    // Helper to render a row
    const renderRow = (label: string, count: number) => {
        if (!count || count <= 0) return null;
        return (
            <InfoRow 
                left={label} 
                right={count.toString()} 
                uppercase={true} 
                disableFocus={true}
                subRow={false}
                className={InfoRowTheme?.infoRow} 
            />
        );
    };
    

    // Register component - key MUST match C# group property
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {

        const jsonString = useValue(activityData);
        // Parse the JSON only when the string changes
        const data = useMemo(() => {
            try {
                if (!jsonString || jsonString === "{}") {
                    return { workers: 0, students: 0, /* ... zeros ... */ };
                }
                return JSON.parse(jsonString);
            } catch (e) {
                console.error("Parse error", e);
                return { workers: 0, students: 0, /* ... zeros ... */ };
            }
        }, [jsonString]);
        

        // Calculate total
        /*const total = (data.workers || 0) + (data.students || 0) + (data.shoppers || 0) + 
                      (data.goingHome || 0) + (data.healthcare || 0) + 
                      (data.cargo || 0) + (data.services || 0) + 
                      (data.publicTransport || 0) + (data.other || 0);
                      */

        // Return the UI            {SectionTitle && <SectionTitle title={`TRAFFIC SPY (${total})`} />}
        return (
            <InfoSection 
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} 
                disableFocus={true} 
                className={InfoSectionTheme?.infoSection}
            >
                
                {renderRow("Commuting to Work", data.workers)}
                {renderRow("Commuting to School", data.students)}
                {renderRow("Returning Home", data.goingHome)}
                {renderRow("Shopping / Leisure", data.shoppers)}
                {renderRow("Healthcare", data.healthcare)}
                {renderRow("Cargo / Delivery", data.cargo)}
                {renderRow("Public Transport", data.publicTransport)}
                {renderRow("City Services", data.services)}
                {renderRow("Other", data.other)}
            </InfoSection>
        );
    };

    return componentList;
}