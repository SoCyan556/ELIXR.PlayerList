import { test } from 'node:test'
import assert from 'node:assert/strict'
import { tagTokens } from '../src/tags.js'
const tags = [{ name:'A',category:'Malicious' },{ name:'B',category:'Malicious' },{ name:'C',category:'Custom' },{ name:'D',category:'Group' }]
const settings = { showMaliciousTags:true,showCustomTags:true,showGroupTags:true,compactTags:false }
test('full tags retain category and individual labels',()=>assert.deepEqual(tagTokens(tags,settings),tags))
test('compact mode emits exactly one token per visible category',()=>assert.deepEqual(tagTokens(tags,{...settings,compactTags:true}).map(t=>t.category),['Malicious','Custom','Group']))
test('hidden categories disappear in both modes',()=>{ for(const compactTags of [false,true]) assert.deepEqual(tagTokens(tags,{...settings,compactTags,showMaliciousTags:false,showGroupTags:false}).map(t=>t.category),['Custom']) })
test('unknown categories are never presented as a trusted category',()=>assert.deepEqual(tagTokens([{name:'Admin',category:'Unknown'}],settings),[]))

