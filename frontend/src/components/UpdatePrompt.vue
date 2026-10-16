<script setup>
import { ref, watch, nextTick } from 'vue'
import { AnimatePresence, motion } from 'motion-v'
import { spring, reducedMotion } from '../motion'
import { state, send } from '../state'
import AppIcon from './AppIcon.vue'
const dialog = ref(null)
let previousFocus
function element(){return dialog.value?.$el||dialog.value}
watch(()=>state.updates.prompt, async open=>{
 if(open){previousFocus=document.activeElement;await nextTick();element()?.querySelector('button')?.focus()}
 else previousFocus?.focus?.()
})
function later(){if(!state.updates.busy){if(state.preview)state.updates.prompt=false;else send('updates.later')}}
function keydown(event){
 if(event.key==='Escape'){event.preventDefault();later()}
 if(event.key==='Tab'){
  const buttons=[...element().querySelectorAll('button:not(:disabled)')]
  if(!buttons.length){event.preventDefault();return}
  const index=buttons.indexOf(document.activeElement)
  if(event.shiftKey&&index<=0){event.preventDefault();buttons.at(-1).focus()}
  else if(!event.shiftKey&&(index===buttons.length-1||index===-1)){event.preventDefault();buttons[0].focus()}
 }
}
</script>
<template><AnimatePresence><motion.div v-if="state.updates.prompt" class="update-backdrop" :initial="{opacity:0}" :animate="{opacity:1}" :exit="{opacity:0}" @keydown="keydown">
<motion.section ref="dialog" class="update-dialog" role="dialog" aria-modal="true" aria-labelledby="update-title" aria-describedby="update-description" :initial="{y:reducedMotion?0:30,scale:reducedMotion?1:.9,opacity:0}" :animate="{y:0,scale:1,opacity:1}" :exit="{y:reducedMotion?0:12,scale:reducedMotion?1:.96,opacity:0}" :transition="spring">
<AppIcon name="Download" :size="28"/><p class="update-eyebrow">ELIXR PLAYER LIST</p><h2 id="update-title">An update is ready</h2>
<p id="update-description">Version {{state.updates.availableVersion}} is available. You're running {{state.updates.currentVersion}}.</p>
<p class="update-status" role="status">{{state.updates.message}}</p><p class="help">The download is verified before the installer opens. Your settings and saved session stay on this computer.</p>
<div class="update-actions"><button :disabled="state.updates.busy" @click="later">Maybe later</button><button class="update-primary" :disabled="state.updates.busy||!state.host" @click="send('updates.install')">{{state.updates.busy?'Preparing update…':'Update now'}}</button></div>
</motion.section></motion.div></AnimatePresence></template>
<style>
.update-backdrop{position:fixed;inset:0;z-index:1000;background:#000b;display:grid;place-items:center;padding:24px}.update-dialog{width:min(440px,100%);padding:28px;background:#181818;border:1px solid #424242;border-radius:8px;box-shadow:0 16px 60px #0006;color:#eee}.update-dialog>svg{color:#ec83b7}.update-eyebrow{font-size:10px;letter-spacing:2px;color:#aaa;margin:16px 0 9px}.update-dialog h2{font-size:23px;margin:0 0 14px}.update-dialog p:not(.update-eyebrow){font-size:13px;line-height:1.6}.update-status{color:#ed9dc7;min-height:24px}.update-dialog .help{color:#aaa;font-size:11px!important}.update-actions{display:flex;justify-content:flex-end;flex-wrap:wrap;gap:10px;margin-top:24px}.update-actions button{padding:10px 15px}.update-actions .update-primary{background:#e782b4;color:#171117;border-color:#e782b4;font-weight:600}.update-actions .update-primary:hover:not(:disabled){background:#f3a3ce}
</style>
