<script lang="ts">
// This starter template is using Vue 3 <script setup> SFCs
// Check out https://vuejs.org/api/sfc-script-setup.html#script-setup
import * as signalR from "@microsoft/signalr";
import { Options, Vue } from "vue-class-component";
import ScheduleGraph from "./components/ScheduleGraph.vue";

export interface KilnInfo {
    measuredTemp: number;
    secondsElapsed: number | null;
    scheduleTarget: number | null;
    heating: boolean;
}

export interface Measurement {
    x: number;
    y: number;
}

export interface ScheduleRamp {
    id: number;
    order: number;
    tempRatePrHr: number;
    targetTemp: number;
    holdMinutes: number;
    calculatedTimeMinutes: number;
    calculatedTimeWithRampMinutes: number;
}

export interface KilnSchedule {
    id: number;
    name: string;
    defaultStartTemp: number;
    ramps: ScheduleRamp[];
}

@Options({
    components: {
        ScheduleGraph,
    },
})
export default class App extends Vue {
    connection: signalR.HubConnection | null = null;

    public connected = false;
    public kilnInfo: KilnInfo = <KilnInfo>{};
    public scheduleToStart: string | null = null;
    public schedule: KilnSchedule | null = null;
    public measurements: Measurement[] = [];
    
    reducedMeasurement = 0; // last sample index that has been reduced
    measurementDetailPeriod = 5 * 60; // five minutes worth of high-res data

    //host = "https://localhost:7135";
    host = "http://kiln-1.local:5000"

    async startSchedule() {
        await fetch(`${this.host}/kiln/schedule/${this.scheduleToStart}`, {
            method: "POST",
        });
        this.scheduleToStart = null;
    }

    async stopSchedule() {
        await fetch(`${this.host}/kiln/stop`, { method: "POST" });
    }

    async created() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.host}/control`)
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.connection.on("update", (info) => {
            this.kilnInfo = info;

            
            if(this.kilnInfo.secondsElapsed == null)
                return;

            this.measurements.push({
                x: this.kilnInfo.secondsElapsed,
                y: this.kilnInfo.measuredTemp,
            });

            var samplesThatShouldBeReduced = this.measurements.length - this.measurementDetailPeriod;
            samplesThatShouldBeReduced -= this.reducedMeasurement;

            if(samplesThatShouldBeReduced > 60) // reduce 1 minute worth of data
            {
                // clamp to 60
                samplesThatShouldBeReduced = samplesThatShouldBeReduced > 60 ? 60 : samplesThatShouldBeReduced;

                var samples = this.measurements.splice(this.reducedMeasurement+1, samplesThatShouldBeReduced);

                var sample = samples.reduce((s,c) => s + c.y, 0) / samples.length;
                this.measurements.splice(this.reducedMeasurement + 1, 0, {
                    x: samples[0].x,
                    y: sample
                });

                this.reducedMeasurement++;
            }
        });

        this.connection.on("currentSchedule", (schedule) => {
            this.measurements = [];
            this.schedule = schedule;
            console.log("schedule received", schedule);
        });

        this.connection.onclose(async () => {
            await this.start();
        });

        // Start the connection.
        this.start();
    }

    async start() {
        if (this.connection == null) {
            setTimeout(this.start, 5000);
            return;
        }

        try {
            await this.connection.start();
            console.log("SignalR Connected.");
            this.connected = true;
        } catch (err) {
            console.log(err);
            setTimeout(this.start, 5000);
        }
    }

    get progressTimeStamp() {
        return new Date((this.kilnInfo.secondsElapsed ?? 0) * 1000).toISOString().substring(11, 19);
    }
}
</script>

<template>
    <div class="container">
        <section class="meters">
            <div class="meter">
                <header>Connected</header>
                {{ connected }}
            </div>
            <div class="meter">
                <header>Temperature (F)</header>
                {{ kilnInfo.measuredTemp?.toFixed(1) }}
            </div>
            <div class="meter">
                <header>Target (F)</header>
                {{ kilnInfo.scheduleTarget?.toFixed(1) || "None" }}
            </div>
            <div class="meter">
                <header>Heating</header>
                {{ kilnInfo.heating }}
            </div>
            <div class="meter">
                <header>Progress</header>
                {{ progressTimeStamp }}
            </div>
            <div class="meter" v-if="schedule == null">
                <header>Start Schedule</header>
                <input type="text" v-model="scheduleToStart" />
                <button @click="startSchedule">Start</button>
            </div>
            <div class="meter" v-if="schedule != null">
                <header>{{ schedule.name }}</header>
                <button @click="stopSchedule">Stop</button>
            </div>
        </section>
        <div class="graph">
            <ScheduleGraph :schedule="schedule" :measurements="measurements"></ScheduleGraph>
        </div>
    </div>
</template>

<style scoped lang="scss">
.container {
    position: fixed;
    left: 0;
    top: 0;
    align-items: center;
    justify-content: center;
    height: 75%;
    width: 95%;
    background: green;
    margin-left: 2.5%;
    margin-top: 2.5%;

    .meters {
        width: 100%;
        display: flex;
        justify-content: center;
        background: #333;
        gap: 3em;

        .meter {
            min-width: 15vh;

            header {
                background: #444;
                width: calc(100% + 3vh);
                margin-left: -2vh;
                margin-top: -2vh;
                border-bottom: 1px solid #000;
                margin-bottom: 2vh;
                padding: 0.5vh;
                font-size: 1.5vh;
            }

            background: #555;
            padding: 2vh;
            font-family: monospace;
            font-size: 3vh;

            button {
                font-size: 1.5vh;
                padding: 1vh;
                margin: 1vh;
            }
        }
    }

    .graph {
        background: #666;
        width: 100%;
        height: 100%;
    }
}

.logo {
    height: 6em;
    padding: 1.5em;
    will-change: filter;
}

.logo:hover {
    filter: drop-shadow(0 0 2em #646cffaa);
}

.logo.vue:hover {
    filter: drop-shadow(0 0 2em #42b883aa);
}
</style>
