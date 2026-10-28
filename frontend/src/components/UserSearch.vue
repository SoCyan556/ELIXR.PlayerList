<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { motion, AnimatePresence } from 'motion-v'
import { state, send } from '../state'
import { spring, enterRow, reducedMotion } from '../motion'
import AppIcon from './AppIcon.vue'
import PlayerAvatar from './PlayerAvatar.vue'
import TagBadges from './TagBadges.vue'
import ProfilePanel from './ProfilePanel.vue'
const query=ref(''), submitted=ref(''), users=ref([]), selected=ref(null), busy=ref(false), checking=ref(false), error=ref(''), searched=ref(false), page=ref(0), hasMore=ref(false), feeds=ref([])
// Monotonic across component mounts, so a previous page's late reply cannot replace a new search.
let activeRequest=0
function nextRequest(){return window.__elixrSearchRequest=(window.__elixrSearchRequest||0)+1}
function search(nextPage=0){
 const text=nextPage===0?query.value.trim():submitted.value
 if(text.length<2||text.length>64){error.value='Enter a VRChat display name between 2 and 64 characters.';return}
 activeRequest=nextRequest();busy.value=true;checking.value=false;error.value='';selected.value=null;users.value=[];feeds.value=[];submitted.value=text;page.value=nextPage;hasMore.value=false;searched.value=true
 send('users.search',{query:text,page:nextPage,requestId:activeRequest})
}
function inspect(user){
 activeRequest=nextRequest();error.value='';selected.value={...user,tagsChecked:false};checking.value=true
 send('users.lookup',{userId:user.vrchatUserId,requestId:activeRequest})
}
function receive({detail:data}){
 if(data.requestId!==activeRequest)return
 busy.value=false;checking.value=false
 if(data.type==='users.error'){error.value=data.message;return}
 if(data.type==='users.results'){users.value=data.users;hasMore.value=data.hasMore}
 if(data.type==='users.detail'){selected.value=data.user;users.value=users.value.map(u=>u.vrchatUserId===data.user.vrchatUserId?data.user:u);feeds.value=data.feeds||[]}
}
function closeProfile(){activeRequest=nextRequest();selected.value=null;checking.value=false;send('users.cancel')}
onMounted(()=>window.addEventListener('elixr:user-search',receive))
onBeforeUnmount(()=>{window.removeEventListener('elixr:user-search',receive);if(state.auth.status==='SignedIn')send('users.cancel')})
</script>
<template><section class="user-search-page">
 <form class="user-search-form" @submit.prevent="search()"><label for="vrchat-user-query">Find a VRChat player</label><div class="user-search-input"><AppIcon name="Search" :size="18"/><input id="vrchat-user-query" v-model="query" type="search" placeholder="VRChat display name…" autocomplete="off" minlength="2" maxlength="64" required/><button type="submit" :disabled="busy||!state.host||state.preview">{{busy?'Searching…':'Search'}}</button></div><p class="help">Search all of VRChat, not just your instance. Select an account to check FewTags, ETags, and public group tags.</p></form>
 <p v-if="state.preview||!state.host" class="notice">Live search is available in the signed-in desktop app.</p>
 <p v-if="error" class="notice" role="alert">{{error}}</p>
 <div class="players-layout search-results-layout" :class="{'profile-open':selected}"><div class="list-column">
 <div class="search-result-heading" role="status">{{busy?'Searching VRChat…':searched?`${users.length} results · page ${page+1}`:'Search by the name shown on their VRChat profile.'}}</div>
 <div class="player-table" role="list" aria-label="VRChat search results" :aria-busy="busy">
 <motion.div v-for="(user,index) in users" :key="user.vrchatUserId" class="player-entry" :initial="{y:reducedMotion?0:12,opacity:0}" :animate="{y:0,opacity:1}" :transition="enterRow(index)"><button class="player-row" :class="{selected:selected?.vrchatUserId===user.vrchatUserId}" :aria-pressed="selected?.vrchatUserId===user.vrchatUserId" @click="inspect(user)"><span class="player-identity"><PlayerAvatar :player="user"/><span class="player-name"><strong>{{user.displayName}}</strong><small>{{user.vrchatUserId}}</small></span></span><TagBadges v-if="user.tagsChecked&&user.tags.length" :tags="user.tags" :settings="state.settings"/><span v-else class="search-tag-status">{{checking&&selected?.vrchatUserId===user.vrchatUserId?'Checking…':user.tagsChecked?'No matches in available sources':'Check tags'}}<AppIcon name="ChevronRight" :size="14"/></span></button></motion.div>
 <div v-if="!users.length" class="empty-state"><AppIcon name="Search" :size="26"/><h2>{{busy?'Looking for players…':searched?'No results on this page':'Find someone, check their tags'}}</h2><p>{{busy?'Your saved VRChat session handles the search.':searched?'Try a different display name or the previous page.':'You don’t need to share an instance with them.'}}</p></div>
 </div><div v-if="searched&&!busy" class="search-pagination"><button :disabled="page===0" @click="search(page-1)">Previous</button><span>Page {{page+1}}</span><button :disabled="!hasMore" @click="search(page+1)">Next</button></div>
 </div><AnimatePresence><motion.div v-if="selected" :key="selected.vrchatUserId" class="profile-slot" :initial="{x:reducedMotion?0:24,opacity:0}" :animate="{x:0,opacity:1}" :exit="{x:reducedMotion?0:20,opacity:0}" :transition="spring"><div v-if="checking||!selected.tagsChecked" class="section-panel search-checking" role="status"><AppIcon name="Search" :size="24"/><h2>{{checking?'Checking tags…':'Tags could not be checked'}}</h2><p class="help">{{selected.displayName}}</p><button v-if="!checking" @click="inspect(selected)">Retry</button><button @click="closeProfile">Close</button></div><template v-else><p class="search-check-summary" role="status">{{selected.tags.length?`${selected.tags.length} matching tags · display filters apply`:'No matching tags in available feeds or memberships.'}}</p><p v-for="feed in feeds.filter(f=>f.state!=='Live')" :key="feed.name" class="help">{{feed.name}}: {{feed.state}}</p><ProfilePanel :player="selected" :settings="state.settings" :instance="null" @close="closeProfile"/></template></motion.div></AnimatePresence></div>
 <p class="help search-disclaimer">Community tags are third-party labels, not verified facts. Unavailable feeds, private memberships, or VRChat rate limits can leave results incomplete. No match does not guarantee someone has no tags. Searches are not added to your instance list or saved as history.</p>
</section></template>
<style>
.user-search-page{height:100%;min-height:0;display:flex;flex-direction:column;gap:12px}.user-search-form>label{display:block;font-weight:600;font-size:13px;margin-bottom:10px}.user-search-input{display:flex;align-items:center;gap:12px;border:1px solid var(--border);padding:8px 10px;background:var(--panel);border-radius:5px}.user-search-input input{flex:1;min-width:0;border:0;background:transparent;padding:7px 0;color:var(--text)}.user-search-form .help{margin:9px 0 0}.search-results-layout{flex:1;min-height:180px;height:auto!important}.search-results-layout .list-column{min-height:0}.search-result-heading{font-size:12px;color:var(--secondary);padding:0 0 10px}.search-tag-status{display:flex;gap:8px;align-items:center;font-size:11px;color:var(--secondary);margin-left:auto}.search-pagination{display:flex;align-items:center;justify-content:flex-end;gap:12px;padding:12px 0;font-size:12px;color:var(--secondary)}.search-disclaimer{margin:0;line-height:1.6}.search-check-summary{font-size:12px;color:var(--secondary);margin:0 0 10px}.search-results-layout .profile-slot{display:flex;flex-direction:column;overflow:auto}.search-results-layout .profile-panel{flex:1;min-height:250px}.search-checking{padding:24px;display:grid;gap:12px}.search-results-layout .player-row{grid-template-columns:minmax(150px,1fr) minmax(100px,1fr)}
@media(max-width:850px){.search-results-layout.profile-open{display:flex;flex-direction:column;overflow:auto}.search-results-layout.profile-open .list-column{flex:0 0 240px}.search-results-layout .profile-slot{flex:0 0 auto}.search-tag-status{font-size:10px}}
.search-results-layout .profile-panel{position:relative;width:100%;box-sizing:border-box}.user-search-input{background:var(--surface)}.user-search-page .player-table{overflow:auto}
</style>
