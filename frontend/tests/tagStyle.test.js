import test from 'node:test'
import assert from 'node:assert/strict'
import { safeColor, animationFor, gradientColor, textSegments } from '../src/tagStyle.js'
test('feed colors are data, never arbitrary CSS', () => {
  assert.equal(safeColor('#aAbBcc'), '#aAbBcc')
  for (const value of ['url(https://example.com)', 'red;display:none', '<script>', null]) assert.equal(safeColor(value), null)
  assert.equal(animationFor({ animation: 'scroll' }), '')
  assert.equal(animationFor({ animation: 'pulse' }), 'pulse')
})
test('static gradients interpolate exact endpoints and midpoint', () => {
  assert.equal(gradientColor(['#000', '#fff'], 0), '#000000ff')
  assert.equal(gradientColor(['#000', '#fff'], .5), '#808080ff')
  assert.equal(gradientColor(['#000', '#fff'], 1), '#ffffffff')
})
test('rich text is represented only by safe color runs', () => {
  assert.deepEqual(textSegments({name:'AB',runs:[{text:'A',color:'#f00'},{text:'B',color:'#00f'}]}), [{text:'A',color:'#f00'},{text:'B',color:'#00f'}])
  assert.equal(textSegments({name:'Canonical',runs:[{text:'wrong',color:'#f00'}]})[0].text, 'Canonical')
})
test('animated source gradients use first stop; rainbow is per character', () => {
  assert.deepEqual(textSegments({name:'AB',color:'#fff',gradient:['#f00','#00f'],animation:'pulse'}), [{text:'AB',color:'#f00'}])
  assert.equal(textSegments({name:'AB',animation:'rain'}).length, 2)
})
