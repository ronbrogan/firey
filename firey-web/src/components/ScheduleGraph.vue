<script lang="ts">
import { Vue, Options, Prop, Watch } from "vue-decorator";
import { LineChart } from "vue-chart-3";
import { KilnSchedule, Measurement } from "../App.vue";
import { ref } from "vue";

@Options({
    components: {
        LineChart,
    }
})
export default class ScheduleGraph extends Vue {
    @Prop()
    public measurements: Measurement[] | null = (this.$props as any)["measurements"];

    @Prop()
    public schedule: KilnSchedule | null = (this.$props as any)["schedule"];


    public chartConfig = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            x: {
                type: "linear",
                //time: {
                //  unit: "year",
                //},
            },
            y: {
                type: "linear",
            },
        }
    };

    @Watch("schedule")
    public onScheduleChange(val: any, old: any) {
        console.log("schedule updated", val, old)
    }

    @Watch("measurements")
    public onMeasurementsChange(val: any, old: any) {
        console.log("measurements updated", val, old)
    }


    get scheduleData() {
        var scheduleData: {x: number, y: number}[] = [];

        if(this.schedule?.ramps) {
            let ramps = this.schedule.ramps.sort(r => r.order);
            var currentTime = 0;
            var currentTemp = 0;

            for(let ramp of ramps) {
                scheduleData.push({x: currentTime, y: currentTemp});
                
                currentTime += ramp.calculatedTimeMinutes;
                currentTemp = ramp.targetTemp;

                scheduleData.push({x: currentTime, y: currentTemp});

                if(ramp.holdMinutes > 0) {
                    currentTime += ramp.holdMinutes;
                    scheduleData.push({x: currentTime, y: currentTemp});
                }
            }
        }

        return scheduleData;
    }

    get chartData(){
        return {
                datasets: [
                {
                    label: "Schedule",
                    data: this.scheduleData,
                    fill: false,
                    borderColor: "rgb(255, 255, 255)",
                    borderWidth: 2,
                    tension: 0.0,
                },
                {
                    label: "Measurements",
                    data: this.measurements,
                    fill: false,
                    borderColor: "rgb(255, 255, 255)",
                    borderWidth: 2,
                    tension: 0.0,
                },
            ],
        };
    }
}
</script>

<template>
    <LineChart :chartData="chartData" :options="chartConfig"></LineChart>

function Watch(arg0: string) {
  throw new Error("Function not implemented.");
}
</template>