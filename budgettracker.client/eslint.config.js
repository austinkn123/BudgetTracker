import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
  },
  {
    // BUD-16 AC 1: feature and page code must route these primitives through the
    // shared Bud* library so styling stays in one channel. The library itself
    // (src/shared/components/ui) is exempt — it is what wraps them.
    files: ['src/features/**/*.{ts,tsx}', 'src/pages/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: [
                '@mui/material/Button',
                '@mui/material/TextField',
                '@mui/material/Select',
                '@mui/material/Dialog',
                '@mui/material/DialogActions',
                '@mui/material/DialogContent',
                '@mui/material/DialogContentText',
                '@mui/material/DialogTitle',
                '@mui/material/Table',
                '@mui/material/TableBody',
                '@mui/material/TableCell',
                '@mui/material/TableContainer',
                '@mui/material/TableHead',
                '@mui/material/TableRow',
                '@mui/material/Chip',
                '@mui/material/Alert',
                '@mui/material/AlertTitle',
                '@mui/material/Card',
                '@mui/material/CardContent',
                '@mui/material/CardHeader',
              ],
              message:
                'Use the Bud* wrapper from shared/components/ui instead (BUD-16 AC 1).',
            },
          ],
        },
      ],
    },
  },
])
