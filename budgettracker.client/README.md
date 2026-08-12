# BudgetTracker Client — React + TypeScript + Vite

## Navigation & app shell

Authenticated routes render inside `<AppShell>` (`src/shared/components/layout/AppShell.tsx`),
a React Router layout route wrapped by `<ProtectedRoute>`. The shell is sidebar-first:

| Viewport | Behavior |
| --- | --- |
| `>= lg` (1024px) | Permanent sidebar, 240px wide, no top bar |
| `< lg` | Slim top bar with a hamburger that opens a temporary drawer |

Both drawers stay mounted and are toggled with `display`, so there is no first-render
flash and no focus loss from remounting.

### Routes

| Nav label | Path | Page |
| --- | --- | --- |
| Dashboard | `/` | `features/dashboard/pages/DashboardPage.tsx` |
| Budget Plans | `/budget-plans` | `features/budget-plans/pages/BudgetPlansPage.tsx` |
| Transactions | `/transactions` | `features/transactions/pages/TransactionsPage.tsx` |
| Settings | `/settings` | `features/settings/pages/SettingsPage.tsx` |

Nav items live in `src/shared/components/layout/navItems.ts`; add entries there rather
than in the sidebar markup. Links use `NavLink`, so the active item gets `aria-current="page"`
and prefix matching keeps a parent highlighted on nested routes.

The sidebar footer shows the signed-in account and hosts the **only** sign-out action
(`src/shared/hooks/useSignOut.ts`). `SIDEBAR_WIDTH` is exported from
`src/shared/components/layout/constants.ts` — never inline the number.

### Design system

Color, typography, spacing, and breakpoints come from `src/shared/theme/`.
`tokens.ts` is the single source of truth and is consumed by both `theme.ts` (MUI)
and `tailwind.config.ts`. MUI breakpoints deliberately mirror Tailwind's scale
(`sm 640 / md 768 / lg 1024 / xl 1280`) so `sx` and `className` agree.

A contract test fails the build on any hardcoded hex outside the theme folder —
use palette paths like `sx={{ bgcolor: 'primary.subtle' }}`.

---

## Tooling

This project uses Vite with HMR and ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) (or [oxc](https://oxc.rs) when used in [rolldown-vite](https://vite.dev/guide/rolldown)) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```
