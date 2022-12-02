<script setup lang="ts">
import { LineChart } from "vue-chart-3";
import { KilnSchedule, Measurement } from "../App.vue";
import { ref, watch, computed } from "vue";

export interface Props {
    measurements: Measurement[];
    schedule: KilnSchedule | null;
}

const props = withDefaults(defineProps<Props>(), {
    measurements: () => [],
    schedule: () => <KilnSchedule>{}
});

const chartConfig = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    scales: {
        x: {
            type: "linear",
            beginAtZero: true,
            title: {
                display: true
            },
            ticks: {
                callback: function (value: any, index: any, ticks: any) {
                    return new Date(value * 1000).toISOString().substr(11, 8);;
                }
            }
        },
        y: {
            type: "linear",
            title: {
                display: true
            }
        },
    },
    elements: {
        point: {
            pointStyle: "circle",
            radius: 1.2
        }
    },
    plugins: {
        zoom: {
            pan: {
                enabled: true,
            },
            zoom: {
                wheel: {
                    enabled: true,
                },
                pinch: {
                    enabled: true
                },
                mode: 'xy',
            },
            limits: {
                y: { min: 0, max: 3200 },
                x: { min: 0 }
            }
        }
    }
};

const scheduleData = computed(buildScheduleData);
function buildScheduleData() {
    console.log("building schedule");
    let scheduleData: Measurement[] = [];

    if (props.schedule?.ramps) {
        let ramps = props.schedule.ramps.sort(r => r.order);
        var currentTime = 0;
        var currentTemp = props.schedule.defaultStartTemp;

        scheduleData.push({ x: currentTime * 60, y: currentTemp });
        for (let ramp of ramps) {

            currentTime += ramp.calculatedTimeMinutes;
            currentTemp = ramp.targetTemp;

            scheduleData.push({ x: currentTime * 60, y: currentTemp });

            if (ramp.holdMinutes > 0) {
                currentTime += ramp.holdMinutes;
                scheduleData.push({ x: currentTime * 60, y: currentTemp });
            }
        }
    }

    console.log(scheduleData);
    return scheduleData;
}

const chartDatas = computed(chartDataGetter);
function chartDataGetter() {
    return {
        datasets: [
            {
                label: "Schedule",
                data: scheduleData.value,
                fill: false,
                borderColor: "rgb(255, 0, 0)",
                borderWidth: 2,
                tension: 0.0,
            },
            {
                label: "Measurements",
                data: props.measurements,
                fill: false,
                borderColor: "rgb(255, 255, 255)",
                borderWidth: 2,
                tension: 0.0,
            },
        ],
    };
}
</script>

<template>
    <LineChart v-if="(scheduleData.length > 0)" :chartData="chartDatas" :options="chartConfig" style="height: 100%"></LineChart>
</template>