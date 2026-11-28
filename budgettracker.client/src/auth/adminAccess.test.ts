import { describe, expect, it } from 'vitest';
import { isAdminToken } from './adminAccess';

const base64Url = (value: string) => btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
const tokenWith = (payload: object) => `header.${base64Url(JSON.stringify(payload))}.signature`;

describe('isAdminToken', () => {
  it('is true when cognito:groups includes admin', () => {
    expect(isAdminToken(tokenWith({ 'cognito:groups': ['beta', 'admin'] }))).toBe(true);
  });

  it.each([
    ['no groups claim', tokenWith({ sub: 'x' })],
    ['another group', tokenWith({ 'cognito:groups': ['admins'] })],
    ['groups as a string', tokenWith({ 'cognito:groups': 'admin' })],
  ])('is false with %s', (_, token) => {
    expect(isAdminToken(token)).toBe(false);
  });

  it.each([null, undefined, '', 'not-a-jwt', 'a.%%%.c'])('is false for %j', (token) => {
    expect(isAdminToken(token)).toBe(false);
  });

  it('decodes base64url payloads that need padding and url-safe characters', () => {
    const token = tokenWith({ 'cognito:groups': ['admin'], name: '??>>~~' });
    expect(isAdminToken(token)).toBe(true);
  });
});
