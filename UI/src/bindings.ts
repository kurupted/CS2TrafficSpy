import { bindValue, trigger } from "cs2/api";

export const toolActive = bindValue<boolean>("TrafficSpy", "toolActive", false);
export const activityData = bindValue<string>("TrafficSpy", "activityData", "{}");
export const highlightAgents = bindValue<boolean>("TrafficSpy", "highlightAgents", false);
export const displayMode = bindValue<number>("TrafficSpy", "displayMode", 0);
export const showRoutes = bindValue<boolean>("TrafficSpy", "showRoutes", false);
export const directionMode = bindValue<number>("TrafficSpy", "directionMode", 0);
export const rangeMode = bindValue<number>("TrafficSpy", "rangeMode", 1);
//export const grayWorld = bindValue<boolean>("TrafficSpy", "grayWorld", false);


export const setTrafficFilter = (filter: string) => trigger("TrafficSpy", "setTrafficFilter", filter);
export const sethighlightAgents = (active: boolean) => trigger("TrafficSpy", "sethighlightAgents", active);
export const setDisplayMode = (mode: number) => trigger("TrafficSpy", "setDisplayMode", mode);
export const setShowRoutes = (active: boolean) => trigger("TrafficSpy", "setShowRoutes", active);

export const setDirectionMode = (mode: number) => trigger("TrafficSpy", "setDirectionMode", mode);
export const setRangeMode = (mode: number) => trigger("TrafficSpy", "setRangeMode", mode);
//export const setGrayWorld = (active: boolean) => trigger("TrafficSpy", "setGrayWorld", active);