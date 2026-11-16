import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = resolve(root, 'artifacts/mermaid');
const installed = JSON.parse(await readFile(resolve(root, 'tools/mermaid/node_modules/mermaid/package.json'), 'utf8'));
if (installed.version !== '12.3.0') throw Error('Installed Mermaid differs from reviewed version');
await mkdir(output, { recursive: true });
const diagrams = [];
for (const name of ['single-end', 'process-chain', 'agent-task', 'review', 'inline-scripts']) {
  const markdown = await readFile(resolve(root, `examples/${name}.md`), 'utf8');
  diagrams.push({ name, source: markdown.match(/```mermaid\r?\n([\s\S]*?)\r?\n```/)[1] });
}
diagrams.push({ name: 'whitespace-entities-cycle', source: `%% preceding comment
flowchart   LR
%% merstride: compatible
    work["Work #quot; &amp; text"]
    review["Review"]
    done["Finish"]
    work -->|exit_0| review
    review -->|retry| work
    review -->|approved| done` });
for (const name of ['end', 'subgraph', 'style', 'class', 'flowchart', 'graph', 'interpolate'])
  diagrams.push({name: 'reserved-' + name, source: `flowchart TD\n${name}["Finish"]`, rejected: true});
for (const [name, declaration] of [['space-bracket', 'done ["Finish"]'], ['space-label', 'done[ "Finish" ]']])
  diagrams.push({name, source: `flowchart TD\n${declaration}`, rejected: true});

let finish;
const completed = new Promise(resolve => { finish = resolve; });
const server = createServer(async (request, response) => {
  try {
    if (request.url === '/result' && request.method === 'POST') {
      let text = '';
      for await (const chunk of request) text += chunk;
      finish(JSON.parse(text)); response.end('ok'); return;
    }
    if (request.url === '/') {
      response.setHeader('Content-Type', 'text/html');
      response.end(`<body><script type="module">
        import mermaid from '/tools/mermaid/node_modules/mermaid/dist/mermaid.esm.min.mjs';
        const results = [];
        mermaid.initialize({startOnLoad:false, securityLevel:'strict'});
        for (const [i, fixture] of ${JSON.stringify(diagrams)}.entries()) {
          try {
            await mermaid.parse(fixture.source);
            const {svg} = await mermaid.render('diagram' + i, fixture.source);
            if (!svg.includes('<svg') || !svg.includes('flowchart')) throw Error('Missing flowchart SVG');
            results.push({name:fixture.name, passed:!fixture.rejected, svg});
          } catch (error) { results.push({name:fixture.name, passed:!!fixture.rejected, rejected:true, error:String(error)}); }
        }
        await fetch('/result', {method:'POST', body:JSON.stringify(results)});
      </script>`); return;
    }
    const path = resolve(root, '.' + decodeURIComponent(request.url.split('?')[0]));
    const allowed = resolve(root, 'tools/mermaid/node_modules') + sep;
    if (!path.startsWith(allowed)) { response.writeHead(403).end(); return; }
    response.setHeader('Content-Type', ['.mjs', '.js'].includes(extname(path)) ? 'text/javascript' : 'application/octet-stream');
    response.end(await readFile(path));
  } catch { response.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const browser = process.env.MERSTRIDE_BROWSER ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const profile = resolve(output, 'browser-' + randomUUID());
const child = spawn(browser, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
  '--edge-skip-compat-layer-relaunch', '--disable-extensions', '--disable-background-networking',
  `--user-data-dir=${profile}`, `http://127.0.0.1:${server.address().port}/`], { stdio: 'ignore', windowsHide: true, detached: process.platform !== 'win32' });
let timer;
try {
  const result = await Promise.race([completed, new Promise((_, reject) => {
    timer = setTimeout(() => reject(Error('Mermaid renderer exceeded 30-second watchdog')), 30000);
    child.once('error', reject);
  })]);
  for (const item of result) if (item.passed && item.svg) {
    await writeFile(resolve(output, item.name + '.svg'), item.svg);
    delete item.svg;
  }
  await writeFile(resolve(output, 'results.json'), JSON.stringify({ mermaid: '12.3.0', browser, result }, null, 2));
  console.log(JSON.stringify(result));
  if (result.some(item => !item.passed)) process.exitCode = 1;
} finally {
  clearTimeout(timer);
  try {
    if (process.platform === 'win32') {
      // Edge may relaunch away from spawn's PID. Match only our unique isolated profile.
      const quoted = "'" + profile.replaceAll("'", "''") + "'";
      const processName = "'" + browser.split(/[\\/]/).at(-1).replaceAll("'", "''") + "'";
      execFileSync('pwsh', ['-NoProfile', '-Command', `$profilePath = ${quoted}; Get-CimInstance Win32_Process |
        Where-Object { $_.Name -eq ${processName} -and $_.CommandLine -and $_.CommandLine.Contains($profilePath) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }`],
      { stdio: 'ignore', windowsHide: true, timeout: 15000 });
    } else if (child.pid) {
      // The isolated Linux process group includes Chrome's renderer descendants.
      try { process.kill(-child.pid, 'SIGTERM'); } catch (error) { if (error.code !== 'ESRCH') throw error; }
    }
  } finally { server.close(); }
}
