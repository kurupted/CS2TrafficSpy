import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { activityData, setTrafficFilter } from "./bindings";
import { useValue } from "cs2/api";
import { useMemo, useState } from "react";
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

    // Helper to render rows with click handlers
    const renderRow = (label: string, count: number, filterKey: string, activeFilter: string, setActive: any) => {
        if (!count || count <= 0) return null;
        
        const isSelected = activeFilter === filterKey;
        // Fix 4: Prepend > < to selected labels
        const displayLabel = isSelected ? `> ${label}` : label; 
        
        // Fix 4: Define styles for clickable elements
        const clickableStyle: React.CSSProperties = {
            cursor: "pointer",
            color: isSelected ? 'rgb(255, 255, 0)' : 'rgb(120, 190, 255)', // Yellow if selected, Blue otherwise
            fontWeight: isSelected ? 'bold' : 'normal',
        };

        return (
            <div 
                key={label}
                onClick={() => {
                    const newValue = isSelected ? "" : filterKey;
                    setActive(newValue);
                    setTrafficFilter(filterKey); 
                }}
                style={clickableStyle} // Apply the style to the container div
            >
                <InfoRow 
                    left={displayLabel} 
                    right={count.toString()} 
                    uppercase={false} 
                    disableFocus={true}
                    subRow={false}
                    className={InfoRowTheme?.infoRow} 
                />
            </div>
        );
    };
    
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {

        const jsonString = useValue(activityData);
        const [activeFilter, setActiveFilter] = useState<string>("");

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
                      
        // Fix 4: Style for the total row
        const totalRowStyle: React.CSSProperties = {
            cursor: "pointer",
            color: activeFilter === "" ? 'rgb(255, 255, 0)' : 'white', // Yellow if reset, White otherwise
            fontWeight: activeFilter === "" ? 'bold' : 'normal',
        };

        const sortedRows = [
            { label: "Going Home", count: data.goingHome || 0, key: "goingHome" },
            { label: "Going to Work", count: data.goingToWork || 0, key: "goingToWork" },
            { label: "Going to School", count: data.school || 0, key: "school" },
            { label: "Shopping", count: data.shopping || 0, key: "shopping" },
            { label: "Leisure", count: data.leisure || 0, key: "leisure" },
            { label: "Transporting / Delivery", count: data.transporting || 0, key: "transporting" },
            { label: "Returning Truck", count: data.returning || 0, key: "returning" },
            { label: "Services", count: data.services || 0, key: "services" },
            { label: "Tourism", count: data.tourism || 0, key: "tourism" },
            { label: "Moving In", count: data.movingIn || 0, key: "movingIn" },
            { label: "Moving Away", count: data.movingAway || 0, key: "movingAway" },
            { label: "Other", count: data.other || 0, key: "other" },
        ].sort((a, b) => b.count - a.count);

        return (
            <InfoSection 
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} 
                disableFocus={true} 
                className={InfoSectionTheme?.infoSection}
            >
                {/* Total Row (Clickable/Reset) */}
                <div 
                    onClick={() => {
                        setActiveFilter("");
                        setTrafficFilter("RESET"); 
                    }}
                    style={totalRowStyle}
                >
                    <InfoRow 
                        left={activeFilter === "" ? "> TOTAL ACTIVITY (RESET)" : "TOTAL ACTIVITY (RESET)"}
                        right={total.toString()} 
                        uppercase={true} 
                        disableFocus={true}
                        subRow={false}
                        className={InfoRowTheme?.infoRow} 
                    />
                </div>
                
                {/* Filterable Rows */}
                {sortedRows.map((row) => row.count > 0 ? renderRow(row.label, row.count, row.key, activeFilter, setActiveFilter) : null)}

                {/* None/Unknown Row */}
                {data.none > 0 ? renderRow("None / Unknown", data.none, "none", activeFilter, setActiveFilter) : null}
            </InfoSection>
        );
    };

    return componentList;
}