import { ref } from 'vue'
const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
export const reducedMotion = ref(preference.matches)
preference.addEventListener('change', e => reducedMotion.value = e.matches)
export const spring = { type: 'spring', stiffness: 380, damping: 29, mass: .85 }
export const softSpring = { type: 'spring', stiffness: 290, damping: 25, mass: .8 }
export function enterRow(index) { return reducedMotion.value ? { duration: 0 } : { ...softSpring, delay: Math.min(index, 9) * .022 } }

