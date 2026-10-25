// Only a path inside this app is accepted, so a crafted ?returnUrl= cannot send the user to another site.
export function safeReturnUrl(url: string | null): string {
  return url && /^\/(?![/\\])/.test(url) ? url : '/boards';
}
