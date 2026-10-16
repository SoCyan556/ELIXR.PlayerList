export function validUserId(value) {
  return typeof value === 'string' && /^usr_[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(value) && value.length === 40
}
