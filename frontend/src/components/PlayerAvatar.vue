<script setup>
import { ref, watch } from 'vue'
import AppIcon from './AppIcon.vue'
const props = defineProps({ player: Object, large: Boolean })
const broken = ref(false)
watch(() => [props.player?.vrchatUserId,props.player?.avatarUrl], () => broken.value = false)
</script>
<template><span class="avatar" :class="{ large }"><img v-if="player.avatarUrl && !broken" :src="player.avatarUrl" alt="" loading="lazy" decoding="async" draggable="false" referrerpolicy="no-referrer" @error="broken = true"/><AppIcon v-else name="UserRound" :size="large ? 32 : 21"/></span></template>

