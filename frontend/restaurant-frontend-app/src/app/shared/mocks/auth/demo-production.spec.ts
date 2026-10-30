import { clearDemoLogin, createDemoTokens } from './demo-auth';

describe('Production demo cleanup', () => {
  it('removes cached demo credentials before production auth starts', () => {
    const values = new Map<string, string>([['auth', JSON.stringify(createDemoTokens())], ['user', 'demo-user']]);
    const storage = { getItem: (key: string) => values.get(key) ?? null, removeItem: (key: string) => { values.delete(key); } };
    expect(clearDemoLogin(storage)).toBeTrue();
    expect(values.has('auth')).toBeFalse();
    expect(values.has('user')).toBeFalse();
  });
  it('preserves real credentials', () => {
    const values = new Map<string, string>([['auth', JSON.stringify({ idToken: 'real.jwt.token' })], ['user', 'real-user']]);
    const storage = { getItem: (key: string) => values.get(key) ?? null, removeItem: (key: string) => { values.delete(key); } };
    expect(clearDemoLogin(storage)).toBeFalse();
    expect(values.get('user')).toBe('real-user');
  });
});
