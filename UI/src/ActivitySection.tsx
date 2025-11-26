import React, { Component } from 'react';
import styles from "./activity.module.scss"; 
import { activityData } from "./bindings";
import { SegmentActivity } from "./types";

interface ActivitySectionProps {
    group?: string; 
}

export class ActivitySection extends Component<ActivitySectionProps> {
    state = {
        data: { 
            workers: 0, 
            students: 0, 
            shoppers: 0, 
            goingHome: 0, 
            healthcare: 0, 
            cargo: 0, 
            services: 0, 
            publicTransport: 0,
            other: 0 
        } as SegmentActivity
    };

    private unsubscribe: (() => void) | undefined;

    componentDidMount() {
        const subscription = activityData.subscribe((jsonString: string) => {
            try {
                const parsed = JSON.parse(jsonString || "{}");
                this.setState({ data: { ...this.state.data, ...parsed } });
            } catch (e) { console.warn("TrafficSpy JSON Parse Error", e); }
        });
        this.unsubscribe = () => subscription.dispose();
    }

    componentWillUnmount() {
        if (this.unsubscribe) this.unsubscribe();
    }

    render() {
        const { data } = this.state;
        const total = (data.workers || 0) + (data.students || 0) + (data.shoppers || 0) + 
                      (data.goingHome || 0) + (data.healthcare || 0) + 
                      (data.cargo || 0) + (data.services || 0) + 
                      (data.publicTransport || 0) + (data.other || 0);

        if (total === 0) return null;

        return (
            <div className={styles.activityPanel}>
                <div className={styles.title}>TRAFFIC ACTIVITY ({total})</div>
                {data.workers > 0 && <div className={styles.row}><span>Commuting to Work</span> <span className={styles.count}>{data.workers}</span></div>}
                {data.students > 0 && <div className={styles.row}><span>Commuting to School</span> <span className={styles.count}>{data.students}</span></div>}
                {data.goingHome > 0 && <div className={styles.row}><span>Returning Home</span> <span className={styles.count}>{data.goingHome}</span></div>}
                {data.shoppers > 0 && <div className={styles.row}><span>Shopping / Leisure</span> <span className={styles.count}>{data.shoppers}</span></div>}
                {data.healthcare > 0 && <div className={styles.row}><span>Healthcare</span> <span className={styles.count}>{data.healthcare}</span></div>}
                {data.cargo > 0 && <div className={styles.row}><span>Cargo / Delivery</span> <span className={styles.count}>{data.cargo}</span></div>}
                {data.publicTransport > 0 && <div className={styles.row}><span>Public Transport</span> <span className={styles.count}>{data.publicTransport}</span></div>}
                {data.services > 0 && <div className={styles.row}><span>City Services</span> <span className={styles.count}>{data.services}</span></div>}
                {data.other > 0 && <div className={styles.row}><span>Other</span> <span className={styles.count}>{data.other}</span></div>}
            </div>
        );
    }
}