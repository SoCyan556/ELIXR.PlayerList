const hex = /^#(?:[\da-f]{3}|[\da-f]{6}|[\da-f]{8})$/i
export const safeColor = value => typeof value === 'string' && hex.test(value) ? value : null
export function animationFor(tag) {
  return ['pulse', 'blink', 'rain', 'glitch', 'shake'].includes(tag.animation) ? tag.animation : ''
}
export function gradientColor(stops, position) {
  const colors = (stops || []).map(safeColor).filter(Boolean)
  if (!colors.length) return null
  const rgba = color => {
    let c = color.slice(1)
    if (c.length === 3) c = [...c].map(x => x + x).join('')
    if (c.length === 6) c += 'ff'
    return c.match(/../g).map(x => parseInt(x, 16))
  }
  const p = Math.max(0, Math.min(1, position)) * (colors.length - 1)
  const left = Math.floor(p), t = p - left
  const a = rgba(colors[left]), b = rgba(colors[Math.min(left + 1, colors.length - 1)])
  return '#' + a.map((x, i) => Math.round(x + (b[i] - x) * t).toString(16).padStart(2, '0')).join('')
}
export function textSegments(tag) {
  const name = String(tag.name || '').slice(0, 240)
  const base = safeColor(tag.color)
  const animation = animationFor(tag)
  // Animated labels use the first gradient stop as their base, as in the original renderer.
  if (animation && animation !== 'rain') return [{ text: name, color: safeColor(tag.gradient?.[0]) || base }]
  if (animation === 'rain' || tag.rainbow || tag.gradient?.length > 1) {
    const chars = Array.from(name)
    return chars.map((text, i) => ({ text, color: tag.rainbow || animation === 'rain'
      ? `hsl(${Math.round(210 + (i / Math.max(1, chars.length - 1)) * 220)} 85% 70%)`
      : gradientColor(tag.gradient, i / Math.max(1, chars.length - 1)) || base }))
  }
  // Runs are plain text only. Never render feed data with v-html.
  if (tag.runs?.length && tag.runs.map(run => run.text).join('').replace(/\s+/g, ' ').trim() === name)
    return tag.runs.slice(0, 240).map(run => ({ text: run.text, color: safeColor(run.color) || base }))
  return [{ text: name, color: base }]
}
