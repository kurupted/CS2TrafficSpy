import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { activityData } from "./bindings";
import { useValue } from "cs2/api";
import { useMemo } from "react";
import { SegmentActivity } from "./types";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

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

export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {

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
    
    // Register component
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {

        const jsonString = useValue(activityData);
        const data: SegmentActivity = useMemo(() => {
            try {
                if (!jsonString || jsonString === "{}") {
                    return { 
                        none: 0, shopping: 0, leisure: 0, goingHome: 0, 
                        goingToWork: 0, movingAway: 0, school: 0, delivery: 0, 
                        tourism: 0, other: 0, services: 0 
                    };
                }
                return JSON.parse(jsonString);
            } catch (e) {
                console.error("Parse error", e);
                return { 
                    none: 0, shopping: 0, leisure: 0, goingHome: 0, 
                    goingToWork: 0, movingAway: 0, school: 0, delivery: 0, 
                    tourism: 0, other: 0, services: 0 
                };
            }
        }, [jsonString]);
        
        // Calculate Total
        const total = (data.none || 0) + (data.shopping || 0) + (data.leisure || 0) +
                      (data.goingHome || 0) + (data.goingToWork || 0) + (data.movingAway || 0) +
                      (data.school || 0) + (data.delivery || 0) + (data.tourism || 0) +
                      (data.other || 0) + (data.services || 0);

        return (
            <InfoSection 
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} 
                disableFocus={true} 
                className={InfoSectionTheme?.infoSection}
            >
                <InfoRow 
                    left="TOTAL ACTIVITY" 
                    right={total.toString()} 
                    uppercase={true} 
                    disableFocus={true}
                    subRow={false}
                    className={InfoRowTheme?.infoRow} 
                />
                
                {renderRow("Going to Work", data.goingToWork)}
                {renderRow("Going to School", data.school)}
                {renderRow("Returning Home", data.goingHome)}
                {renderRow("Shopping", data.shopping)}
                {renderRow("Leisure / Relaxing", data.leisure)}
                {renderRow("Delivery / Commercial", data.delivery)}
                {renderRow("Services / Healthcare", data.services)}
                {renderRow("Tourism", data.tourism)}
                {renderRow("Moving Away", data.movingAway)}
                {renderRow("Other", data.other)}
                {renderRow("None / Unknown", data.none)}
            </InfoSection>
        );
    };

    return componentList;
}