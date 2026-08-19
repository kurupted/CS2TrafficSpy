import { useMemo } from "react";
import { useValue, trigger } from "cs2/api";
import { Button, Portal, Tooltip } from "cs2/ui";
import { toolActive, isRoadSelected, trafficJamData } from "./bindings";
import { TrafficMonitorPanel } from "./TrafficMonitorPanel";

const TrafficButton = () => {
    //console.log("[TrafficSpy] TrafficButton rendering...");

    try {
        const active = useValue(toolActive);
        const roadSelected = useValue(isRoadSelected);
        const rawJamData = useValue(trafficJamData);

        const alertCount = useMemo(() => {
            try {
                const parsed = JSON.parse(rawJamData || "{}");
                if (Array.isArray(parsed)) return 0;
                const notifs = parsed.notifications || [];
                return notifs.length;
            } catch (e) {
                return 0;
            }
        }, [rawJamData]);

        const tooltipText = alertCount > 0
            ? `Traffic Spy (${alertCount} traffic jam alert${alertCount === 1 ? "" : "s"})`
            : "Traffic Spy";

        return (
            <>
                <div style={{ position: "relative", display: "inline-flex", alignItems: "center", justifyContent: "center" }}>
                    <Tooltip tooltip={tooltipText}>
                        <Button
                            src="coui://uil/Standard/GenericVehicles.svg"
                            selected={active}
                            variant="floating"
                            onSelect={() => {
                                trigger("TrafficSpy", "setToolActive", !active);
                            }}
                        />
                    </Tooltip>

                    {alertCount > 0 && (
                        <div
                            style={{
                                position: "absolute",
                                bottom: "-2rem",
                                right: "-2rem",
                                width: alertCount > 9 ? "auto" : "18rem",
                                minWidth: "18rem",
                                height: "18rem",
                                padding: alertCount > 9 ? "0 4rem" : "0",
                                borderRadius: "9999rem",
                                backgroundColor: "#ff3b30",
                                color: "#ffffff",
                                fontSize: "11rem",
                                fontWeight: 800,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                boxShadow: "0 2rem 5rem rgba(0, 0, 0, 0.7), 0 0 8rem rgba(255, 59, 48, 0.6)",
                                border: "none",
                                pointerEvents: "none",
                                zIndex: 10,
                                lineHeight: "1",
                                boxSizing: "border-box",
                                textAlign: "center"
                            }}
                        >
                            <span
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    width: "100%",
                                    height: "100%",
                                    textAlign: "center",
                                    margin: 0,
                                    padding: 0,
                                    lineHeight: 1
                                }}
                            >
                                {alertCount > 99 ? "99+" : alertCount}
                            </span>
                        </div>
                    )}
                </div>

                {active && (
                    <Portal>
                        <div style={{
                            position: "absolute",
                            top: "150rem",
                            left: "50%",
                            transform: "translateX(-50%)",
                            padding: "10rem 20rem",
                            background: "rgba(0, 0, 0, 0.8)",
                            color: "white",
                            borderRadius: "5rem",
                            fontSize: "16rem",
                            pointerEvents: "none",
                            zIndex: 10000
                        }}>
                            Pick a road segment, path, or transit structure.
                        </div>
                        {!roadSelected && (
                            <TrafficMonitorPanel onClose={() => trigger("TrafficSpy", "setToolActive", false)} />
                        )}
                    </Portal>
                )}
            </>
        );
    } catch (error) {
        console.error("[TrafficSpy] Button render error:", error);
        return null;
    }
};

export default TrafficButton;
