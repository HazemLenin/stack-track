import { passwordRules } from './password-rules';

const met = (password: string) =>
  Object.fromEntries(passwordRules(password).map((r) => [r.id, r.met]));

describe('passwordRules', () => {
  it('has every rule unmet for an empty password', () => {
    expect(met('')).toEqual({ len: false, up: false, lo: false, di: false });
  });

  it('flips each rule independently', () => {
    expect(met('abcdefgh')).toEqual({ len: true, up: false, lo: true, di: false });
    expect(met('ABCDEFGH')).toEqual({ len: true, up: true, lo: false, di: false });
    expect(met('12345678')).toEqual({ len: true, up: false, lo: false, di: true });
    expect(met('Ab1')).toEqual({ len: false, up: true, lo: true, di: true });
  });

  it('is fully met for a valid password', () => {
    expect(passwordRules('Valid1234').every((r) => r.met)).toBe(true);
  });

  it('uses the same labels as the server', () => {
    expect(passwordRules('').map((r) => r.label)).toEqual([
      'At least 8 characters',
      'One uppercase letter',
      'One lowercase letter',
      'One digit',
    ]);
  });
});
