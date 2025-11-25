import { getModule } from "cs2/modding";
import { Theme } from "cs2/bindings";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import { segmentActivity } from "./bindings";
import { useState, useEffect } from "react";

interface InfoSectionComponent {
	group: string;
	tooltipKeys: Array<string>;
	tooltipTags: Array<string>;
}

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

function handleClick(eventName: string) {
	// This triggers an event on C# side and C# designates the method to implement.
}



export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {

    // Render rows helper
    const renderRow = (label: string, count: number): any => {
        if (!count || count <= 0) return null;
        return (
            <InfoRow 
                left={label} 
                right={count.toString()} 
                uppercase={true} 
                disableFocus={true}
                className={InfoRowTheme?.infoRow} 
            />
        );
    };
    
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => { // This key MUST match the C# group property
        const data = JSON.parse(e.group);
        console.log("spy data", data);

        const infs = <InfoSection focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} disableFocus={true} className={InfoSectionTheme.infoSection}>
                {renderRow("Commuting to Work", data.workers)}
                {renderRow("Commuting to School", data.students)}
                {renderRow("Returning Home", data.goingHome)}
                {renderRow("Shopping", data.shoppers)}
		</InfoSection>
			;
		return infs;
    };
    /*
                    {renderRow("Commuting to Work", data.workers)}
                {renderRow("Commuting to School", data.students)}
                {renderRow("Returning Home", data.goingHome)}
                {renderRow("Shopping", data.shoppers)}
                {renderRow("Healthcare", data.healthcare)}
                {renderRow("Cargo / Delivery", data.cargo)}
                {renderRow("Public Transport", data.publicTransport)}
                {renderRow("City Services", data.services)}
                {renderRow("Other", data.other)}
                */

    return componentList as any;
}

