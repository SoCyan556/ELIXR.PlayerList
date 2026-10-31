import { test } from 'node:test'
import assert from 'node:assert/strict'
import { orderPlayers, playerGroup } from '../src/playerOrder.js'
const p=(name,id,isFriend=false)=>({displayName:name,vrchatUserId:id,isFriend,tags:[]})
test('self first, alphabetical friends next, alphabetical others last',()=>{
 const input=[p('zebra','z'),p('Zed','f2',true),p('me','self'),p('Amy','f1',true),p('alice','a')]
 assert.deepEqual(orderPlayers(input,'self').map(p=>p.vrchatUserId),['self','f1','f2','a','z'])
 assert.equal(input[0].vrchatUserId,'z')
})
test('self is identified by user ID, not a duplicate display name',()=>{
 assert.equal(playerGroup(p('Me','other'),'SELF'),'Other players')
 assert.equal(playerGroup(p('Different name','self',true),'SELF'),'You')
 assert.equal(playerGroup(p('Me',''),'self'),'Other players')
})
test('a community Friend tag does not imply an actual friendship',()=>{
 assert.equal(playerGroup({...p('A','a'),tags:[{name:'Friend',category:'Custom'}]},'self'),'Other players')
 assert.equal(playerGroup({...p('A','a'),isFriend:'true'},'self'),'Other players')
})
test('natural alphabetical order ignores case and uses stable ID tie breaks',()=>{
 const rows=[p('Player 10','10'),p('player 2','2'),p('ALICE','b'),p('alice','a')]
 assert.deepEqual(orderPlayers(rows,null).map(p=>p.vrchatUserId),['a','b','2','10'])
})
test('empty list and missing names are safe',()=>{
 assert.deepEqual(orderPlayers([],null),[])
 assert.equal(orderPlayers([{vrchatUserId:'a'},p('A','b')],null).length,2)
})
