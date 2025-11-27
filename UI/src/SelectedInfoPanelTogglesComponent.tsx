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
                uppercase={false} 
                disableFocus={true}
                subRow={false}
                className={InfoRowTheme?.infoRow} 
                key={label} // Added key property for React list rendering
            />
        );
    };
    
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {

        const jsonString = useValue(activityData);
        const data: SegmentActivity = useMemo(() => {
            try {
                if (!jsonString || jsonString === "{}") {
                    return { 
                        none: 0, shopping: 0, leisure: 0, goingHome: 0, 
                        goingToWork: 0, movingIn: 0, movingAway: 0, school: 0, transporting: 0, returning: 0,
                        tourism: 0, other: 0, services: 0 
                    };
                }
                return JSON.parse(jsonString);
            } catch (e) {
                console.error("Parse error", e);
                return { 
                    none: 0, shopping: 0, leisure: 0, goingHome: 0, 
                    goingToWork: 0, movingIn: 0, movingAway: 0, school: 0, transporting: 0, returning: 0,
                    tourism: 0, other: 0, services: 0 
                };
            }
        }, [jsonString]);
        
        const total = (data.none || 0) + (data.shopping || 0) + (data.leisure || 0) +
                      (data.goingHome || 0) + (data.goingToWork || 0) + (data.movingIn || 0) + (data.movingAway || 0) +
                      (data.school || 0) + (data.transporting || 0) + (data.returning || 0) +
                      (data.tourism || 0) + (data.other || 0) + (data.services || 0);

        // 1. Create an array of objects for all categories EXCEPT "None/Unknown"
        //    Use ( || 0 ) to ensure safety if a field is undefined
        const sortedRows = [
            { label: "Going Home", count: data.goingHome || 0 },
            { label: "Going to Work", count: data.goingToWork || 0 },
            { label: "Going to School", count: data.school || 0 },
            { label: "Shopping", count: data.shopping || 0 },
            { label: "Leisure", count: data.leisure || 0 },
            { label: "Transporting / Delivery", count: data.transporting || 0 },
            { label: "Returning Truck", count: data.returning || 0 },
            { label: "Services", count: data.services || 0 },
            { label: "Tourism", count: data.tourism || 0 },
            { label: "Moving In", count: data.movingIn || 0 },
            { label: "Moving Away", count: data.movingAway || 0 },
            { label: "Other", count: data.other || 0 },
        ].sort((a, b) => b.count - a.count); // 2. Sort Descending

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
                
                {/* 3. Render the sorted list */}
                {sortedRows.map((row) => renderRow(row.label, row.count))}

                {/* 4. Always render "None" last */}
                {renderRow("None / Unknown", data.none)}
            </InfoSection>
        );
    };

    return componentList;
}