<script setup>
import { computed, ref } from 'vue'
import { motion, AnimatePresence } from 'motion-v'
import { enterRow, reducedMotion } from '../motion'
import AppIcon from './AppIcon.vue'
import TagBadges from './TagBadges.vue'
import PlayerAvatar from './PlayerAvatar.vue'
const props = defineProps({ players: { type: Array, default: () => [] }, settings: Object, selected: Object, recent: Boolean, readonly: Boolean, expandable: Boolean, presence: Boolean, showSearch: {type:Boolean,default:true} })
defineEmits(['select'])
const query = ref('')
const searchInput = ref(null)
defineExpose({focusSearch:()=>searchInput.value?.focus(),clearSearch:()=>{query.value=''}})
const filtered = computed(() => props.players.filter(p => (p.displayName + ' ' + (p.vrchatUserId || '')).toLocaleLowerCase().includes(query.value.toLocaleLowerCase())))
function isSelected(player) { return props.selected?.vrchatUserId ? props.selected.vrchatUserId===player.vrchatUserId : props.selected===player }
</script>
<template><div class="list-column">
<label v-show="showSearch" class="search"><AppIcon name="Search" :size="17"/><input ref="searchInput" v-model="query" aria-label="Search players" placeholder="Search players..."/><span v-if="query">{{ filtered.length }} found</span></label>
<div class="player-table" role="list" aria-label="Players">
<AnimatePresence><motion.div v-for="(p,index) in filtered" :key="p.vrchatUserId || p.displayName" class="player-entry" layout="position" :initial="{y:reducedMotion?0:12,scale:reducedMotion?1:.985,opacity:0}" :animate="{y:0,scale:1,opacity:1}" :exit="{x:reducedMotion?0:-14,opacity:0,transition:{duration:.12}}" :transition="enterRow(index)"><motion.button class="player-row" :while-press="reducedMotion||readonly?{}:{scale:.994}" :class="{ selected: isSelected(p) }" :aria-pressed="readonly||expandable?undefined:isSelected(p)" :aria-expanded="expandable?isSelected(p):undefined" :aria-controls="expandable?`row-actions-${index}`:undefined" :tabindex="readonly?-1:0" @click="!readonly&&$emit('select',p)">
<span class="player-identity"><span class="player-avatar-wrap"><PlayerAvatar :player="p"/><span v-if="presence" class="player-presence" title="In this instance"/></span><span class="player-name"><strong :title="p.displayName">{{ p.displayName }}</strong><small v-if="p.vrchatUserId" :title="p.vrchatUserId">{{ p.vrchatUserId }}</small><small v-else>Identity pending</small></span></span>
<TagBadges :tags="p.tags" :settings="settings"/>
<AppIcon v-if="expandable" name="ChevronDown" :size="13" :class="['expand-chevron',{expanded:isSelected(p)}]"/>
</motion.button><AnimatePresence><motion.div v-if="expandable&&isSelected(p)" :id="`row-actions-${index}`" class="player-row-actions" :initial="{height:reducedMotion?'auto':0,opacity:0}" :animate="{height:'auto',opacity:1}" :exit="{height:reducedMotion?'auto':0,opacity:0}" :transition="{type:'spring',stiffness:340,damping:30}"><slot name="actions" :player="p"/></motion.div></AnimatePresence></motion.div></AnimatePresence>
<div v-if="!filtered.length" class="empty-state"><AppIcon :name="query ? 'Search' : recent ? 'Clock3' : 'Users'" :size="24"/><h2>{{ query ? 'No matching players' : recent ? 'No recent players' : 'Waiting for your instance' }}</h2><p>{{ query ? 'Try another name or user ID.' : recent ? 'No local encounter history is available.' : 'Players appear here when VRChat reports activity.' }}</p></div>
</div></div></template>
<style>
.player-entry{min-width:0}.player-row-actions{overflow:hidden}.expand-chevron{color:var(--muted);transition:transform .24s cubic-bezier(.2,.8,.2,1)}.expand-chevron.expanded{transform:rotate(180deg)}
.player-avatar-wrap{position:relative;display:inline-flex;flex-shrink:0}.player-presence{position:absolute;width:9px;height:9px;border-radius:50%;background:#49c889;border:2px solid #19161e;right:-1px;bottom:0}
@media(prefers-reduced-motion:reduce){.expand-chevron{transition:none}}
</style>

