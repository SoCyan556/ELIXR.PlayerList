<script setup>
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { animationFor, textSegments } from '../tagStyle'
const props = defineProps({ tag: { type: Object, required: true } })
const root = ref(null), visible = ref(false)
const segments = computed(() => textSegments(props.tag))
const animation = computed(() => animationFor(props.tag))
const style = computed(() => ({ '--tag-duration': `${2 / Math.max(.05, Math.min(5, Number(props.tag.speed) || 1))}s`, '--tag-shift': `${Math.min(2, Math.max(.1, Number(props.tag.amplitude) || 1))}px` }))
let observer
onMounted(() => {
  observer = new IntersectionObserver(entries => { visible.value = entries[0]?.isIntersecting || false })
  observer.observe(root.value)
})
onUnmounted(() => observer?.disconnect())
</script>
<template><span ref="root" :class="['tag-text', animation && `tag-effect-${animation}`, { 'tag-offscreen': !visible }]" :style="style"><span v-for="(segment,index) in segments" :key="index" :style="{ color: segment.color || 'inherit', '--char-delay': `${-index * .065}s` }">{{ segment.text }}</span></span></template>
<style>
.tag-text{display:inline-block;white-space:pre-wrap;max-width:100%}
.tag .tag-text{color:inherit}
.tag-text>span{color:inherit}
.tag-effect-pulse{animation:tag-pulse var(--tag-duration) ease-in-out infinite}
.tag-effect-blink{animation:tag-blink var(--tag-duration) ease-in-out infinite}
.tag-effect-glitch{animation:tag-glitch var(--tag-duration) steps(1,end) infinite}
.tag-effect-shake{animation:tag-shake var(--tag-duration) linear infinite}
.tag-effect-rain>span{animation:tag-rain calc(var(--tag-duration) * 2) linear infinite;animation-delay:var(--char-delay)}
.tag-offscreen,.tag-offscreen>span{animation-play-state:paused!important}
.app-hidden .tag-text,.app-hidden .tag-text>span{animation-play-state:paused!important}
@keyframes tag-pulse{0%,100%{filter:brightness(.88)}50%{filter:brightness(1.22)}}
@keyframes tag-blink{0%,100%{opacity:.52}50%{opacity:1}}
@keyframes tag-rain{to{filter:hue-rotate(360deg)}}
@keyframes tag-glitch{0%,84%,100%{filter:none;transform:none}85%,89%{filter:hue-rotate(35deg);transform:translateX(var(--tag-shift))}87%,91%{filter:hue-rotate(-30deg);transform:translateX(calc(-1 * var(--tag-shift)))}}
@keyframes tag-shake{0%,70%,100%{transform:none}75%,85%,95%{transform:translateX(var(--tag-shift))}80%,90%{transform:translateX(calc(-1 * var(--tag-shift)))}}
@media(prefers-reduced-motion:reduce){.tag-text,.tag-text>span{animation:none!important;transform:none!important;filter:none!important}}
</style>
