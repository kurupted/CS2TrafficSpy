import React, { Component } from 'react';
import { FloatingButton, Tooltip } from "cs2/ui"; 
import { trigger } from "cs2/api";
import { toolActive } from "./bindings"; 

export default class TrafficButton extends Component {
    state = {
        active: toolActive.value, // This will now be 'false' by default instead of crashing
        hovering: false
    };

    private unsubscribe: (() => void) | undefined;

    componentDidMount() {
        const subscription = toolActive.subscribe((val: boolean) => {
            this.setState({ active: val });
        });
        this.unsubscribe = () => subscription.dispose();
    }

    componentWillUnmount() {
        if (this.unsubscribe) {
            this.unsubscribe();
        }
    }

    render() {
        return (
            <Tooltip tooltip="Traffic Explorer Tool (Ctrl+T)">
                <FloatingButton 
                    src="coui://uil/Standard/GenericVehicles.svg" 
                    selected={this.state.active} 
                    onSelect={() => { 
                        trigger("TrafficSpy", "setToolActive", !this.state.active); 
                    }}
                />
            </Tooltip>
        );
    }
}