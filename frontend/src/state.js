import { reactive } from 'vue'
export const defaults = { showMaliciousTags: true, showCustomTags: true, showGroupTags: true, compactTags: false, overlayEnabled: true, overlayVisible: true, uiScale: 1, overlayOpacity: .94, checkUpdatesOnStartup: true }
const preview = import.meta.env.MODE === 'preview' && new URLSearchParams(location.search).get('preview') === '1'
export const state = reactive({ connected: false, instance: null, players: [], profileRequest: null, profileAction: null, updates: {currentVersion:'0.1.0',availableVersion:null,prompt:false,busy:false,enabled:false,message:'Updates are checked against the official GitHub releases.'}, settings: { ...defaults }, desktopOverlay: { visible: false }, auth: { status:'SignedOut',busy:false,restoring:false,message:null }, capabilities: { overlay: false, desktopOverlay: false, history: false, authentication: false }, host: false, preview, notice: '' })
const bridge = window.chrome?.webview
export function send(type, payload = {}) { if (!preview || ['ready','desktopOverlay.visibility','desktopOverlay.close','desktopOverlay.resize','desktopOverlay.layout','profile.copyId','profile.openInApp'].includes(type)) bridge?.postMessage({ type, ...payload }) }
if (bridge) {
  bridge.addEventListener('message', ({ data }) => {
    if (data.type === 'state') {
      if (!preview) Object.assign(state, data.payload)
      else if (data.payload.desktopOverlay) { state.desktopOverlay = data.payload.desktopOverlay; state.capabilities.desktopOverlay = !!data.payload.capabilities?.desktopOverlay }
      state.host = true
      state.notice = ''
    } else if (data.type === 'profile.focus') state.profileRequest = {userId:data.userId}
    else if (data.type === 'profile.copied') state.profileAction = {type:'copied',userId:data.userId}
    else if (data.type === 'group.copied') state.profileAction = {type:'groupCopied',userId:data.userId,groupId:data.groupId}
    else if (data.type === 'error') state.notice = data.message
  })
}
export async function start() {
  if (preview) {
    const { previewPlayers } = await import('./preview.js')
    const firstAvatar = previewPlayers[0].avatarUrl
    state.players = previewPlayers
    state.auth = { status:'SignedIn',busy:false,restoring:false,user:{displayName:'Preview account'} }
    // Development diagnostic only; this branch and fixture chunk are removed from production.
    window.__elixrPreview = {
      reset: () => { state.players = previewPlayers; Object.assign(state.settings, defaults); state.notice=''; state.auth = {status:'SignedIn',busy:false,restoring:false} },
      auth: status => { state.auth = {status, busy:status==='Restoring',restoring:status==='Restoring',verificationMethods:['totp','emailOtp']} },
      empty: () => { state.players = [] },
      update: () => { state.updates={currentVersion:'0.1.0',availableVersion:'0.2.0',prompt:true,busy:false,enabled:true,message:'A new stable release is available.'} },
      avatar: url => { state.players[0].avatarUrl = url }, resetAvatar: () => { state.players[0].avatarUrl = firstAvatar }
    }
  }
  send('ready')
}
export function updateSetting(key, value) {
  state.settings[key] = value
  send('settings.set', { key, value })
}

