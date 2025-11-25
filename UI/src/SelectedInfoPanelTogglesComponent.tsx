import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { segmentActivity } from "./bindings";
import { useState, useEffect } from "react";

// Helper to safely get game components
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
    // This key MUST match the C# group property
    const componentKey = "TrafficSpy.Systems.TrafficUISystem";
    
    componentList[componentKey] = () => {
        const [data, setData] = useState<any>({});
        const [isVisible, setIsVisible] = useState(false);

        useEffect(() => {
            const sub = segmentActivity.subscribe((jsonString: string) => {
                if (!jsonString || jsonString === "{}") {
                    setIsVisible(false);
                    return;
                }
                try {
                    const parsed = JSON.parse(jsonString);
                    setData(parsed);
                    setIsVisible(true);
                } catch (e) { 
                    console.warn("[TrafficSpy] Parse error", e); 
                }
            });
            return () => sub.dispose();
        }, []);

        if (!isVisible) return null;

        // If game modules failed to load, return nothing or a simple fallback
        if (!InfoSection || !InfoRow) return null;

        // Render rows helper
        const renderRow = (label: string, count: number) => {
            if (!count || count <= 0) return null;
            return (
                <InfoRow 
                    left={label} 
                    right={count.toString()} 
                    uppercase={true} 
                    className={InfoRowTheme?.infoRow} 
                />
            );
        };

        return (
            <InfoSection 
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} 
                className={InfoSectionTheme?.infoSection}
            >
                <div style={{ marginBottom: "5rem", color: "rgba(255,255,255,0.5)", textTransform: "uppercase", letterSpacing: "1rem", fontSize: "14rem" }}>
                    Traffic Spy
                </div>
                {renderRow("Commuting to Work", data.workers)}
                {renderRow("Commuting to School", data.students)}
                {renderRow("Returning Home", data.goingHome)}
                {renderRow("Shopping", data.shoppers)}
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