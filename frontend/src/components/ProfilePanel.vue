<script setup>
import { computed, ref, watch } from 'vue'
import AppIcon from './AppIcon.vue'
import PlayerAvatar from './PlayerAvatar.vue'
import TagText from './TagText.vue'
import { categories, visibleTags, categoryFor } from '../tags'
import { send, state } from '../state'
import { validUserId } from '../profileActions'
const props = defineProps({ player: Object, settings: Object, instance: Object })
defineEmits(['close'])
const notice = ref('')
watch(() => props.player?.vrchatUserId, () => notice.value = '')
watch(() => state.profileAction, action => {
 if(action?.type==='groupCopied'&&action.userId===props.player.vrchatUserId) notice.value='Group code copied'
})
const tags = computed(() => visibleTags(props.player.tags, props.settings).slice().sort((a,b) => categories.findIndex(c=>c.key===a.category)-categories.findIndex(c=>c.key===b.category)))
const validId = computed(() => validUserId(props.player.vrchatUserId))
async function copy() {
 if (!props.player.vrchatUserId) return
 try {
  if (state.host && !state.preview) send('profile.copyId', { userId: props.player.vrchatUserId })
  else await navigator.clipboard.writeText(props.player.vrchatUserId)
  notice.value = 'User ID copied'
 } catch { notice.value = 'Clipboard unavailable' }
}
async function copyGroupCode(tag) {
 if(!tag.groupCode) return
 if(state.host&&!state.preview) { send('group.copyCode',{userId:props.player.vrchatUserId,groupId:tag.groupId});return }
 try { await navigator.clipboard.writeText(tag.groupCode);notice.value='Group code copied' }
 catch { notice.value='Clipboard unavailable' }
}
</script>
<template><aside class="profile-panel" aria-label="Player profile" @keydown.esc="$emit('close')">
<button class="icon-button close-profile" aria-label="Close profile" @click="$emit('close')"><AppIcon name="X" :size="18"/></button>
<div class="profile-header"><PlayerAvatar :player="player" large/><div class="profile-name"><h2 :title="player.displayName">{{ player.displayName }}</h2><div class="profile-id"><span :title="player.vrchatUserId">{{ player.vrchatUserId || 'Identity pending' }}</span><button class="icon-button" aria-label="Copy user ID" :disabled="!player.vrchatUserId" @click="copy"><AppIcon name="Copy" :size="15"/></button></div></div></div>
<div class="profile-actions"><button class="profile-link" :disabled="!validId || !state.host || state.preview" @click="send('profile.open',{userId:player.vrchatUserId})">Open VRChat Profile<AppIcon name="ExternalLink" :size="14"/></button></div>
<section class="profile-tags"><h3>Tags</h3><div v-for="(tag,index) in tags" :key="tag.category + index" class="profile-tag" :title="tag.source"><span :class="['tag-symbol',categoryFor(tag)?.className]"><AppIcon :name="categoryFor(tag)?.icon" :size="21"/></span><span class="profile-tag-content"><strong><TagText :tag="tag"/></strong><small :class="categoryFor(tag)?.className">{{ categoryFor(tag)?.label }}</small><template v-if="tag.category==='Group'&&tag.groupId"><span class="profile-group-name">{{ tag.groupName||'Group name unavailable' }}</span><span class="profile-group-code"><code>{{ tag.groupCode||'Code unavailable' }}</code><button class="icon-button" :aria-label="'Copy group code for '+(tag.groupName||tag.name)" :title="tag.groupCode?'Copy group code':'Group code unavailable'" :disabled="!tag.groupCode" @click="copyGroupCode(tag)"><AppIcon name="Copy" :size="14"/></button></span></template></span></div><p v-if="!tags.length" class="help">No visible tags.</p></section>
<section v-if="player.profileImageUrl" class="profile-image"><h3>Avatar</h3><img :src="player.profileImageUrl" alt="Player avatar preview"/></section>
<dl class="profile-details"><div v-if="instance?.worldName"><AppIcon name="Users" :size="18"/><span><dt>Instance</dt><dd>{{ instance.worldName }}</dd></span></div><div v-if="player.platform"><AppIcon name="Monitor" :size="18"/><span><dt>Platform</dt><dd>{{ player.platform }}</dd></span></div><div v-if="player.lastSeen"><AppIcon name="Clock3" :size="18"/><span><dt>Last encountered</dt><dd>{{ new Date(player.lastSeen).toLocaleString() }}</dd></span></div></dl>
<p class="action-notice" role="status">{{ notice }}</p>
</aside></template>
<style>
.profile-tag-content{min-width:0;flex:1}.profile-group-name{display:block;font-size:11px;color:var(--secondary);margin-top:8px;overflow-wrap:anywhere}.profile-group-code{display:flex;align-items:center;gap:6px;margin-top:3px}.profile-group-code code{font:11px Consolas,monospace;color:var(--text);overflow-wrap:anywhere}.profile-group-code .icon-button{width:24px;height:24px;flex-shrink:0}
</style>

