const names = new Intl.Collator(undefined, { sensitivity:'base', numeric:true })
export function playerGroup(player, selfId) {
  if (selfId && player.vrchatUserId && player.vrchatUserId.toLowerCase() === selfId.toLowerCase()) return 'You'
  return player.isFriend === true ? 'Friends' : 'Other players'
}
export function orderPlayers(players, selfId) {
  const rank = { You:0, Friends:1, 'Other players':2 }
  return [...players].sort((a,b) => rank[playerGroup(a,selfId)]-rank[playerGroup(b,selfId)] ||
    names.compare(a.displayName||'',b.displayName||'') || (a.vrchatUserId||'').localeCompare(b.vrchatUserId||''))
}
