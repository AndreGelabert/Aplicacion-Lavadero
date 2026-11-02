'use strict';
const path = require('path');
const { AuthEmulator } = require(path.resolve(__dirname, '../../.local/firebase-emulator/npm/node_modules/firebase-tools/lib/emulator/auth/index.js'));
const emulator = new AuthEmulator({ host: '127.0.0.1', port: 9199, projectId: 'demo-local' });
emulator.start().catch(error => { console.error(error); process.exitCode = 1; });
