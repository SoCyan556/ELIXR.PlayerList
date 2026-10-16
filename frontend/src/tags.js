export const categories = [
  { key: 'Malicious', setting: 'showMaliciousTags', label: 'Malicious', icon: 'Skull', className: 'malicious' },
  { key: 'Custom', setting: 'showCustomTags', label: 'Fun / Custom', icon: 'Smile', className: 'custom' },
  { key: 'Group', setting: 'showGroupTags', label: 'Group', icon: 'Users', className: 'group' }
]
export function visibleTags(tags, settings) {
  return tags.filter(tag => categories.some(c => c.key === tag.category && settings[c.setting]))
}
export function categoryFor(tag) { return categories.find(c => c.key === tag.category) }
export function tagTokens(tags, settings) {
  const visible = visibleTags(tags, settings)
  return settings.compactTags ? categories.filter(c => visible.some(t => t.category === c.key)).map(c => ({ category: c.key, name: c.label })) : visible
}

