<script setup>
import { House, Users, Clock3, Settings, Skull, Smile, Search, ChevronRight, ChevronDown, ArrowRight, Shield, Eye, EyeOff, Monitor, X, Copy, ExternalLink, LayoutGrid, SlidersHorizontal, MessageSquare, Ellipsis, UserRound, Maximize2, Download } from '@lucide/vue'
defineProps({ name: { type: String, default: 'Users' }, size: { type: Number, default: 18 } })
const icons = { House, Users, Clock3, Settings, Skull, Smile, Search, ChevronRight, ChevronDown, ArrowRight, Shield, Eye, EyeOff, Monitor, X, Copy, ExternalLink, LayoutGrid, SlidersHorizontal, MessageSquare, Ellipsis, UserRound, Maximize2, Download }
</script>
<template><component :is="icons[name] || UserRound" :size="size" :stroke-width="1.8" aria-hidden="true"/></template>

