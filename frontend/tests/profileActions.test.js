import test from 'node:test'
import assert from 'node:assert/strict'
import { validUserId } from '../src/profileActions.js'
test('profile actions accept only full VRChat user IDs', () => {
  assert.equal(validUserId('usr_00000000-0000-0000-0000-000000000001'), true)
  for(const id of [undefined,null,{},'', 'Identity pending','preview_fictional_01','usr_missing','usr_00000000-0000-0000-0000-000000000001\n','https://vrchat.com/home/user/usr_00000000-0000-0000-0000-000000000001','usr_../../example'])
    assert.equal(validUserId(id), false)
})
