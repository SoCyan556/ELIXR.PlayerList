<script setup>
import { computed } from 'vue'
import { tagTokens, categoryFor } from '../tags'
import AppIcon from './AppIcon.vue'
import TagText from './TagText.vue'
import { motion, AnimatePresence } from 'motion-v'
import { spring, reducedMotion } from '../motion'
const props = defineProps({ tags: { type: Array, default: () => [] }, settings: { type: Object, required: true } })
const tokens = computed(() => tagTokens(props.tags, props.settings))
</script>
<template><span class="tags"><AnimatePresence><motion.span v-for="(tag,index) in tokens" :key="tag.category + tag.name + index" :class="['tag', categoryFor(tag)?.className, { compact: settings.compactTags }]" :title="tag.name + (tag.source ? ' · '+tag.source : '')" :initial="{scale:reducedMotion?1:.8,y:reducedMotion?0:3,opacity:0}" :animate="{scale:1,y:0,opacity:1}" :exit="{scale:reducedMotion?1:.9,opacity:0,transition:{duration:.1}}" :transition="spring"><AppIcon :name="categoryFor(tag)?.icon" :size="settings.compactTags ? 19 : 13"/><TagText v-if="!settings.compactTags" :tag="tag"/></motion.span></AnimatePresence></span></template>

