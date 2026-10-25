export interface PasswordRule {
  id: 'len' | 'up' | 'lo' | 'di';
  label: string;
  met: boolean;
}

// Labels and checks mirror RegisterCommandValidator on the API.
export function passwordRules(password: string): PasswordRule[] {
  return [
    { id: 'len', label: 'At least 8 characters', met: password.length >= 8 },
    { id: 'up', label: 'One uppercase letter', met: /[A-Z]/.test(password) },
    { id: 'lo', label: 'One lowercase letter', met: /[a-z]/.test(password) },
    { id: 'di', label: 'One digit', met: /\d/.test(password) },
  ];
}
