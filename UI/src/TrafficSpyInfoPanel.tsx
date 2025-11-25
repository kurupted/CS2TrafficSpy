import React, { useState, useEffect } from 'react';
import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { segmentActivity } from "./bindings";
// Import types locally to avoid runtime crashes from "cs2/bindings"
import { SegmentActivity, Theme } from "./types"; 

// Helper to safely get game modules without crashing
function safeGet(path: string, name: string) {
    const mod = getModule(path, name);
    if (!mod) {
        console.warn(`[TrafficSpy] Module not found: ${path}:${name}`);
        return null;
    }
    return mod;
}

// Safely fetch components. If these fail, they return null instead of crashing.
const InfoSectionTheme: any = safeGet("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss", "classes");
const InfoRowTheme: any = safeGet("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss", "classes");
const InfoSection: any = safeGet("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx", "InfoSection");
const InfoRow: any = safeGet("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx", "InfoRow");
const SectionTitle: any = safeGet("game-ui/game/components/selected-info-panel/shared-components/section-title/section-title.tsx", "SectionTitle");

export const TrafficSpyInfoPanel = () => {
    const [data, setData] = useState<SegmentActivity>({
        workers: 0, students: 0, shoppers: 0, goingHome: 0, 
        healthcare: 0, cargo: 0, services: 0, publicTransport: 0, other: 0
    });
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
                console.warn("[TrafficSpy] JSON Parse Error", e);
            }
        });
        return () => sub.dispose();
    }, []);

    // If dependencies are missing, just hide the panel. DO NOT CRASH.
    if (!isVisible || !InfoSection || !InfoRow || !InfoSectionTheme || !InfoRowTheme) {
        return null;
    }

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
                className={InfoRowTheme.infoRow} 
            />
        );
    };

    const focusKey = VanillaComponentResolver.instance?.FOCUS_DISABLED || null;

    return (
        <InfoSection focusKey={focusKey} disableFocus={true} className={InfoSectionTheme.infoSection}>
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
};