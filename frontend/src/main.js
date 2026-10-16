import { createApp } from 'vue'
import App from './App.vue'
import DesktopOverlay from './components/DesktopOverlay.vue'
import './style.css'
import './theme.css'
function pauseHiddenTagEffects() { document.documentElement.classList.toggle('app-hidden', document.hidden) }
document.addEventListener('visibilitychange', pauseHiddenTagEffects)
pauseHiddenTagEffects()
const overlay = new URLSearchParams(location.search).get('surface') === 'desktop-overlay'
createApp(overlay ? DesktopOverlay : App).mount('#app')

