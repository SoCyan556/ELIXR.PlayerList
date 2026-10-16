<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { motion, AnimatePresence } from 'motion-v'
import { spring, reducedMotion } from '../motion'
import { state, updateSetting } from '../state'
import { categories } from '../tags'
import ToggleSwitch from './ToggleSwitch.vue'
import AppIcon from './AppIcon.vue'
const open = ref(false), narrow = ref(innerWidth<=1100), root = ref(null)
function resize() { narrow.value=innerWidth<=1100 }
function dismiss(e) { if(e.key==='Escape'||(e.type==='pointerdown'&&!root.value?.contains(e.target))) open.value=false }
onMounted(()=>{window.addEventListener('resize',resize);document.addEventListener('pointerdown',dismiss);document.addEventListener('keydown',dismiss)})
onBeforeUnmount(()=>{window.removeEventListener('resize',resize);document.removeEventListener('pointerdown',dismiss);document.removeEventListener('keydown',dismiss)})
</script>
<template><div ref="root" class="tag-controls"><button class="filter-menu" :aria-expanded="open" @click="open=!open"><AppIcon name="SlidersHorizontal" :size="16"/>Filters<AppIcon name="ChevronDown" :size="14"/></button><AnimatePresence><motion.div v-if="!narrow||open" class="filter-controls" :class="{open}" :initial="narrow?{y:reducedMotion?0:-8,scale:reducedMotion?1:.94,opacity:0}:false" :animate="{y:0,scale:1,opacity:1}" :exit="{y:reducedMotion?0:-5,scale:reducedMotion?1:.97,opacity:0,transition:{duration:.14}}" :transition="spring"><ToggleSwitch v-for="c in categories" :key="c.key" :label="c.label" :icon="c.icon" :category="c.className" :model-value="state.settings[c.setting]" @update:model-value="updateSetting(c.setting,$event)"/><ToggleSwitch label="Compact Tags" :model-value="state.settings.compactTags" @update:model-value="updateSetting('compactTags',$event)"/></motion.div></AnimatePresence></div></template>

