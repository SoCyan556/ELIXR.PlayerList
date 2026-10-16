// Imported only by Vite's explicit development preview build. All entries are fictional.
const colors = ['#9A7185','#687E91','#897E9F','#A28A82','#6A9191','#88966F']
function portrait(index) {
 const color = colors[index % colors.length]
 const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="72" height="72" viewBox="0 0 72 72"><rect width="72" height="72" fill="#1b2530"/><circle cx="36" cy="32" r="22" fill="'+color+'"/><path d="M10 72c0-28 52-28 52 0" fill="'+color+'"/><path d="M14 30c-3-31 47-34 45 3L47 20 36 28 27 18z" fill="#303744"/><circle cx="28" cy="34" r="2" fill="#111820"/><circle cx="44" cy="34" r="2" fill="#111820"/><path d="M31 43q5 4 10 0" fill="none" stroke="#303744" stroke-width="2"/></svg>'
 return 'data:image/svg+xml,' + encodeURIComponent(svg)
}
const tag = (name,category) => ({name,category})
const names = ['Mizuki · Demo','Kuro · Demo','Luna · Demo','Rin · Demo','Valen · Demo','Sori · Demo','Nyx · Demo','Echo · Demo','Mochi · Demo','Kitsu · Demo','Aster · Demo','Zyro · Demo','Fictional player with an exceptionally long display name','Nimbus · Demo','Willow · Demo','Arlo · Demo','Pip · Demo','Sol · Demo']
const sets = [
 [tag('Known Crasher','Malicious'),tag('Client Mod','Malicious'),tag('ELIXR Staff','Group')],
 [tag('Chill','Custom'),tag('Frequent','Custom')],
 [tag('Friend','Custom')],[tag('Nocturnale','Group')],
 [tag('Quest User','Custom'),tag('New User','Custom')],[tag('ELIXR','Group')],
 [tag('Chill','Custom'),tag('Mute Often','Custom')],[tag('Suspected Alt','Malicious')],
 [tag('Artist','Custom'),tag('Kappa','Group')],[tag('PC Only','Custom')],
 [tag('Trusted','Group')],[tag('Loud','Custom'),tag('Funny','Custom')],
 [tag('Long community affiliation label','Group'),tag('Multiple','Custom'),tag('Additional tag','Custom'),tag('Risk example','Malicious'),tag('More tags','Custom')],[],[tag('Explorer','Custom')],[],[tag('Night Owls','Group')],[]
]
// Synthetic feed styling for offline visual diagnostics; not live labels or identities.
Object.assign(sets[0][0], { color:'#ff4d6d', source:'FewTags' })
Object.assign(sets[0][1], { color:'#ff657b', animation:'glitch', source:'ETags' })
Object.assign(sets[0][2], { color:'#24b8ff', animation:'pulse', source:'Groups', groupId:'grp_00000000-0000-0000-0000-000000000001',groupName:'ELIXR Preview Group',groupCode:'ELIXR.0042' })
Object.assign(sets[3][0], { groupId:'grp_00000000-0000-0000-0000-000000000002',groupName:'Nocturnale Preview Group' })
Object.assign(sets[1][0], { gradient:['#ff7bdd','#9387ff'], source:'ETags' })
Object.assign(sets[1][1], { runs:[{text:'Fre',color:'#ff7bdd'},{text:'quent',color:'#24b8ff'}], source:'FewTags' })
Object.assign(sets[2][0], { animation:'rain', source:'FewTags' })
export const previewPlayers = names.map((displayName,i) => ({displayName,vrchatUserId:'preview_fictional_'+String(i+1).padStart(2,'0'),hasCompleteIdentity:false,tags:sets[i],avatarUrl:portrait(i)}))
// Syntactically valid but entirely fictional identity for local profile-action diagnostics.
previewPlayers[0].vrchatUserId = 'usr_00000000-0000-0000-0000-000000000001'

