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
            none: 0, 
            shopping: 0, 
            leisure: 0, 
            goingHome: 0, 
            goingToWork: 0, 
            movingAway: 0, 
            school: 0, 
            delivery: 0, 
            tourism: 0, 
            other: 0, 
            services: 0 
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
        const total = (data.none || 0) + (data.shopping || 0) + (data.leisure || 0) + 
                      (data.goingHome || 0) + (data.goingToWork || 0) + (data.movingAway || 0) + 
                      (data.school || 0) + (data.delivery || 0) + (data.tourism || 0) + 
                      (data.other || 0) + (data.services || 0);

        if (total === 0) return null;

        return (
            <div className={styles.activityPanel}>
                <div className={styles.title}>TRAFFIC ACTIVITY ({total})</div>
                {data.goingToWork > 0 && <div className={styles.row}><span>Commuting to Work</span> <span className={styles.count}>{data.goingToWork}</span></div>}
                {data.school > 0 && <div className={styles.row}><span>Commuting to School</span> <span className={styles.count}>{data.school}</span></div>}
                {data.goingHome > 0 && <div className={styles.row}><span>Returning Home</span> <span className={styles.count}>{data.goingHome}</span></div>}
                {data.shopping > 0 && <div className={styles.row}><span>Shopping</span> <span className={styles.count}>{data.shopping}</span></div>}
                {data.leisure > 0 && <div className={styles.row}><span>Leisure / Relaxing</span> <span className={styles.count}>{data.leisure}</span></div>}
                {data.delivery > 0 && <div className={styles.row}><span>Delivery / Commercial</span> <span className={styles.count}>{data.delivery}</span></div>}
                {data.services > 0 && <div className={styles.row}><span>Services / Healthcare</span> <span className={styles.count}>{data.services}</span></div>}
                {data.tourism > 0 && <div className={styles.row}><span>Tourism</span> <span className={styles.count}>{data.tourism}</span></div>}
                {data.movingAway > 0 && <div className={styles.row}><span>Moving Away</span> <span className={styles.count}>{data.movingAway}</span></div>}
                {data.other > 0 && <div className={styles.row}><span>Other</span> <span className={styles.count}>{data.other}</span></div>}
                {data.none > 0 && <div className={styles.row}><span>None / Unknown</span> <span className={styles.count}>{data.none}</span></div>}
            </div>
        );
    }
}