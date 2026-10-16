<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import { motion, AnimatePresence } from 'motion-v'
import logo from '../assets/elixr-logo.png'
import AppIcon from './AppIcon.vue'
import { state, send } from '../state'
import { spring, reducedMotion } from '../motion'
const username = ref(''), password = ref(''), remember = ref(true), showPassword = ref(false), code = ref(''), method = ref('totp'), input = ref(null)
const verification = computed(() => state.auth.status === 'VerificationRequired')
const restoring = computed(() => state.auth.restoring)
const methods = computed(() => state.auth.verificationMethods || [])
watch(methods, values => { method.value = values.includes('totp') ? 'totp' : values[0] || 'totp' })
watch(() => state.auth.status, async () => { code.value='';password.value='';showPassword.value=false; await nextTick(); input.value?.focus() })
function submit() {
 if (!state.host || state.auth.busy) return
 if (verification.value) { send('auth.verify',{method:method.value, code:code.value});code.value='' }
 else { send('auth.login',{username:username.value,password:password.value,remember:remember.value});password.value='' }
}
const label = m => ({totp:'Authenticator code',emailOtp:'Email code',otp:'Recovery code'}[m] || m)
</script>
<template><div class="login-screen">
<div class="login-brand"><img :src="logo" alt=""/><span>ELIXR</span></div>
<AnimatePresence mode="wait">
<motion.section :key="restoring?'restore':verification?'verify':'login'" class="login-form" :initial="{y:reducedMotion?0:18,scale:reducedMotion?1:.97,opacity:0}" :animate="{y:0,scale:1,opacity:1}" :exit="{y:reducedMotion?0:-10,opacity:0}" :transition="spring">
<template v-if="restoring"><div class="session-loader" aria-hidden="true"><span/><span/><span/></div><h1>Signing you in…</h1><p class="login-subtitle">Restoring your saved session.</p><button class="text-button" :disabled="!state.host" @click="send('auth.logout')">Use another account</button></template>
<form v-else @submit.prevent="submit">
<h1>{{ verification?'Verify your sign-in':'Sign in to VRChat' }}</h1><p class="login-subtitle">{{ verification?'Enter your verification code to continue.':'Use your VRChat account to continue.' }}</p>
<template v-if="verification"><label v-if="methods.length>1" class="form-label">Verification method<select v-model="method" :disabled="state.auth.busy"><option v-for="m in methods" :key="m" :value="m">{{ label(m) }}</option></select></label><label class="form-label">{{ label(method) }}<input ref="input" v-model="code" :disabled="state.auth.busy" autocomplete="one-time-code" inputmode="text" maxlength="12" required placeholder="Verification code"/></label></template>
<template v-else><label class="form-label">Username or email<input ref="input" v-model="username" :disabled="state.auth.busy" autocomplete="username" maxlength="256" required placeholder="Username or email"/></label><label class="form-label">Password<span class="password-field"><input v-model="password" :disabled="state.auth.busy" :type="showPassword?'text':'password'" autocomplete="current-password" maxlength="1024" required placeholder="Password"/><button type="button" class="icon-button" :aria-label="showPassword?'Hide password':'Show password'" @click="showPassword=!showPassword"><AppIcon :name="showPassword?'EyeOff':'Eye'" :size="17"/></button></span></label><label class="remember"><input v-model="remember" type="checkbox" :disabled="state.auth.busy"/>Remember me</label></template>
<AnimatePresence><motion.p v-if="state.auth.message" :key="state.auth.message" class="login-error" role="alert" :initial="{x:reducedMotion?0:-5,opacity:0}" :animate="{x:0,opacity:1}" :exit="{opacity:0}" :transition="spring">{{ state.auth.message }}</motion.p></AnimatePresence>
<motion.button class="primary-button" type="submit" :disabled="state.auth.busy||!state.host" :while-press="reducedMotion?{}:{scale:.97}" :while-hover="reducedMotion?{}:{y:-1}" :transition="spring">{{ state.auth.busy?'Connecting…':verification?'Verify':'Sign in' }}</motion.button>
<button v-if="verification" type="button" class="text-button" :disabled="state.auth.busy" @click="send('auth.logout')">Back to sign in</button><button v-else-if="state.auth.status==='Failed'" type="button" class="text-button" :disabled="state.auth.busy" @click="send('auth.restore')">Retry saved session</button>
<p class="login-privacy">{{ verification?'Verification is handled directly by VRChat.':'Your password is sent only to VRChat.' }}<br v-if="!verification"/><span v-if="!verification">Remember me protects your session with Windows.</span></p>
</form>
</motion.section></AnimatePresence>
<span class="login-version">v{{state.updates.currentVersion}}</span>
</div></template>

