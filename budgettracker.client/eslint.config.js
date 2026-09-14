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
    // BUD-20: MUI was removed entirely. The UI is Radix + Tailwind, styled from
    // shared/theme/tokens.ts. Nothing may reintroduce MUI or its emotion runtime.
    files: ['**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@mui/*', '@mui/**', '@emotion/*', '@emotion/**'],
              message:
                'MUI was removed (BUD-20). Use shared/components/ui + Tailwind tokens instead.',
            },
          ],
        },
      ],
    },
  },
])
