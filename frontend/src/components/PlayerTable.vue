<script setup>
import { computed, ref } from 'vue'
import { motion, AnimatePresence } from 'motion-v'
import { enterRow, reducedMotion } from '../motion'
import AppIcon from './AppIcon.vue'
import TagBadges from './TagBadges.vue'
import PlayerAvatar from './PlayerAvatar.vue'
import { orderPlayers, playerGroup } from '../playerOrder'
import { state } from '../state'
const props = defineProps({ players: { type: Array, default: () => [] }, settings: Object, selected: Object, recent: Boolean, readonly: Boolean, expandable: Boolean, presence: Boolean, showSearch: {type:Boolean,default:true} })
defineEmits(['select'])
const query = ref('')
const searchInput = ref(null)
defineExpose({focusSearch:()=>searchInput.value?.focus(),clearSearch:()=>{query.value=''}})
const selfId = computed(()=>state.auth.user?.vrchatUserId)
const filtered = computed(() => {
 const matches=props.players.filter(p => (p.displayName + ' ' + (p.vrchatUserId || '')).toLocaleLowerCase().includes(query.value.toLocaleLowerCase()))
 return props.recent ? matches : orderPlayers(matches,selfId.value)
})
const counts=computed(()=>filtered.value.reduce((result,p)=>{const group=playerGroup(p,selfId.value);result[group]=(result[group]||0)+1;return result},{}))
function sectionStart(index) { return !props.recent&&(index===0||playerGroup(filtered.value[index],selfId.value)!==playerGroup(filtered.value[index-1],selfId.value)) }
function isSelected(player) { return props.selected?.vrchatUserId ? props.selected.vrchatUserId===player.vrchatUserId : props.selected===player }
function lastSeen(value) { const minutes=Math.max(0,Math.floor((Date.now()-new Date(value).getTime())/60000));return minutes<1?'Just now':minutes<60?`${minutes}m ago`:minutes<1440?`${Math.floor(minutes/60)}h ago`:`${Math.floor(minutes/1440)}d ago` }
</script>
<template><div class="list-column">
<label v-show="showSearch" class="search"><AppIcon name="Search" :size="17"/><input ref="searchInput" v-model="query" aria-label="Search players" placeholder="Search players..."/><span v-if="query">{{ filtered.length }} found</span></label>
<div class="player-table" role="list" aria-label="Players">
<AnimatePresence><motion.div v-for="(p,index) in filtered" :key="p.vrchatUserId || p.displayName" class="player-entry" layout="position" :initial="{y:reducedMotion?0:12,scale:reducedMotion?1:.985,opacity:0}" :animate="{y:0,scale:1,opacity:1}" :exit="{x:reducedMotion?0:-14,opacity:0,transition:{duration:.12}}" :transition="enterRow(index)"><div v-if="sectionStart(index)" class="player-section" role="heading" aria-level="2"><span>{{playerGroup(p,selfId)}}</span><span>{{counts[playerGroup(p,selfId)]}}</span></div><motion.button class="player-row" :while-press="reducedMotion||readonly?{}:{scale:.994}" :class="{ selected: isSelected(p) }" :aria-pressed="readonly||expandable?undefined:isSelected(p)" :aria-expanded="expandable?isSelected(p):undefined" :aria-controls="expandable?`row-actions-${index}`:undefined" :tabindex="readonly?-1:0" @click="!readonly&&$emit('select',p)">
<span class="player-identity"><span class="player-avatar-wrap"><PlayerAvatar :player="p"/><span v-if="presence" class="player-presence" title="In this instance"/></span><span class="player-name"><strong :title="p.displayName">{{ p.displayName }}</strong><small v-if="p.vrchatUserId" :title="p.vrchatUserId">{{ p.vrchatUserId }}</small><small v-else>Identity pending</small></span></span>
<TagBadges :tags="p.tags" :settings="settings"/>
<span v-if="recent&&p.lastSeen" class="recent-seen" :title="new Date(p.lastSeen).toLocaleString()">{{lastSeen(p.lastSeen)}}</span>
<AppIcon v-if="expandable" name="ChevronDown" :size="13" :class="['expand-chevron',{expanded:isSelected(p)}]"/>
</motion.button><AnimatePresence><motion.div v-if="expandable&&isSelected(p)" :id="`row-actions-${index}`" class="player-row-actions" :initial="{height:reducedMotion?'auto':0,opacity:0}" :animate="{height:'auto',opacity:1}" :exit="{height:reducedMotion?'auto':0,opacity:0}" :transition="{type:'spring',stiffness:340,damping:30}"><slot name="actions" :player="p"/></motion.div></AnimatePresence></motion.div></AnimatePresence>
<div v-if="!filtered.length" class="empty-state"><AppIcon :name="query ? 'Search' : recent ? 'Clock3' : 'Users'" :size="24"/><h2>{{ query ? 'No matching players' : recent ? 'No recent players' : 'Waiting for your instance' }}</h2><p>{{ query ? 'Try another name or user ID.' : recent ? 'No local encounter history is available.' : 'Players appear here when VRChat reports activity.' }}</p></div>
</div></div></template>
<style>
.player-entry{min-width:0}.player-row-actions{overflow:hidden}.expand-chevron{color:var(--muted);transition:transform .24s cubic-bezier(.2,.8,.2,1)}.expand-chevron.expanded{transform:rotate(180deg)}
.player-section{display:flex;align-items:center;gap:9px;padding:13px 12px 7px;color:var(--secondary);font-size:10px;letter-spacing:.03em;border-top:1px solid var(--border);background:var(--background)}.player-section>span:last-child{color:var(--muted);font-variant-numeric:tabular-nums}.player-entry:first-child .player-section{border-top:0;padding-top:8px}.desktop-overlay .player-section{padding:9px 9px 5px;font-size:9px}
.player-avatar-wrap{position:relative;display:inline-flex;flex-shrink:0}.player-presence{position:absolute;width:9px;height:9px;border-radius:50%;background:#49c889;border:2px solid #19161e;right:-1px;bottom:0}
.recent-seen{font-size:10px;color:var(--secondary);text-align:right;white-space:nowrap}.player-row:has(.recent-seen){grid-template-columns:minmax(120px,1fr) minmax(90px,1fr) 64px}.recent-history-help{margin:0 0 10px!important}.recent-history-help+.players-layout{height:calc(100% - 35px)}
@media(prefers-reduced-motion:reduce){.expand-chevron{transition:none}}
</style>

