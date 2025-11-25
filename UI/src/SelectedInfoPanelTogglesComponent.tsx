import { TrafficSpyInfoPanel } from "./TrafficSpyInfoPanel";

// This component acts as a "Router". It tells the game:
// "When you see the C# system 'TrafficSpy.Systems.TrafficUISystem', show this React component."
export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {
    
    // The key MUST match your C# Class: namespace + class name
    componentList["TrafficSpy.Systems.TrafficUISystem"] = () => {
        return <TrafficSpyInfoPanel />;
    };

    return componentList;
};