<script setup>
import { computed, nextTick, onMounted, onBeforeUnmount, ref, watch } from 'vue'
import { AnimatePresence, motion, MotionConfig } from 'motion-v'
import AppIcon from './AppIcon.vue'
import logo from '../assets/elixr-logo.png'
import PlayerTable from './PlayerTable.vue'
import PlayerAvatar from './PlayerAvatar.vue'
import TagBadges from './TagBadges.vue'
import ToggleSwitch from './ToggleSwitch.vue'
import { state, start, send, updateSetting } from '../state'
import { validUserId } from '../profileActions'
import { categories } from '../tags'
import { spring, reducedMotion } from '../motion'
const viewport = ref(innerWidth), searchOpen = ref(false), settingsOpen = ref(false), table = ref(null)
function resize(){viewport.value=innerWidth}
onMounted(()=>{start();window.addEventListener('resize',resize)})
onBeforeUnmount(()=>window.removeEventListener('resize',resize))
const compact = computed(()=>viewport.value/state.settings.uiScale<340)
const displaySettings = computed(()=>({...state.settings,compactTags:compact.value||state.settings.compactTags}))
function changeLayout(value){send('desktopOverlay.layout',{compact:value,scale:state.settings.uiScale})}
async function toggleSearch(){searchOpen.value=!searchOpen.value;settingsOpen.value=false;await nextTick();if(searchOpen.value)table.value?.focusSearch();else table.value?.clearSearch()}
function escape(){if(settingsOpen.value)settingsOpen.value=false;else if(selectedKey.value)selectedKey.value=null;else if(searchOpen.value){searchOpen.value=false;table.value?.clearSearch()}}
const selectedKey = ref(null)
const selected = computed(() => state.players.find(p => (p.vrchatUserId||p.displayName)===selectedKey.value))
function select(player) { const key=player.vrchatUserId||player.displayName; selectedKey.value=selectedKey.value===key?null:key; settingsOpen.value=false;state.notice='' }
function validId(player) { return validUserId(player.vrchatUserId) }
watch(() => state.players, () => { if(!selected.value) selectedKey.value=null })
const copied = computed(() => selected.value&&state.profileAction?.type==='copied'&&state.profileAction.userId===selected.value.vrchatUserId)
</script>
<template><MotionConfig reduced-motion="user"><section :class="['desktop-overlay',{'overlay-compact':compact}]" aria-label="Desktop player list overlay" :style="{zoom:state.settings.uiScale,width:`calc(100vw / ${state.settings.uiScale})`,height:`calc(100dvh / ${state.settings.uiScale})`}" @keydown.esc="escape">
<header class="overlay-header"><div class="overlay-brand"><img class="overlay-logo" :src="logo" alt="ELIXR logo" draggable="false"/><h1>ELIXR</h1></div><span class="overlay-player-count"><AppIcon name="Users" :size="15"/><span class="overlay-count-label">Players</span>{{state.players.length}}</span><div class="overlay-header-buttons"><button class="icon-button" title="Resize and position overlay" aria-label="Resize overlay" @click="settingsOpen=false;send('desktopOverlay.resize')"><AppIcon name="Maximize2" :size="17"/></button><button class="icon-button" title="Search players" aria-label="Search players" :aria-expanded="searchOpen" @click="toggleSearch"><AppIcon name="Search" :size="18"/></button><button class="icon-button" title="Overlay settings" aria-label="Overlay settings" :aria-expanded="settingsOpen" @click="settingsOpen=!settingsOpen"><AppIcon name="Settings" :size="18"/></button><button class="icon-button" title="Hide desktop overlay" aria-label="Hide desktop overlay" @click="send('desktopOverlay.close')"><AppIcon name="X" :size="17"/></button></div></header>
<AnimatePresence><motion.div v-if="settingsOpen" class="overlay-settings" role="dialog" aria-label="Overlay settings" :initial="{y:reducedMotion?0:-9,scale:reducedMotion?1:.95,opacity:0}" :animate="{y:0,scale:1,opacity:1}" :exit="{y:reducedMotion?0:-6,opacity:0}" :transition="spring"><div class="overlay-settings-title">Overlay<button class="icon-button" aria-label="Close overlay settings" @click="settingsOpen=false"><AppIcon name="X" :size="15"/></button></div><ToggleSwitch label="Compact layout" icon="LayoutGrid" :model-value="compact" @update:model-value="changeLayout"/><ToggleSwitch v-for="c in categories" :key="c.key" :label="c.label" :icon="c.icon" :category="c.className" :model-value="state.settings[c.setting]" @update:model-value="updateSetting(c.setting,$event)"/><ToggleSwitch label="Compact Tags" :disabled="compact" :model-value="displaySettings.compactTags" @update:model-value="updateSetting('compactTags',$event)"/><p>Drag the header to move. Drag any edge to resize.</p></motion.div></AnimatePresence>
<p v-if="state.notice" class="overlay-action-notice" role="alert">{{ state.notice }}</p>
<PlayerTable v-if="state.auth.status==='SignedIn'" ref="table" :players="state.players" :settings="displaySettings" :selected="selected" :presence="state.connected||state.preview" :show-search="searchOpen" expandable @select="select"><template #actions="{player}"><div class="overlay-player-actions"><div class="overlay-action-person"><PlayerAvatar :player="player"/><div><strong>{{player.displayName}}</strong><span>{{state.connected||state.preview?'In VRChat':'In this instance'}}</span></div></div><TagBadges v-if="player.tags?.length" :tags="player.tags" :settings="{...state.settings,compactTags:false}"/><code :title="player.vrchatUserId">{{ player.vrchatUserId||'Identity pending' }}</code><div class="overlay-action-menu"><button :disabled="!validId(player)||!state.host" @click="send('profile.copyId',{userId:player.vrchatUserId})"><AppIcon name="Copy" :size="18"/>Copy User ID</button><button :disabled="!validId(player)||!state.host" @click="send('profile.openInApp',{userId:player.vrchatUserId})"><AppIcon name="UserRound" :size="18"/>Open in app<AppIcon name="ArrowRight" :size="14"/></button></div><span v-if="copied" class="overlay-copy-notice" role="status">User ID copied</span><span v-else-if="!validId(player)" class="overlay-copy-notice">Waiting for a verified user ID.</span></div></template></PlayerTable>
<p v-else class="help">Sign in through the main ELIXR window.</p>
<footer title="Always on top · Drag header to move; drag edges to resize"><AppIcon name="Monitor" :size="12"/>Always on top<span>v{{state.updates.currentVersion}}</span></footer>
</section></MotionConfig></template>
<style>
.desktop-overlay{display:flex;flex-direction:column;min-height:0;overflow:hidden;background:var(--content);padding:0 10px 8px;width:100%}
.overlay-header{height:46px;min-height:46px;display:flex;justify-content:space-between;align-items:center;gap:10px}
.overlay-header>div{display:flex;align-items:center;gap:9px;min-width:0}.overlay-header h1{font-size:15px}.overlay-header .secondary{font-size:12px}
.overlay-tools{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:8px;margin-bottom:10px;min-height:30px}
.overlay-status{font-size:10px;color:var(--secondary);white-space:nowrap}.overlay-tools .toggle-control{height:28px;font-size:10px;gap:8px;padding:0 6px}
.desktop-overlay .list-column{flex:1;min-height:0}.desktop-overlay .search{height:32px;min-height:32px;margin-bottom:8px}.desktop-overlay .search input{font-size:12px}
.desktop-overlay .player-row{grid-template-columns:minmax(100px,.85fr) minmax(0,1.15fr) 13px;min-height:48px!important;height:auto!important;padding:7px 6px;gap:6px;cursor:pointer}
.overlay-player-actions{padding:10px;background:#1c1c1c;border-bottom:1px solid var(--border);display:flex;flex-direction:column;gap:9px}
.overlay-player-actions code{font:10px 'Consolas',monospace;color:var(--secondary);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.overlay-player-actions>div{display:flex;flex-wrap:wrap;gap:6px}.overlay-player-actions button{display:flex;align-items:center;gap:6px;font-size:11px;padding:7px 9px}.overlay-player-actions button:hover:not(:disabled){background:var(--hover)}
.overlay-copy-notice,.overlay-action-notice{font-size:10px;color:var(--secondary)}.overlay-action-notice{padding:0 0 8px}
.desktop-overlay .player-name strong{font-size:12px}.desktop-overlay .player-name small{display:none}.desktop-overlay .avatar{width:30px;height:30px;min-width:30px}.desktop-overlay .player-identity{gap:8px}
.desktop-overlay .tags{gap:4px}.desktop-overlay .tag{font-size:10px;padding:0 5px;height:22px}.desktop-overlay .tag.compact{padding:0 2px}.desktop-overlay .player-table{border:1px solid var(--border)}
.desktop-overlay footer{min-height:26px;display:flex;align-items:center;gap:6px;font-size:9px;color:var(--muted);padding-top:6px}.desktop-overlay footer span{margin-left:auto;text-align:right;max-width:150px}
</style>
<style src="../overlay.css"/>
