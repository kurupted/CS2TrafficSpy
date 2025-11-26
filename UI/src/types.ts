export interface SegmentActivity {
    none: number;
    shopping: number;
    leisure: number;
    goingHome: number;
    goingToWork: number;
    movingAway: number;
    school: number;
    delivery: number;
    tourism: number;
    other: number;
    services: number;
}

export interface Entity {
    index: number;
    version: number;
}

export interface Theme {
    entity: Entity;
    name: string;
    icon: string;
}