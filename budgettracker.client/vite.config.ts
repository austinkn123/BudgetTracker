import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

/**
 * The ASP.NET dev certificate for the HTTPS dev server, exported with `dotnet dev-certs` on first run.
 * Only `vite` / `vite preview` serve HTTPS, so `vite build` (CI, packaging) needs neither the
 * certificate nor the .NET SDK.
 */
function devCertificate() {
    const baseFolder =
        env.APPDATA !== undefined && env.APPDATA !== ''
            ? `${env.APPDATA}/ASP.NET/https`
            : `${env.HOME}/.aspnet/https`;

    const certificateName = "budgettracker.client";
    const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(baseFolder)) {
        fs.mkdirSync(baseFolder, { recursive: true });
    }

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        if (0 !== child_process.spawnSync('dotnet', [
            'dev-certs',
            'https',
            '--export-path',
            certFilePath,
            '--format',
            'Pem',
            '--no-password',
        ], { stdio: 'inherit', }).status) {
            throw new Error("Could not create certificate.");
        }
    }

    return { key: fs.readFileSync(keyFilePath), cert: fs.readFileSync(certFilePath) };
}

const target = env.VITE_API_PROXY_TARGET ??
    (env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
        env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7134');

// https://vitejs.dev/config/
export default defineConfig(({ command }) => ({
    plugins: [plugin()],
    build: {
        rollupOptions: {
            output: {
                // Framework code every page needs at boot, in chunks that only change when a dependency does,
                // so a deploy of app code leaves them cached (BUD-28). Pages are split by React.lazy in App.tsx.
                manualChunks(id) {
                    if (!id.includes('node_modules')) return undefined;
                    if (/node_modules\/(react|react-dom|react-router|react-router-dom|scheduler)\//.test(id)) return 'vendor-react';
                    if (/node_modules\/(@aws-amplify|aws-amplify|@aws-sdk|@smithy|@aws-crypto)\//.test(id)) return 'vendor-amplify';
                    if (/node_modules\/(@tanstack|axios)\//.test(id)) return 'vendor-data';
                    return undefined;
                },
            },
        },
    },
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            },
            '^/weatherforecast': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '53608'),
        https: command === 'serve' ? devCertificate() : undefined,
    }
}));
