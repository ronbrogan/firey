import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { Chart, registerables } from 'chart.js';
import zoomPlugin from 'chartjs-plugin-zoom';

Chart.register(...registerables)
Chart.register(zoomPlugin);
Chart.defaults.color  = "#fff";
Chart.defaults.backgroundColor  = "rgba(255, 255, 255, 0.1)";
Chart.defaults.borderColor  = "rgba(255, 255, 255, 0.1)";

createApp(App).mount('#app');
