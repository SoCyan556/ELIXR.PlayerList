<script setup>
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { motion, AnimatePresence, MotionConfig } from 'motion-v'
import { spring, reducedMotion } from './motion'
import LoginScreen from './components/LoginScreen.vue'
import UpdatePrompt from './components/UpdatePrompt.vue'
import AppIcon from './components/AppIcon.vue'
import PlayerTable from './components/PlayerTable.vue'
import ProfilePanel from './components/ProfilePanel.vue'
import TagControls from './components/TagControls.vue'
import ToggleSwitch from './components/ToggleSwitch.vue'
import UserSearch from './components/UserSearch.vue'
import logo from './assets/elixr-logo.png'
import { categories } from './tags'
import { state, start, send, updateSetting } from './state'
const page = ref('Players'), selected = ref(null)
const nav = [{label:'Home',icon:'House'}, {label:'Players',icon:'Users'}, {label:'Search',icon:'Search'}, {label:'Recent',icon:'Clock3'}, {label:'Settings',icon:'Settings'}]
const title = computed(()=>({Players:'Player List',Recent:'Recent Players'}[page.value] || page.value))
const pageIcon = computed(()=>nav.find(n=>n.label===page.value).icon)
const players = computed(()=>page.value==='Recent' ? [] : state.players)
const connection = computed(()=>state.preview ? 'UI Preview · fictional players' : state.connected ? 'Connected to VRChat' : 'Waiting for VRChat')
function navigate(name) { page.value = name; selected.value = null }
function escape(e) { if(e.key==='Escape') selected.value=null }
watch(()=>state.profileRequest, request=>{
 if(!request||state.auth.status!=='SignedIn') return
 const player=state.players.find(p=>p.vrchatUserId===request.userId)
 if(player) { navigate('Players');selected.value=player }
})
watch(()=>state.players, list=>{
 if(selected.value&&page.value==='Players') {
  const key=selected.value.vrchatUserId||selected.value.displayName
  selected.value=list.find(p=>(p.vrchatUserId||p.displayName)===key)||null
 }
})
onMounted(()=>{ start(); window.addEventListener('keydown',escape) })
onBeforeUnmount(()=>window.removeEventListener('keydown',escape))
</script>
<template><MotionConfig reduced-motion="user"><LoginScreen v-if="state.auth.status!=='SignedIn'"/><div v-else class="app-shell" :style="{zoom:state.settings.uiScale,width:'calc(100vw / '+state.settings.uiScale+')',height:'calc(100dvh / '+state.settings.uiScale+')'}">
<aside class="sidebar"><div class="brand"><img :src="logo" alt="ELIXR logo"/><span>ELIXR</span></div>
<nav aria-label="Main navigation"><motion.button v-for="n in nav" :key="n.label" :class="{active:page===n.label}" :aria-current="page===n.label?'page':undefined" :while-press="reducedMotion?{}:{scale:.96}" :transition="spring" @click="navigate(n.label)"><motion.span v-if="page===n.label" class="nav-selection" layout-id="nav-selection" :transition="spring" aria-hidden="true"/><AppIcon :name="n.icon" :size="21"/><span class="nav-label">{{ n.label }}</span></motion.button></nav>
<div class="sidebar-footer">v{{state.updates.currentVersion}}</div></aside>
<div class="workspace"><header class="page-header"><div class="page-title"><AppIcon :name="pageIcon" :size="25"/><h1>{{ title }}</h1><span v-if="page==='Players'" class="player-count"><AppIcon name="Users" :size="14"/>{{ state.players.length }} players</span></div><div class="header-actions"><button class="desktop-overlay-button icon-button" aria-label="Desktop overlay" :aria-pressed="state.desktopOverlay.visible" :title="state.desktopOverlay.visible?'Hide desktop overlay':'Show always-on-top player list'" :disabled="!state.host||!state.capabilities.desktopOverlay" @click="send('desktopOverlay.visibility',{visible:!state.desktopOverlay.visible})"><AppIcon name="Monitor" :size="18"/></button><TagControls/></div></header>
<main>
<div v-if="state.auth.message" class="notice" role="status">{{ state.auth.message }}</div>
<div v-if="!state.host&&!state.preview" class="notice">Browser preview · Native services are available in the desktop app.</div><div v-if="state.notice" class="notice" role="alert">{{ state.notice }}</div>
<Transition name="page" mode="out-in"><section v-if="page==='Players'||page==='Recent'" :key="page" class="players-layout" :class="{'profile-open':selected}">
<PlayerTable :players="players" :settings="state.settings" :selected="selected" :recent="page==='Recent'" @select="selected=$event"/>
<AnimatePresence><motion.div v-if="selected" class="profile-slot" :initial="{x:reducedMotion?0:28,scale:reducedMotion?1:.97,opacity:0}" :animate="{x:0,scale:1,opacity:1}" :exit="{x:reducedMotion?0:24,opacity:0}" :transition="spring"><ProfilePanel :player="selected" :settings="state.settings" :instance="state.instance" @close="selected=null"/></motion.div></AnimatePresence>
</section>
<UserSearch v-else-if="page==='Search'" key="Search"/>
<section v-else-if="page==='Home'" key="Home" class="home-page">
<div class="section-panel home-intro"><h2>Welcome to ELIXR</h2><p class="secondary">Stay safe. Stay aware.</p><dl class="connection-list"><div><dt>VRChat</dt><dd>{{ connection }}</dd></div><div><dt>Instance</dt><dd>{{ state.instance?.worldName || 'Not available' }}</dd></div><div><dt>Account</dt><dd>{{ state.capabilities.authentication?'Connected':'Not signed in' }}</dd></div></dl><div class="home-actions"><button @click="navigate('Players')"><AppIcon name="Users" :size="17"/>Open Player List</button><button :disabled="!state.capabilities.overlay" @click="send('overlay.visibility',{visible:true})"><AppIcon name="Eye" :size="17"/>Show VR Overlay</button></div></div>
</section>
<section v-else key="Settings" class="settings-page"><div class="settings-columns"><div>
<div class="section-panel account-settings"><h2>VRChat account</h2><p class="help">{{ state.auth.user?.displayName || 'Signed in' }}</p><button class="sign-out" @click="send('auth.logout')">Sign out</button></div>
<div class="section-panel setting-list"><h2>Tag Display</h2><ToggleSwitch v-for="c in categories" :key="c.key" :label="'Show '+c.label+' Tags'" :icon="c.icon" :category="c.className" :model-value="state.settings[c.setting]" @update:model-value="updateSetting(c.setting,$event)"/><ToggleSwitch label="Compact Tag Icons" icon="LayoutGrid" :model-value="state.settings.compactTags" @update:model-value="updateSetting('compactTags',$event)"/></div>
<div class="section-panel"><h2>Appearance</h2><label class="slider-label">UI scale<output>{{ Math.round(state.settings.uiScale*100) }}%</output></label><input aria-label="UI scale" type="range" min=".85" max="1.3" step=".05" :value="state.settings.uiScale" @input="updateSetting('uiScale',Number($event.target.value))"/></div>
<div class="section-panel"><h2>Player List</h2><p class="help">Player activity is read from local VRChat logs.</p></div>
<div class="section-panel"><h2>Tag sources</h2><p class="help">FewTags · ETags · etags-groups. Public feeds refresh every 10 minutes. Group tags match public VRChat memberships.</p><p v-for="feed in state.tagFeeds || []" :key="feed.name" class="help" :title="feed.url">{{ feed.name }}: {{ feed.state }} · {{ feed.records.toLocaleString() }} records</p><p class="help">Community tags are third-party labels, not verified facts or app permissions.</p></div>
</div><div>
<div class="section-panel setting-list"><h2>VR Overlay</h2><ToggleSwitch label="Show in VR" icon="Eye" :disabled="!state.capabilities.overlay" :model-value="state.capabilities.overlay&&state.settings.overlayVisible" @update:model-value="send('overlay.visibility',{visible:$event})"/><p class="help" v-if="!state.capabilities.overlay">SteamVR integration is not available in this build.</p></div>
<div class="section-panel setting-list"><h2>Desktop overlay</h2><ToggleSwitch label="Always-on-top player list" icon="Monitor" :disabled="!state.host||!state.capabilities.desktopOverlay" :model-value="state.desktopOverlay.visible" @update:model-value="send('desktopOverlay.visibility',{visible:$event})"/><p class="help">A separate movable, resizable player list. Stays visible when the main window is minimized. Closing it turns this toggle off. Independent of the VR overlay.</p></div>
<div class="section-panel setting-list"><h2>Updates</h2><p class="help">Version {{state.updates.currentVersion}}. {{state.updates.message}}</p><ToggleSwitch label="Check on startup" :model-value="state.settings.checkUpdatesOnStartup" @update:model-value="updateSetting('checkUpdatesOnStartup',$event)"/><button :disabled="!state.host||state.updates.busy||!state.updates.enabled" @click="send('updates.check')">{{state.updates.busy?'Checking…':'Check for updates'}}</button></div>
<div class="section-panel"><h2>Privacy</h2><p class="help">Settings and encrypted saved sessions stay on your computer. VRChat handles authentication. Public tag feeds and update checks contact GitHub. After your first successful sign-in, your own VRChat user ID is recorded once by ELIXR to count unique Player List users. No password, session cookies, instance rosters, or ongoing activity are sent to ELIXR.</p></div>
<div class="section-panel"><h2>AI assistance</h2><p class="help">This app was created with the assistance of AI. We fully understand the concerns people have about its use.</p></div>
</div></div></section></Transition>
</main></div></div><UpdatePrompt/></MotionConfig></template>

