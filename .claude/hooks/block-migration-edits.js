// PreToolUse hook: CLAUDE.md says never edit existing migrations, add a new one.
// Blocks Edit/Write on tracked migration files; new files (not yet tracked) are allowed.
const { execFileSync } = require('child_process');

let raw = '';
process.stdin.on('data', (c) => (raw += c));
process.stdin.on('end', () => {
  const file = (JSON.parse(raw).tool_input || {}).file_path || '';
  if (!/[\\/]Migrations[\\/]/.test(file)) return;
  try {
    execFileSync('git', ['ls-files', '--error-unmatch', file], { stdio: 'ignore' });
  } catch {
    return; // untracked = migration being created now
  }
  console.error('Blocked: existing migrations are immutable. Add a new migration with `dotnet ef migrations add`.');
  process.exit(2);
});
