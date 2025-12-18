import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import {
    activityData, setTrafficFilter,
    highlightAgents, sethighlightAgents,
    showPedestrians, setShowPedestrians,
    showVehicles, setShowVehicles,
    directionMode, setDirectionMode,
    showRoutes, setShowRoutes
} from "./bindings";
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

    const renderRow = (label: string, count: number, filterKey: string, activeFilter: string, setActive: any) => {
        if (!count || count <= 0) return null;

        const isSelected = activeFilter === filterKey;
        const displayLabel = isSelected ? `> ${label}` : label;

        const textStyle: React.CSSProperties = {
            color: isSelected ? 'rgba(255, 235, 100, 1)' : 'rgba(120, 200, 255, 1)',
            fontWeight: isSelected ? '800' : 'normal',
        };

        return (
            <div
                key={label}
                onClick={() => {
                    const newValue = isSelected ? "" : filterKey;
                    setActive(newValue);
                    setTrafficFilter(filterKey);
                }}
                style={{ cursor: "pointer" }}
            >
                <InfoRow
                    left={<span style={textStyle}>{displayLabel}</span>}
                    right={<span style={textStyle}>{count.toString()}</span>}
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
        const showHighlights = useValue(highlightAgents);
        const showPeds = useValue(showPedestrians);
        const showCars = useValue(showVehicles);
        const showPathLines = useValue(showRoutes);
        const currentDirMode = useValue(directionMode); // Get current mode
        const [activeFilter, setActiveFilter] = useState<string>("");

        const data: SegmentActivity = useMemo(() => {
            try { return JSON.parse(jsonString || "{}"); } catch(e) { return {}; }
        }, [jsonString]);
        
        // If data is empty (no road selected), hide the component entirely.
        // TrafficUISystem sends "{}" when selection is not a road.
        if (!data || Object.keys(data).length === 0) return null;

        const total = (data.none || 0) + (data.shopping || 0) + (data.leisure || 0) +
            (data.goingHome || 0) + (data.goingToWork || 0) + (data.movingIn || 0) + (data.movingAway || 0) +
            (data.school || 0) + (data.transporting || 0) + (data.returning || 0) +
            (data.tourism || 0) + (data.other || 0) + (data.services || 0);

        const totalStyle: React.CSSProperties = {
            color: activeFilter === "" ? 'rgba(255, 235, 100, 1)' : 'rgba(120, 200, 255, 1)',
            fontWeight: activeFilter === "" ? '800' : 'normal',
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

        // Mutual Exclusivity (prevent both being off)
        const toggleVehicles = () => {
            if (showCars && !showPeds) setShowPedestrians(true);
            setShowVehicles(!showCars);
        }
        const togglePedestrians = () => {
            if (showPeds && !showCars) setShowVehicles(true);
            setShowPedestrians(!showPeds);
        }

        // Cycles through: 0 (Both) -> 1 (Forward) -> 2 (Backward) -> 0
        const toggleDirection = () => {
            const nextMode = (currentDirMode + 1) % 3;
            setDirectionMode(nextMode);
        }

        const getDirectionLabel = () => {
            switch(currentDirMode) {
                case 1: return "Side A";
                case 2: return "Side B";
                default: return "Both Sides";
            }
        }

        return (
            <InfoSection
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                disableFocus={true}
                className={InfoSectionTheme?.infoSection}
            >
                {/* 1. RESET / TOTAL ROW */}
                <div
                    onClick={() => { setActiveFilter(""); setTrafficFilter("RESET"); }}
                    style={{ cursor: "pointer", marginBottom: "5px" }}
                >
                    <InfoRow
                        left={<span style={totalStyle}>{activeFilter === "" ? "> ALL ACTIVITY" : "RESET FILTER"}</span>}
                        right={<span style={totalStyle}>{total.toString()}</span>}
                        uppercase={true}
                        disableFocus={true}
                        subRow={false}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 2. DIRECTION TOGGLE */}
                <div
                    onClick={toggleDirection}
                    style={{ cursor: "pointer", marginBottom: "0px" }}
                >
                    <InfoRow
                        left={<span style={{ color: "white", fontSize: "1em", opacity: 0.8 }}>Road Side:</span>}
                        right={
                            <span style={{
                                color: currentDirMode === 0 ? "white" : "rgb(100, 255, 100)",
                                fontSize: "1em",
                                fontWeight: currentDirMode === 0 ? "normal" : "bold"
                            }}>
                                {getDirectionLabel()}
                            </span>
                        }
                        uppercase={false}
                        subRow={true}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 3. INCLUDE VEHICLES TOGGLE */}
                <div
                    onClick={toggleVehicles}
                    style={{ cursor: "pointer", marginBottom: "0px" }}
                >
                    <InfoRow
                        left={<span style={{ color: "white", fontSize: "1em", opacity: 0.8 }}>Include Vehicles</span>}
                        right={
                            <div style={{
                                width: "12px", height: "12px",
                                borderRadius: "50%",
                                border: "1px solid white",
                                backgroundColor: showCars ? "rgb(100, 255, 100)" : "transparent"
                            }}></div>
                        }
                        uppercase={false}
                        subRow={true}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 4. INCLUDE PEDESTRIANS TOGGLE */}
                <div
                    onClick={togglePedestrians}
                    style={{ cursor: "pointer", marginBottom: "5px" }}
                >
                    <InfoRow
                        left={<span style={{ color: "white", fontSize: "1em", opacity: 0.8 }}>Include Pedestrians</span>}
                        right={
                            <div style={{
                                width: "12px", height: "12px",
                                borderRadius: "50%",
                                border: "1px solid white",
                                backgroundColor: showPeds ? "rgb(100, 255, 100)" : "transparent"
                            }}></div>
                        }
                        uppercase={false}
                        subRow={true}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 5. HIGHLIGHT TOGGLE (Visible only when no specific filter active) */}
                <div
                    onClick={() => sethighlightAgents(!showHighlights)}
                    style={{ cursor: "pointer", marginBottom: "10px" }}
                >
                    <InfoRow
                        left={<span style={{ color: "white", fontSize: "1em", opacity: 0.8 }}>Highlight Destinations & Agents</span>}
                        right={
                            <div style={{
                                width: "12px", height: "12px",
                                borderRadius: "50%",
                                border: "1px solid white",
                                backgroundColor: showHighlights ? "rgb(100, 255, 100)" : "transparent"
                            }}></div>
                        }
                        uppercase={false}
                        subRow={true}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 6. SHOW ROUTES TOGGLE */}
                <div
                    onClick={() => setShowRoutes(!showPathLines)}
                    style={{ cursor: "pointer", marginBottom: "10px" }}
                >
                    <InfoRow
                        left={<span style={{ color: "white", fontSize: "1em", opacity: 0.8 }}>Show Route Lines</span>}
                        right={
                            <div style={{
                                width: "12px", height: "12px",
                                borderRadius: "50%",
                                border: "1px solid white",
                                backgroundColor: showPathLines ? "rgb(100, 255, 100)" : "transparent"
                            }}></div>
                        }
                        uppercase={false}
                        subRow={true}
                        className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 7. DATA ROWS */}
                {sortedRows.map((row) => row.count > 0 ? renderRow(row.label, row.count, row.key, activeFilter, setActiveFilter) : null)}
                {data.none > 0 ? renderRow("None / Unknown", data.none, "none", activeFilter, setActiveFilter) : null}

            </InfoSection>
        );
    };

    return componentList;
}