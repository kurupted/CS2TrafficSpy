import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { segmentActivity } from "./bindings";
import { useState, useEffect } from "react";

// Define the component interface
interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

// Safely get game modules with error handling
function safeGetModule(path: string, exportName: string, fallback: any = null) {
    try {
        const module = getModule(path, exportName);
        if (module) {
            console.log(`[TrafficSpy] ? Loaded ${exportName}`);
            return module;
        }
    } catch (error) {
        console.warn(`[TrafficSpy] ? Failed to load ${exportName}:`, error);
    }
    return fallback;
}

// Import game modules
const InfoSectionTheme = safeGetModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
    "classes"
);

const InfoRowTheme = safeGetModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
);

const InfoSection = safeGetModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

const InfoRow = safeGetModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

const SectionTitle = safeGetModule(
    "game-ui/game/components/selected-info-panel/shared-components/section-title/section-title.tsx",
    "SectionTitle"
);

// Main export function
export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {
    console.log("[TrafficSpy] SelectedInfoPanelTogglesComponent called");
    console.log("[TrafficSpy] componentList type:", typeof componentList);
    console.log("[TrafficSpy] componentList keys:", Object.keys(componentList || {}));

    // Register the component - MUST match C# namespace.classname
    const componentKey = "TrafficSpy.Systems.TrafficUISystem";
    
    try {
        componentList[componentKey] = (e: InfoSectionComponent) => {
            console.log("[TrafficSpy] Component rendering, group:", e.group);
            
            // State management
            const [data, setData] = useState<any>({});
            const [isVisible, setIsVisible] = useState(false);

            // Subscribe to the binding
            useEffect(() => {
                console.log("[TrafficSpy] Setting up subscription...");
                const sub = segmentActivity.subscribe((jsonString: string) => {
                    console.log("[TrafficSpy] Received data:", jsonString);
                    if (!jsonString || jsonString === "{}") {
                        setIsVisible(false);
                        return;
                    }
                    try {
                        const parsed = JSON.parse(jsonString);
                        setData(parsed);
                        setIsVisible(true);
                        console.log("[TrafficSpy] Parsed data:", parsed);
                    } catch (err) { 
                        console.warn("[TrafficSpy] JSON Parse Error:", err); 
                    }
                });
                return () => {
                    console.log("[TrafficSpy] Disposing subscription");
                    sub.dispose();
                };
            }, []);

            // If modules didn't load, show a simple fallback
            if (!InfoSection || !InfoRow || !SectionTitle) {
                console.warn("[TrafficSpy] Missing game components, using fallback");
                if (!isVisible) return null;
                
                const total = (data.workers || 0) + (data.students || 0) + (data.shoppers || 0) + 
                              (data.goingHome || 0) + (data.healthcare || 0) + 
                              (data.cargo || 0) + (data.services || 0) + 
                              (data.publicTransport || 0) + (data.other || 0);
                
                return (
                    <div style={{ padding: "10rem", color: "white", backgroundColor: "rgba(0,0,0,0.5)" }}>
                        <div style={{ fontWeight: "bold", marginBottom: "5rem" }}>TRAFFIC SPY ({total})</div>
                        {data.workers > 0 && <div>Commuting to Work: {data.workers}</div>}
                        {data.students > 0 && <div>Commuting to School: {data.students}</div>}
                        {data.goingHome > 0 && <div>Returning Home: {data.goingHome}</div>}
                        {data.shoppers > 0 && <div>Shopping / Leisure: {data.shoppers}</div>}
                        {data.healthcare > 0 && <div>Healthcare: {data.healthcare}</div>}
                        {data.cargo > 0 && <div>Cargo / Delivery: {data.cargo}</div>}
                        {data.publicTransport > 0 && <div>Public Transport: {data.publicTransport}</div>}
                        {data.services > 0 && <div>City Services: {data.services}</div>}
                        {data.other > 0 && <div>Other: {data.other}</div>}
                    </div>
                );
            }

            // Don't render if not visible
            if (!isVisible) {
                console.log("[TrafficSpy] Not visible, returning null");
                return null;
            }

            // Calculate total
            const total = (data.workers || 0) + (data.students || 0) + (data.shoppers || 0) + 
                          (data.goingHome || 0) + (data.healthcare || 0) + 
                          (data.cargo || 0) + (data.services || 0) + 
                          (data.publicTransport || 0) + (data.other || 0);

            console.log("[TrafficSpy] Rendering with total:", total);

            // Helper to render a row
            const renderRow = (label: string, count: number) => {
                if (!count) return null;
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

            // Return the UI
            return (
                <InfoSection 
                    focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} 
                    disableFocus={true} 
                    className={InfoSectionTheme?.infoSection}
                >
                    <SectionTitle title={`TRAFFIC SPY (${total})`} />
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
        
        console.log(`[TrafficSpy] ? Registered component: ${componentKey}`);
    } catch (error) {
        console.error("[TrafficSpy] ? Failed to register component:", error);
    }

    return componentList;
}