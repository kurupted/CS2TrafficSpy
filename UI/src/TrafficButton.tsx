import React, { Component } from 'react';
import { FloatingButton, Tooltip } from "cs2/ui"; 
import { trigger } from "cs2/api";
import { toolActive } from "./bindings"; 

export default class TrafficButton extends Component {
    state = {
        active: toolActive.value,
        hovering: false
    };

    private unsubscribe: (() => void) | undefined;

    componentDidMount() {
        const subscription = toolActive.subscribe((val: boolean) => {
            this.setState({ active: val });
        });
        
        // Store the cleanup function
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
                    src="coui://uil/Standard/Cars.svg" 
                    selected={this.state.active} 
                    // FIXED: Changed 'onSelect' to 'onClick' to match your types.d.ts
                    onClick={() => { 
                        trigger("trafficExplorer", "setToolActive", !this.state.active); 
                    }}
                />
            </Tooltip>
        );
    }
}