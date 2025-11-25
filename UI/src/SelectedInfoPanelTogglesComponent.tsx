import { TrafficSpyInfoPanel } from "./TrafficSpyInfoPanel";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {
    
    // The key here MUST match the C# Namespace + Class Name
    componentList["TrafficSpy.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {
        return <TrafficSpyInfoPanel />;
    }

    return componentList;
}