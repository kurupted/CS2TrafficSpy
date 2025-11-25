import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { segmentActivity } from "./bindings";
import { useState, useEffect } from "react";

// 1. Define types LOCALLY to prevent runtime crashes
interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

// 2. Fetch Game Modules (Exactly like EmploymentTracker, but cast to 'any' for safety)
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

const SectionTitle: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/section-title/section-title.tsx",
    "SectionTitle"
);

// 3. The Main Export
export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {

    // 4. Register the Component
    // The key MUST match your C# Class Name: Namespace.ClassName
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {
        
        // --- TrafficSpy Logic (Inline) ---
        // We ignore 'e.group' and use our own binding because it handles complex JSON data better
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
                } catch (e) { console.warn(e); }
            });
            return () => sub.dispose();
        }, []);

        // Safety check: If game modules failed to load, don't render (prevents white screen)
        if (!isVisible || !InfoSection || !InfoRow) return null;

        const total = (data.workers || 0) + (data.students || 0) + (data.shoppers || 0) + 
                      (data.goingHome || 0) + (data.healthcare || 0) + 
                      (data.cargo || 0) + (data.services || 0) + 
                      (data.publicTransport || 0) + (data.other || 0);

        const renderRow = (label: string, count: number) => {
            if (!count) return null;
            return (
                <InfoRow 
                    left={label} 
                    right={count.toString()} 
                    uppercase={true} 
                    disableFocus={true} 
                    subRow={false}
                    className={InfoRowTheme.infoRow} 
                />
            );
        };

        // Return the UI using the Native components
        return (
            <InfoSection focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} disableFocus={true} className={InfoSectionTheme.infoSection}>
                {SectionTitle && <SectionTitle title={`TRAFFIC SPY (${total})`} />}
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
    }

    return componentList;
}