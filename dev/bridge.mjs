import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';

const [command = 'status', argument] = process.argv.slice(2);
const endpoints = { status: 'GET', logs: 'GET', ui: 'GET', reload: 'POST', screenshot: 'POST' };
if (!(command in endpoints)) throw new Error('Use status, logs [filter], ui, reload, or screenshot [output.png].');
const profile = process.env.VALHEIM_PROFILE ?? join(homedir(), '.config/r2modmanPlus-local/Valheim/profiles/Default');
const key = readFileSync(join(profile, 'BepInEx/config/ValheimDevBridge.key'), 'utf8').trim();
const url = new URL(command, 'http://127.0.0.1:19287/');
if (command === 'logs' && argument) url.searchParams.set('contains', argument);
const response = await fetch(url, { method: endpoints[command], headers: { 'X-Valheim-Dev-Key': key }, signal: AbortSignal.timeout(25000) });
if (!response.ok) throw new Error(await response.text());
if (command === 'screenshot') {
  const path = argument ?? '/tmp/valheim-dev.png';
  writeFileSync(path, new Uint8Array(await response.arrayBuffer()));
  console.log(path);
} else console.log(JSON.stringify(await response.json(), null, 2));
