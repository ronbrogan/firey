import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

createApp(App).mount('#app');
