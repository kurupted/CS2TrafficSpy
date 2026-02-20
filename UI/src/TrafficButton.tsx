import { useValue } from "cs2/api";
import { trigger } from "cs2/api";
import { Button, Portal, Tooltip } from "cs2/ui";
import { toolActive } from "./bindings";

const TrafficButton = () => {
    console.log("[TrafficSpy] TrafficButton rendering...");

    try {
        const active = useValue(toolActive);

        console.log("[TrafficSpy] Button state:", active);

        return (
            <>
                <Tooltip tooltip="Traffic Spy">
                    <Button
                        src="coui://uil/Standard/GenericVehicles.svg"
                        selected={active}
                        variant="floating"
                        onSelect={() => {
                            console.log("[TrafficSpy] Button clicked, toggling from", active);
                            trigger("TrafficSpy", "setToolActive", !active);
                        }}
                    />
                </Tooltip>

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