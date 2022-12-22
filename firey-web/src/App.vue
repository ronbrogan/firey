<script lang="ts">
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

</script>
<script setup lang="ts">
import * as signalR from "@microsoft/signalr";
import ScheduleChart from './components/ScheduleChart.vue';
import { ref, computed, onMounted } from "vue";

let connection: signalR.HubConnection | null = null;

const connected = ref(false);
const kilnInfo = ref(<KilnInfo>{});
const scheduleToStart = ref<string | null>(null);
const schedule = ref<KilnSchedule | null>(null);
const measurements = ref<Measurement[]>([]);

let reducedMeasurement = 0; // last sample index that has been reduced
let measurementDetailPeriod = 5 * 60; // five minutes worth of high-res data


let host = `${window.location.protocol}//${window.location.hostname}`;

async function startSchedule() {
    await fetch(`${host}/api/kiln/schedule/${scheduleToStart.value}`, {
        method: "POST",
    });
    scheduleToStart.value = null;
}

async function stopSchedule() {
    await fetch(`${host}/api/kiln/stop`, { method: "POST" });
}

let ready = false;

onMounted(() => {
    if(import.meta.env.VITE_backend_port)
        host += ":" + import.meta.env.VITE_backend_port;

    connection = new signalR.HubConnectionBuilder()
        .withUrl(`${host}/control`, { withCredentials: false })
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("update", (info) => {
        kilnInfo.value = info;

        if(!ready){
            console.log("not ready for graphing");
            return;
        }

        if (kilnInfo.value.secondsElapsed) {
            measurements.value.push({
                x: kilnInfo.value.secondsElapsed ?? 0,
                y: kilnInfo.value.measuredTemp,
            });
        }

        var samplesThatShouldBeReduced = measurements.value.length - measurementDetailPeriod;
        samplesThatShouldBeReduced -= reducedMeasurement;

        if (samplesThatShouldBeReduced > 60) // reduce 1 minute worth of data
        {
            // clamp to 60
            samplesThatShouldBeReduced = samplesThatShouldBeReduced > 60 ? 60 : samplesThatShouldBeReduced;

            var samples = measurements.value.splice(reducedMeasurement + 1, samplesThatShouldBeReduced);

            var sample = samples.reduce((s, c) => s + c.y, 0) / samples.length;
            measurements.value.splice(reducedMeasurement + 1, 0, {
                x: samples[0].x,
                y: sample
            });

            reducedMeasurement++;
        }
    });

    connection.on("currentSchedule", (s) => {
        if (s) {
            measurements.value.splice(0, measurements.value.length);
        }
        
        schedule.value = s;
        console.log("schedule received", s);
    });

    connection.on("runHistory", (s :KilnInfo[]) => {
        
        let lastSample = 0;
        let i = 0;
        
        let reduced = [] as Measurement[];

        while(lastSample < s.length)
        {
            i++;

            var first = s[lastSample];
            var current = s[i];

            if(!current)
                break;

            if((current.secondsElapsed! - first.secondsElapsed!) > 60)
            {
                let samplesToReduce = s.slice(lastSample, i);
                let value = samplesToReduce.reduce((s, c) => s + c.measuredTemp, 0) / samplesToReduce.length;
                reduced.push({x: first.secondsElapsed ?? 0, y: value});
                lastSample = i;
            }
        }

        measurements.value = reduced;
        console.log(`backlog received, ${s.length} received -> reduced to ${reduced.length}`, reduced);
        ready = true;
    });

    connection.onclose(async () => {
        connected.value = false;
        await start();
    });

    // Start the connection.
    start();
});



async function start() {
    if (connection == null) {
        setTimeout(start, 2000);
        return;
    }

    try {
        await connection.start();
        console.log("SignalR Connected.");
        connected.value = true;
    } catch (err) {
        console.log(err);
        setTimeout(start, 2000);
    }
}

const progressTimeStamp = computed(
    function progressTimeStamp() {
        return new Date((kilnInfo.value.secondsElapsed ?? 0) * 1000).toISOString().substring(11, 19);
    })

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
            <ScheduleChart :schedule="schedule" :measurements="measurements"></ScheduleChart>
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
