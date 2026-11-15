# 6. Frontend: the React app

The frontend lives in `mizan-web/`. It is a **single-page application (SPA)**: the browser loads one HTML page once, and after that React swaps what is on the screen without reloading the page. Moving between "pages" (`/tickets`, `/tickets/new`, `/tickets/:id`) is done by **client-side routing** with React Router, so the URL changes but the browser never asks the server for a new HTML page. The only thing that talks to the server is the API wrapper, which sends JSON requests to the backend from chapters 2 to 5. A React **component** is simply a JavaScript function that returns what the screen should look like right now; when its data changes, React calls the function again and updates only the parts of the screen that changed.

| File | One line |
|---|---|
| [package.json](../../mizan-web/package.json) | The project file: dependencies (React, React Router, Bootstrap, Vite) and npm scripts. |
| [vite.config.js](../../mizan-web/vite.config.js) | Dev server settings: port 5173 and the `/api` proxy to the backend on 5199. |
| [index.html](../../mizan-web/index.html) | The one HTML page. Has an empty `<div id="root">` that React fills. |
| [src/main.jsx](../../mizan-web/src/main.jsx) | The entry point: starts React inside `#root`, wraps the app in the router and the auth provider. |
| [src/App.jsx](../../mizan-web/src/App.jsx) | The route table: which URL shows which page, and which pages need a login. |
| [src/index.css](../../mizan-web/src/index.css) | The few CSS rules Bootstrap doesn't cover. |
| [src/api/client.js](../../mizan-web/src/api/client.js) | The one `api()` function every page uses to call the backend, plus `ApiError` and the token store. |
| [src/auth/AuthContext.jsx](../../mizan-web/src/auth/AuthContext.jsx) | Shares "who is logged in" with every component; login, logout, `/me` check after a refresh. |
| [src/auth/RequireAuth.jsx](../../mizan-web/src/auth/RequireAuth.jsx) | Guard: shows the page only to a logged-in user, otherwise sends them to `/login`. |
| [src/components/Layout.jsx](../../mizan-web/src/components/Layout.jsx) | The navbar around every logged-in page. |
| [src/pages/LoginPage.jsx](../../mizan-web/src/pages/LoginPage.jsx) | Login form with one-click demo account buttons. |
| [src/pages/TicketsPage.jsx](../../mizan-web/src/pages/TicketsPage.jsx) | The ticket list (already filtered by role on the server). |
| [src/pages/CreateTicketPage.jsx](../../mizan-web/src/pages/CreateTicketPage.jsx) | The new-ticket form (clients only), with server errors shown per field. |
| [src/pages/TicketDetailsPage.jsx](../../mizan-web/src/pages/TicketDetailsPage.jsx) | One ticket: details, snapshots, responses, action buttons, history. |
| [src/components/ticket/ActionsPanel.jsx](../../mizan-web/src/components/ticket/ActionsPanel.jsx) | Draws one button or form per entry in the server's `allowedActions`. |
| [src/components/ticket/RouteForm.jsx](../../mizan-web/src/components/ticket/RouteForm.jsx) | Manager picks a team. |
| [src/components/ticket/AssignForm.jsx](../../mizan-web/src/components/ticket/AssignForm.jsx) | Leader ticks team members to assign. |
| [src/components/ticket/SubmitFixForm.jsx](../../mizan-web/src/components/ticket/SubmitFixForm.jsx) | Member enters branch, commit SHA and summary of the fix. |
| [src/components/ticket/HistoryList.jsx](../../mizan-web/src/components/ticket/HistoryList.jsx) | Shows the audit log entries. |
| [src/workflow/actions.js](../../mizan-web/src/workflow/actions.js) | How each workflow action looks: URL path, label, colour, form, confirm text. |
| [src/utils/labels.js](../../mizan-web/src/utils/labels.js) | Display helpers: colours, "LeaderReview" to "Leader Review", date formatting. |
| [src/utils/fieldErrors.js](../../mizan-web/src/utils/fieldErrors.js) | Reads the API's per-field validation errors. |

---

## [package.json](../../mizan-web/package.json)

**What it is:** The npm project file. It is the frontend's version of a `.csproj`: it lists the packages the app needs and defines short commands (scripts).

**The code:**

```json
{
  "name": "mizan-web",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "lint": "eslint .",
    "preview": "vite preview"
  },
  "dependencies": {
    "bootstrap": "^5.3.9",
    "react": "^19.1.0",
    "react-dom": "^19.1.0",
    "react-router-dom": "^7.18.5"
  },
  "devDependencies": {
    "@eslint/js": "^9.25.0",
    "@types/react": "^19.1.2",
    "@types/react-dom": "^19.1.2",
    "@vitejs/plugin-react": "^4.4.1",
    "eslint": "^9.25.0",
    "eslint-plugin-react-hooks": "^5.2.0",
    "eslint-plugin-react-refresh": "^0.4.19",
    "globals": "^16.0.0",
    "vite": "^6.3.5"
  }
}
```

**Line by line:**
- `"private": true`: stops anyone from accidentally publishing this app to the public npm registry.
- `"type": "module"`: the `.js` files use **ES modules** (`import` / `export`), the modern JavaScript way to split code into files. It is like `using` plus `public` in C#: a file only shares what it `export`s, and another file pulls it in with `import`.
- `scripts`: run with `npm run <name>`. `dev` starts the Vite development server (hot reload). `build` makes the optimized files for production in `dist/`. `lint` runs ESLint (a code checker). `preview` serves the built `dist/` folder locally.
- `dependencies`: packages the app needs at runtime.
  - `react`: the library that builds the UI from components.
  - `react-dom`: draws React components into the browser's page (the DOM).
  - `react-router-dom`: React Router 7, the client-side routing (URL to page).
  - `bootstrap`: Bootstrap 5, a CSS library. We only use its CSS classes, not its JavaScript.
- `devDependencies`: tools needed only while developing or building: Vite (the dev server and bundler), the Vite React plugin, ESLint with React rules, and type definitions that help the editor's IntelliSense.
- The `^` means "this version or any newer minor/patch version". The exact installed versions are locked in `package-lock.json`.

**Committee might ask:**
- *Why Vite and not Create React App?* Create React App is deprecated. Vite starts in under a second and reloads instantly on save.
- *Why plain JavaScript and not TypeScript?* Time. The project has one week of frontend work. The server validates everything anyway, so the risk of untyped data on the client is small. TypeScript is listed as future work.

---

## [vite.config.js](../../mizan-web/vite.config.js)

**What it is:** Settings for the Vite development server. The important part is the proxy that forwards API calls to the backend.

**The code:**

```js
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // The browser only talks to localhost:5173. Vite forwards /api calls to the API,
    // so the browser sees one origin and CORS never comes into play.
    proxy: {
      '/api': 'http://localhost:5199',
    },
  },
})
```

**Line by line:**
- Line 1-2: ES module imports. `defineConfig` just gives the editor IntelliSense for the settings object. `react()` is the plugin that understands JSX (explained below) and supports hot reload.
- Line 5: `export default` makes this object the one thing the file exports. Vite reads it on start.
- Line 8-9: always run on port 5173. `strictPort: true` means "if 5173 is taken, fail instead of silently picking 5174". That keeps the address predictable for `.claude/launch.json` and the demo.
- Line 12-14: the **proxy**. Any request the browser makes to `http://localhost:5173/api/...` is forwarded by Vite to `http://localhost:5199/api/...` (the API's `http` profile in `launchSettings.json`). The answer comes back the same way.
- Why that removes CORS: CORS is a browser rule that blocks a page from reading responses from a **different origin** (a different scheme, host or port) unless that server allows it. Here the browser only ever talks to `localhost:5173`. It never sees port 5199. Same origin, so CORS never applies, and the backend needs no CORS policy.

**Committee might ask:**
- *Why no CORS in the backend?* In development the Vite proxy makes the frontend and API look like one origin. In production you would do the same thing with a reverse proxy (IIS, Nginx), or serve the built React files from the API itself. Only if the frontend were hosted on a separate domain would you add a CORS policy.
- *Does the proxy exist in production?* No. It is part of the Vite dev server only. That is why `client.js` uses relative URLs (`/api/...`): they work with whatever server hosts the page.

---

## [index.html](../../mizan-web/index.html)

**What it is:** The single HTML page of the SPA. The browser downloads it once; React does the rest.

**The code:**

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/mizan.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Mizan</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.jsx"></script>
  </body>
</html>
```

**Line by line:**
- Line 4: UTF-8, so the Arabic name "ميزان" shows correctly.
- Line 5: the tab icon, served from `mizan-web/public/mizan.svg` (files in `public/` are served as they are).
- Line 6: makes the page scale properly on phones (Bootstrap's responsive layout needs it).
- Line 10: an **empty** `div`. React will draw the whole app inside it.
- Line 11: loads `main.jsx` as an ES module. In development Vite translates the JSX on the fly; `npm run build` replaces this with the bundled file.

---

## [main.jsx](../../mizan-web/src/main.jsx)

**What it is:** The entry point, like `Program.cs` for the frontend. It starts React and wraps the app in the things every page needs.

**The code:**

```jsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import 'bootstrap/dist/css/bootstrap.min.css'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './auth/AuthContext.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      {/* Inside the router, so login/logout can navigate between pages */}
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)
```

**Line by line:**
- Line 1-7: imports. `{ StrictMode }` in curly braces is a **named import** (the file exports many things, we take one). `App` without braces is the file's **default export**. Line 4-5 import CSS files: Vite injects them into the page. Bootstrap's CSS comes first, so our own `index.css` can override it.
- Line 9: find the empty `<div id="root">` from `index.html` and tell React to draw into it.
- Line 10-17: this is **JSX**: HTML-like syntax inside JavaScript. `<App />` means "call the `App` component here". JSX is turned into normal function calls at build time. Nested tags wrap each other, like middleware layers in `Program.cs`:
  - `StrictMode`: a development-only checker. It warns about unsafe code and, importantly, **runs every effect twice** in development (mount, unmount, mount again) to expose effects that don't clean up after themselves. This is why the pages cancel their requests in a cleanup function (see `TicketsPage`). It does nothing in a production build.
  - `BrowserRouter`: turns on React Router. It reads the URL from the browser's address bar and lets components change it without a page reload.
  - `AuthProvider`: shares the logged-in user with every component below it (see `AuthContext.jsx`).
- Line 12: `{/* ... */}` is how you write a comment inside JSX.
- Why `AuthProvider` is inside `BrowserRouter`: `AuthProvider` uses `useNavigate()` to go to `/login` on logout, and that hook only works inside a router.

**Committee might ask:**
- *Why do I see every request twice in the Network tab during development?* That is `StrictMode` running effects twice. The first request is cancelled by the cleanup function (it shows as "canceled"). In production it runs once.

---

## [App.jsx](../../mizan-web/src/App.jsx)

**What it is:** The route table: which URL shows which page. It plays the role that `[Route]` attributes and `MapControllers()` play on the backend.

**The code:**

```jsx
import { Navigate, Route, Routes } from 'react-router-dom'
import RequireAuth from './auth/RequireAuth.jsx'
import Layout from './components/Layout.jsx'
import LoginPage from './pages/LoginPage.jsx'
import TicketsPage from './pages/TicketsPage.jsx'
import CreateTicketPage from './pages/CreateTicketPage.jsx'
import TicketDetailsPage from './pages/TicketDetailsPage.jsx'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      {/* Everything inside needs a logged-in user and shares the navbar */}
      <Route element={<RequireAuth><Layout /></RequireAuth>}>
        <Route path="/tickets" element={<TicketsPage />} />
        <Route path="/tickets/new" element={<CreateTicketPage />} />
        <Route path="/tickets/:id" element={<TicketDetailsPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/tickets" replace />} />
    </Routes>
  )
}
```

**Line by line:**
- Line 9: a component is a function. `export default function App()` is the whole component. It takes no inputs and returns JSX.
- Line 11: `<Routes>` looks at the current URL and picks the best matching `<Route>`.
- Line 12: `/login` shows `LoginPage`. It is outside the guard, so anyone can open it.
- Line 15: a **layout route**. It has no `path`, only an `element`. Its child routes render *inside* it. The element is `RequireAuth` wrapping `Layout`: first check the login, then draw the navbar, then draw the child page where `Layout` puts its `<Outlet />`.
- Line 16-18: the three protected pages. `:id` is a **route parameter**, like `{id}` in `[Route("api/tickets/{id}")]`. `TicketDetailsPage` reads it with `useParams()`. React Router ranks routes, so `/tickets/new` wins over `/tickets/:id` for the URL `/tickets/new` even though both could match.
- Line 21: `path="*"` catches any other URL. `<Navigate>` is a component that redirects as soon as it is drawn. `replace` means the bad URL is *replaced* in the browser history instead of added, so Back doesn't return to it.

**Committee might ask:**
- *Where is the URL `/` handled?* By the `*` route: it redirects to `/tickets`. If the user isn't logged in, `RequireAuth` then sends them on to `/login`.

---

## [index.css](../../mizan-web/src/index.css)

**What it is:** The only custom CSS. Bootstrap does almost all the styling.

**The code:**

```css
/* Bootstrap does most of the work; these are the few extras it doesn't cover */

body {
  background-color: #f5f6f8;
}

.login-card {
  max-width: 420px;
}

/* Lets a whole table row act as a link to the ticket */
.row-link {
  cursor: pointer;
}
```

**Line by line:**
- `body`: a light grey background, so the white Bootstrap cards stand out.
- `.login-card`: keeps the login box narrow on wide screens (used in `LoginPage`).
- `.row-link`: shows the hand cursor over ticket rows, because the whole row is clickable (used in `TicketsPage`).

A quick word on **Bootstrap classes**, since they appear everywhere below: Bootstrap gives ready-made CSS classes you put in `className`. Examples: `btn btn-primary` (a blue button), `card` (a white box), `alert alert-danger` (a red message box), `form-control` (a styled input), `d-flex gap-2` (lay children out in a row with a gap), `mb-3` (margin-bottom size 3), `text-muted` (grey text), `badge text-bg-success` (a small green label), `row` / `col-lg-8` (a 12-column grid: 8 of 12 columns on large screens). In JSX you write `className` instead of `class`, because `class` is a reserved word in JavaScript.

---

## [client.js](../../mizan-web/src/api/client.js)

**What it is:** The one place in the frontend that calls the API. Every page goes through `api()`, so the token, JSON, and error handling are written once. Think of it as a tiny typed `HttpClient` wrapper.

**The code (part 1: token store, error type, 401 hook):**

```js
// The ONE place that calls the API. Every page goes through api(), so the token,
// JSON handling and error format are dealt with once instead of on every screen.

const TOKEN_KEY = 'mizan.token'

export const tokenStore = {
  get: () => sessionStorage.getItem(TOKEN_KEY),
  set: (token) => sessionStorage.setItem(TOKEN_KEY, token),
  clear: () => sessionStorage.removeItem(TOKEN_KEY),
}

// Thrown for any non-2xx response. Carries the ProblemDetails the API sends back.
export class ApiError extends Error {
  constructor(status, title, fieldErrors = {}) {
    super(title)
    this.status = status
    // Validation failures (400) list messages per field: { Title: ["..."], ... }
    this.fieldErrors = fieldErrors
  }
}

// Set by AuthContext so a 401 anywhere logs the user out in one place
let onUnauthorized = () => {}
export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler
}

// True when a request was cancelled on purpose (the page closed before the answer came)
export const isAbort = (err) => err?.name === 'AbortError'
```

**Line by line:**
- Line 4: the key under which the JWT is saved in the browser.
- Line 6-10: `tokenStore` is an object with three small functions. `(token) => ...` is an **arrow function**, the JavaScript version of a C# lambda. `sessionStorage` is a key-value store in the browser that lives **per tab** and is wiped when the tab closes. We chose it over `localStorage` (which survives forever) so a forgotten demo session doesn't stay logged in, and the token lives as short a time as possible. Every place that touches the token goes through these three functions.
- Line 13-20: `ApiError` is a custom exception class, like `class ApiError : Exception` in C#. `super(title)` calls the base constructor (like `: base(title)`), so `err.message` is the ProblemDetails `title`. It adds `status` (the HTTP status code) and `fieldErrors` (the `errors` dictionary from a 400 validation response). `fieldErrors = {}` is a default parameter value, like `= null` in C#.
- Line 23-26: a **module-level variable** that holds "what to do on a 401". It starts as a function that does nothing. `AuthContext` calls `setUnauthorizedHandler(logout)` at startup. Why not import `logout` directly? Because `client.js` is plain JavaScript, not a component, so it can't use React hooks like `useNavigate`. The handler pattern lets React plug its logout into the plain module. It is like registering a callback (an event handler) in C#.
- Line 29: `isAbort` tells apart "the request was cancelled on purpose" from a real failure. `err?.name` is **optional chaining**: if `err` is `null` or `undefined`, the result is `undefined` instead of a crash. It is the same as `?.` in C#.

**The code (part 2: the `api()` function, sending the request):**

```js
// signal: lets a page cancel its request when it closes, like CancellationToken on the backend
export async function api(path, { method = 'GET', body, signal } = {}) {
  const headers = { Accept: 'application/json' }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  let response
  try {
    // Relative URL: in development the Vite proxy forwards /api to the backend
    response = await fetch(`/api${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    })
  } catch (err) {
    if (isAbort(err)) throw err
    throw new ApiError(0, 'Cannot reach the server. Is the API running?')
  }
```

**Line by line:**
- Line 32: `async function` works like `async Task<T>` in C#: inside it you can `await`, and calling it returns a **Promise** (JavaScript's `Task`). The second parameter uses **destructuring**: the caller passes one options object like `{ method: 'POST', body }`, and the function unpacks it into three variables. `method = 'GET'` is a default. `= {}` at the end means "if no options object is passed, use an empty one", so `api('/tickets')` works.
- Line 33: start with `Accept: application/json`, so the API knows we want JSON.
- Line 34-35: read the token **once** and remember it in `token`. This copy matters later (line 65). `` `Bearer ${token}` `` is a **template literal**: backticks with `${...}` inside, like C# string interpolation `$"Bearer {token}"`.
- Line 36: only say "the body is JSON" when there is a body. A GET has none.
- Line 41: `fetch` is the browser's built-in HTTP function, like `HttpClient.SendAsync`. It returns a Promise of a `Response`. The URL is **relative** (`/api/...`), so it goes to the same server that served the page; in development that is Vite on 5173, which proxies it to 5199.
- Line 44: `JSON.stringify` turns a JavaScript object into a JSON string (serialize).
- Line 45: `signal` comes from an **AbortController**. A page creates `new AbortController()`, passes `controller.signal` here, and calls `controller.abort()` to cancel. That is exactly the `CancellationTokenSource` / `CancellationToken` pair from C#: the controller is the source, the signal is the token. When aborted, `fetch` throws an error named `AbortError`.
- Line 47-50: `fetch` only throws when there was **no HTTP answer at all** (network down, request cancelled). An HTTP 404 or 500 does *not* throw; it comes back as a normal response. Here: a cancel is re-thrown unchanged so the page can ignore it; anything else becomes an `ApiError` with status `0` and a friendly message.

**The code (part 3: reading the response and turning errors into `ApiError`):**

```js
  // Some endpoints answer with no body, and a proxy error page isn't JSON
  const text = await response.text()
  let data = null
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = null
  }

  if (!response.ok) {
    // An expired or revoked token (or a deactivated user): the session is over.
    // Only if that token is still the current one: a late answer to an old request
    // must not log out a session that started after it.
    if (response.status === 401 && token && token === tokenStore.get()) onUnauthorized()

    // The API always sends a ProblemDetails body. Without one, the Vite proxy
    // couldn't reach the API at all (it isn't running, or is still starting).
    if (!data && response.status >= 500)
      throw new ApiError(response.status, 'Cannot reach the server. Is the API running?')

    throw new ApiError(
      response.status,
      data?.title ?? `Request failed (${response.status})`,
      data?.errors ?? {},
    )
  }

  return data
}
```

**Line by line:**
- Line 53-59: read the body as text first, then try to parse it. Why not call `response.json()` directly? Because it throws on an empty body, and some answers have none, and an error page from the proxy is HTML, not JSON. This way `data` is either the parsed JSON or `null`, never a crash. `JSON.parse` is deserialize.
- Line 61: `response.ok` is true for status 200-299.
- Line 65: the **401 rule**. A 401 means the token is expired, invalid, or the user was deactivated (the backend reloads the user on every ticket call). Then the session is over, so call `onUnauthorized()` (which is `logout`). But only if three things hold: it is a 401, the request *had* a token, and that token is *still the current one* in storage. Why the last check: imagine an old request that was sent before a logout. If its 401 answer arrives after the user has logged in again as someone else, it must not log out the new session. The `token` copy from line 34 is what makes this comparison possible. It also means a wrong password on the login call (which is sent without a token) never triggers a logout.
- Line 69-70: the API **always** answers errors with a ProblemDetails JSON body (`GlobalExceptionHandler`, chapter 5). So a 5xx with **no** JSON body did not come from our API: it is the Vite proxy saying "I couldn't reach localhost:5199" (the API is stopped or still starting). We show the same friendly "Cannot reach the server" message as in line 49.
- Line 72-76: every other error becomes an `ApiError` with the HTTP status, the ProblemDetails `title` as the message, and the `errors` dictionary for validation failures. `??` is the **null-coalescing** operator, same as C#: use the right side if the left is `null` or `undefined`. So if there is no title (for example a 401 from the JWT middleware, which may have an empty body), the message becomes `Request failed (401)`.
- Line 79: success: return the parsed JSON (or `null` for an empty body). Because the function is `async`, the caller gets it through `await` or `.then()`.

**Committee might ask:**
- *Why one wrapper instead of calling fetch in each page?* So the token header, JSON parsing, error format and 401 logout are written once. A page only says `api('/tickets')` and catches `ApiError`.
- *Why sessionStorage and not a cookie?* The backend issues a bearer token in JSON (no refresh tokens, by scope). Storing it in sessionStorage keeps it per tab and short-lived. A script running in the page could read it (an XSS risk), which is why the app never renders user input as HTML (React escapes text by default) and checks links before making them clickable. HttpOnly cookies are future work.
- *What happens if the API is down?* Either `fetch` throws (line 49) or the proxy answers 5xx with no ProblemDetails (line 70). Both show "Cannot reach the server. Is the API running?".

---

## [AuthContext.jsx](../../mizan-web/src/auth/AuthContext.jsx)

**What it is:** Keeps "who is logged in" and shares it with every component, plus the `login` and `logout` functions. It is the frontend's version of a scoped service you inject everywhere.

**The code (part 1: state and logout):**

```jsx
/* eslint-disable react-refresh/only-export-components -- the provider and its hook belong together */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, setUnauthorizedHandler, tokenStore } from '../api/client.js'

const AuthContext = createContext(null)

// Shares "who is logged in" with every component, without passing it down by hand
export function AuthProvider({ children }) {
  const navigate = useNavigate()
  const [user, setUser] = useState(null)
  // True until we know whether a saved token is still good, so pages don't flash the login screen
  const [checking, setChecking] = useState(() => tokenStore.get() !== null)

  const logout = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    navigate('/login', { replace: true })
  }, [navigate])

  useEffect(() => {
    setUnauthorizedHandler(logout)
  }, [logout])
```

**Line by line:**
- Line 1: turns off one ESLint rule for this file. The rule wants a file to export only components (for hot reload). Here the provider and its `useAuth` hook belong together, so we keep them in one file on purpose.
- Line 6: **`createContext`** creates a "channel" that any component below the provider can read, without passing the value through every layer by hand. `null` is the default value if no provider is found.
- Line 9: `AuthProvider` is a component. `{ children }` is **props destructuring**. **Props** are a component's inputs, like constructor parameters; they are passed as attributes in JSX (`<RouteForm busy={busy} />`). `children` is a special prop: whatever is written *between* the opening and closing tags. In `main.jsx` that is `<App />`.
- Line 10: **`useNavigate`** is a React Router hook that returns a function to change the URL from code. A **hook** is a special React function whose name starts with `use`. Hooks may only be called at the top of a component, never inside `if` or loops, because React identifies them by their order.
- Line 11: **`useState`** gives a component memory that survives redraws. It returns a pair: the current value and a setter. `const [user, setUser] = ...` is **array destructuring** of that pair. Calling `setUser(x)` stores the new value and tells React to redraw the component. You never assign `user = x` directly; React would not notice.
- Line 13: `checking` starts as `true` only if a token is already saved (after a page refresh). Passing a function `() => ...` to `useState` means "compute the first value once", not on every redraw. While `checking` is true, `RequireAuth` shows "Loading…" instead of flashing the login page.
- Line 15-19: `logout` clears the token, forgets the user and goes to `/login`. `{ replace: true }` replaces the current history entry, so Back doesn't return to a protected page. **`useCallback`** keeps the *same function object* between redraws; it only makes a new one when something in its **dependency array** (`[navigate]`) changes. Why care: in JavaScript a function defined inside a component is a new object on every redraw, and other hooks (line 21-23, line 55) compare by reference. Without `useCallback`, they would see a "change" on every redraw.
- Line 21-23: **`useEffect`** runs code *after* the component is drawn, for side effects (calls to the API, timers, wiring outside code). The **dependency array** `[logout]` says when to run it again: only when `logout` changes. Here it plugs our `logout` into `client.js`, so a 401 anywhere logs the user out in one place.

**The code (part 2: the `/me` check after a refresh):**

```jsx
  // After a page refresh, ask the API who the saved token belongs to.
  // /me reads the user from the database, so a deactivated user is logged out here
  // (a 401 goes through the handler above).
  useEffect(() => {
    if (!tokenStore.get()) return

    const controller = new AbortController()
    api('/auth/me', { signal: controller.signal })
      .then(setUser)
      .catch(() => {
        // Any other failure (API down or restarting): keep the token, so the next refresh
        // still works, and let the guard send the user to the login page for now
      })
      .finally(() => {
        if (!controller.signal.aborted) setChecking(false)
      })

    // StrictMode runs effects twice in development: cancel the first call instead of sending two
    return () => controller.abort()
  }, [])
```

**Line by line:**
- Why this exists: after F5, the React state is gone (memory is wiped), but the token is still in sessionStorage. We have the token but don't know who it belongs to, or whether it is still valid.
- Line 28 and 44: an effect with an **empty dependency array** `[]` runs once, when the component first appears (in development, StrictMode runs it twice; see below).
- Line 29: no saved token, nothing to check. (`checking` was already `false` in that case.)
- Line 31-32: create an AbortController and call `GET /api/auth/me` with its signal.
- Line 33-40: **Promise chaining**, the non-`await` way to handle async results. `.then(fn)` runs when it succeeds (like the code after `await`), `.catch(fn)` when it fails (like `catch`), `.finally(fn)` always (like `finally`). `.then(setUser)` passes the result straight into `setUser`, so the user object returned by `/me` becomes the logged-in user.
- Line 34-37: errors are deliberately ignored here. A 401 has already been handled inside `api()` (logout). Any other error (API down) keeps the token, so the next refresh can try again; since `user` is still `null`, the guard sends the user to `/login` for now.
- Line 38-40: stop showing "Loading…", unless this call was cancelled.
- Line 43: the **cleanup function**. If an effect returns a function, React calls it when the component goes away, or before running the effect again. Here it aborts the request. Why this matters: in development, StrictMode mounts, unmounts and mounts again. Run 1 starts a request, cleanup aborts it, run 2 starts a fresh one. The aborted run 1 skips `setChecking(false)` (line 39), so only run 2 decides. Without the cleanup, two `/me` calls would race.

**The code (part 3: login and the shared value):**

```jsx
  const login = useCallback(async (email, password) => {
    // A leftover token must not ride along on the login call
    tokenStore.clear()
    const result = await api('/auth/login', { method: 'POST', body: { email, password } })
    tokenStore.set(result.accessToken)
    setUser(result.user)
  }, [])

  // The same object between renders unless something changed, so pages don't redraw for nothing
  const value = useMemo(() => ({ user, checking, login, logout }), [user, checking, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  return useContext(AuthContext)
}
```

**Line by line:**
- Line 46-52: `login` posts the email and password to `/api/auth/login`. `{ email, password }` is shorthand for `{ email: email, password: password }`. On success it saves the JWT (`accessToken`) and the user from the response. If the call fails, `api()` throws an `ApiError` and `login` lets it bubble up to `LoginPage`, which shows the message.
- Line 48: clear any old token first, so it is not sent with the login request (and so a 401 for a wrong password can't trigger a logout; see `client.js` line 65).
- Line 55: **`useMemo`** caches a computed value and only recomputes it when a dependency changes. Here it builds the object every component reads. Why: when the context value is a new object, *every* component using `useAuth()` redraws. With `useMemo` the object only changes when `user`, `checking`, `login` or `logout` really change. That is also why `login` and `logout` are wrapped in `useCallback`: otherwise they would be new each time and break the memo.
- Line 57: `AuthContext.Provider` makes `value` available to everything inside it (`children`).
- Line 60-62: **`useContext`** reads the nearest provider's value. `useAuth` is a **custom hook**: a normal function whose name starts with `use` and which calls other hooks. Any component can write `const { user, logout } = useAuth()` (object destructuring) instead of knowing about the context object.

**Committee might ask:**
- *Is the role in the frontend trusted?* No. It only decides what to show. The backend reloads the user and role from the database on every ticket request.
- *What happens on F5?* The token stays in sessionStorage. `checking` starts `true`, the app shows "Loading…", calls `/me`, gets the user back and shows the page. If the user was deactivated, `/me` returns 401 and the app logs out.
- *Why `useCallback` and `useMemo`?* To keep the same objects between redraws, so components that use the auth context don't redraw when nothing changed. It is a small optimization, but it also keeps the `useEffect` on line 21 from re-running every redraw.

---

## [RequireAuth.jsx](../../mizan-web/src/auth/RequireAuth.jsx)

**What it is:** A guard around the logged-in pages. It is the frontend look-alike of `[Authorize]`, but only for display.

**The code:**

```jsx
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext.jsx'

// Wraps pages that need a login. This only decides what to SHOW:
// the API still checks the token on every request, so hiding a page is not the security.
export default function RequireAuth({ children }) {
  const { user, checking } = useAuth()
  const location = useLocation()

  if (checking) {
    return <div className="text-center text-muted mt-5">Loading…</div>
  }

  if (!user) {
    // Remember where they were going, so login can send them back there
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return children
}
```

**Line by line:**
- Line 7: read `user` and `checking` from the auth context.
- Line 8: **`useLocation`** returns the current URL (path, query string, and any extra `state`).
- Line 10-12: **conditional rendering** with a plain `if`: a component can return different JSX depending on its data. While the `/me` check runs, show "Loading…".
- Line 14-17: not logged in: redirect to `/login`. `state={{ from: ... }}` attaches hidden data to the navigation (not in the URL). The double braces are JSX `{ }` holding a JavaScript object `{ from: ... }`. `LoginPage` reads `location.state.from` to send the user back to the page they wanted, for example a ticket link.
- Line 19: logged in: draw what was wrapped (`<Layout />` in `App.jsx`).

**Committee might ask:**
- *Can a user bypass this by editing the JavaScript?* Yes, they can see an empty page. But every API call still needs a valid JWT, and the server decides what data they get. This guard is for user experience, not security. The comment on line 4-5 says that.

---

## [Layout.jsx](../../mizan-web/src/components/Layout.jsx)

**What it is:** The navbar shown above every logged-in page. It works like `_Layout.cshtml` in MVC, with `<Outlet />` playing the role of `@RenderBody()`.

**The code:**

```jsx
import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import { roleLabel } from '../utils/labels.js'

// The navbar around every logged-in page. <Outlet /> is where the current page renders.
export default function Layout() {
  const { user, logout } = useAuth()

  return (
    <>
      <nav className="navbar navbar-dark bg-primary mb-4">
        <div className="container">
          <Link className="navbar-brand fw-semibold" to="/tickets">
            Mizan <span className="opacity-75">ميزان</span>
          </Link>
          <div className="d-flex align-items-center gap-3 text-white">
            <span>
              {user.name}{' '}
              <span className="badge text-bg-light">{roleLabel(user.role)}</span>
            </span>
            <button className="btn btn-outline-light btn-sm" onClick={logout}>
              Log out
            </button>
          </div>
        </div>
      </nav>

      <main className="container pb-5">
        <Outlet />
      </main>
    </>
  )
}
```

**Line by line:**
- Line 10 and 31: `<>...</>` is a **fragment**: a wrapper that adds no HTML element. A component must return one root, and a fragment lets us return two siblings (`nav` and `main`).
- Line 13: **`Link`** is React Router's version of `<a>`. It changes the URL without reloading the page.
- Line 18: `{user.name}`: curly braces in JSX insert a JavaScript value. React escapes it as text, so a name containing `<script>` is shown as text, never run. `{' '}` adds a space, because JSX drops whitespace at line ends. `user` is never `null` here, because `RequireAuth` only draws `Layout` when a user exists.
- Line 19: the role from the API is `"TeamLeader"`; `roleLabel` shows "Team Leader".
- Line 21: `onClick={logout}` passes the function itself (no parentheses), so React calls it on click. Writing `onClick={logout()}` would call it immediately during drawing.
- Line 29: **`Outlet`** is where the matching child route (`TicketsPage`, `CreateTicketPage` or `TicketDetailsPage`) is drawn.

---

## [LoginPage.jsx](../../mizan-web/src/pages/LoginPage.jsx)

**What it is:** The login form, plus buttons that fill in the demo accounts so switching roles during the presentation is one click.

**The code (part 1: state and submit):**

```jsx
import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

// The seeded demo accounts (see DbSeeder.cs). Clicking one fills the form,
// so switching roles during the presentation takes one click.
const demoAccounts = [
  { label: 'Client', email: 'client@mizan.local' },
  { label: 'Teams Manager', email: 'manager@mizan.local' },
  { label: 'Team Leader', email: 'leader@mizan.local' },
  { label: 'Member One', email: 'member1@mizan.local' },
  { label: 'Member Two', email: 'member2@mizan.local' },
]

export default function LoginPage() {
  const { user, login } = useAuth()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Logged in (already, or just now): go back to the page they wanted, or the ticket list.
  // This one line handles the redirect after a successful login too.
  if (user) return <Navigate to={location.state?.from ?? '/tickets'} replace />

  async function handleSubmit(event) {
    event.preventDefault() // Stop the browser from reloading the page
    setError(null)
    setSubmitting(true)
    try {
      await login(email, password)
    } catch (err) {
      setError(err.message)
      setSubmitting(false)
    }
  }
```

**Line by line:**
- Line 7-13: a constant list of the seeded accounts, outside the component, so it is created once. Only the emails are here. The password is **not** in the frontend; the user types it (the hint on screen says it comes from the seeder). The admin account is left out because there are no admin screens (cut from scope).
- Line 19-22: four pieces of state: the two input values, the error message, and whether a login is in progress.
- Line 26: if a user exists, redirect. This covers two cases with one line: someone opens `/login` while already logged in, and the moment right after a successful login (`login()` calls `setUser`, React redraws this page, and now `user` is set). `location.state?.from` is the page `RequireAuth` remembered; `?.` handles the case where there is no state, and `??` falls back to `/tickets`. Note: this early `return` comes after all the hooks, which is required (hooks must always run in the same order).
- Line 28-38: the submit handler. **`event.preventDefault()`**: an HTML form's default action is to send the data and reload the page. In an SPA that would wipe all state, so we stop it and handle the submit in JavaScript. Then: clear the old error, disable the button, `await login(...)`. On failure show the `ApiError` message (for example the API's "invalid email or password" title) and enable the button again. On success there is nothing to do: line 26 handles the redirect on the next redraw.

**The code (part 2: the form):**

```jsx
  return (
    <div className="container d-flex justify-content-center align-items-center min-vh-100">
      <div className="card shadow-sm w-100 login-card">
        <div className="card-body p-4">
          <h1 className="h3 text-center mb-1">Mizan <span className="text-primary">ميزان</span></h1>
          <p className="text-center text-muted mb-4">Code review requests, weighed before they ship</p>

          {error && <div className="alert alert-danger py-2">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="mb-3">
              <label htmlFor="email" className="form-label">Email</label>
              <input
                id="email"
                type="email"
                className="form-control"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
              />
            </div>
            <div className="mb-3">
              <label htmlFor="password" className="form-label">Password</label>
              <input
                id="password"
                type="password"
                className="form-control"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
            <button type="submit" className="btn btn-primary w-100" disabled={submitting}>
              {submitting ? 'Signing in…' : 'Sign in'}
            </button>
          </form>

          <hr />
          <p className="small text-muted mb-2">Demo accounts (password from the seeder):</p>
          <div className="d-flex flex-wrap gap-2">
            {demoAccounts.map((account) => (
              <button
                key={account.email}
                type="button"
                className="btn btn-outline-secondary btn-sm"
                onClick={() => setEmail(account.email)}
              >
                {account.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
```

**Line by line:**
- Line 47: **conditional rendering with `&&`**: `{error && <div>...</div>}` draws the alert only when `error` has a value. If `error` is `null`, React draws nothing. It reads as "if error, then show this".
- Line 49: `onSubmit={handleSubmit}` runs on the Submit button or Enter.
- Line 52-60: a **controlled input**: React state is the single source of truth. `value={email}` makes the input show the state, and `onChange` copies every keystroke back into state with `setEmail(e.target.value)`. `e.target` is the input element. Because state drives the input, a demo button can fill it with one `setEmail` call (line 86).
- Line 51 and 53: `htmlFor` is JSX for the HTML `for` attribute (again because `for` is a JavaScript word). It ties the label to the input.
- Line 58: `required` is a browser check: the form won't submit with an empty field.
- Line 73-75: the button is disabled while logging in, so a second click can't send a second request. The text uses a **ternary** (`condition ? a : b`), the same as in C#.
- Line 81-90: **rendering a list with `.map`**: `.map` turns each item of an array into something else, like LINQ `Select`. Here each account becomes a `<button>`. The **`key`** prop gives each item a stable, unique id, so React can tell which item is which when the list changes. The email is unique, so it is the key.
- Line 85: `type="button"` matters: a button inside or near a form defaults to `submit`. These are outside the `<form>`, but saying it explicitly avoids accidents.
- Line 86: `onClick={() => setEmail(account.email)}` wraps the call in an arrow function so it runs on click, not during drawing.

**Committee might ask:**
- *Why is there no registration screen?* Out of scope for the MVP. Users are seeded (`DbSeeder.cs`). Registration and admin screens are future work.
- *Is it safe to show the demo emails?* They are seeded demo accounts for the presentation. The password is not shown or stored in the frontend. In a real deployment these buttons would be removed.

---

## [TicketsPage.jsx](../../mizan-web/src/pages/TicketsPage.jsx)

**What it is:** The ticket list. It loads `GET /api/tickets` and shows a table. The server already returns only the tickets this user may see.

**The code (part 1: loading):**

```jsx
import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, isAbort } from '../api/client.js'
import { useAuth } from '../auth/AuthContext.jsx'
import { formatDate, priorityColor, splitWords, statusColor } from '../utils/labels.js'

export default function TicketsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()

  const [tickets, setTickets] = useState(null) // null = still loading
  const [error, setError] = useState(null)

  useEffect(() => {
    // The API already filters by role (TicketVisibility), so we show whatever comes back
    const controller = new AbortController()
    api('/tickets', { signal: controller.signal })
      .then(setTickets)
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })

    // Leaving the page cancels the request, so a late answer can't update a closed page
    return () => controller.abort()
  }, [])
```

**Line by line:**
- Line 11: `tickets` has three meanings: `null` = still loading, `[]` = loaded but empty, a filled array = show the table.
- Line 14-25: the standard "load on open" pattern used across the app. An effect with `[]` runs when the page appears. It creates an AbortController (the frontend's `CancellationTokenSource`), passes the signal to `api()`, stores the result with `setTickets`, and shows any error except a cancel. The cleanup function aborts the request when the page closes (or between the two StrictMode runs in development), so a late answer can't update a page that is no longer on screen.
- Line 15: there is no role filtering in the frontend. The backend's `TicketVisibility` rule (chapter 2) decides which tickets come back.

**The code (part 2: the table):**

```jsx
  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Tickets</h1>
        {/* Only clients create tickets. Hiding the button is for convenience; the API enforces it. */}
        {user.role === 'Client' && (
          <button className="btn btn-primary" onClick={() => navigate('/tickets/new')}>
            New ticket
          </button>
        )}
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {!error && tickets === null && <p className="text-muted">Loading tickets…</p>}

      {tickets?.length === 0 && (
        <div className="card card-body text-center text-muted">No tickets to show yet.</div>
      )}

      {tickets?.length > 0 && (
        <div className="card shadow-sm">
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th>Title</th>
                  <th>Status</th>
                  <th>Priority</th>
                  <th>Client</th>
                  <th>Team</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {tickets.map((ticket) => (
                  <tr key={ticket.id} className="row-link" onClick={() => navigate(`/tickets/${ticket.id}`)}>
                    {/* A real link too, so keyboard users and middle-click work */}
                    <td className="fw-medium">
                      <Link to={`/tickets/${ticket.id}`} className="text-reset text-decoration-none">
                        {ticket.title}
                      </Link>
                    </td>
                    <td>
                      <span className={`badge text-bg-${statusColor(ticket.status)}`}>
                        {splitWords(ticket.status)}
                      </span>
                    </td>
                    <td>
                      <span className={`badge text-bg-${priorityColor(ticket.priority)}`}>{ticket.priority}</span>
                    </td>
                    <td>{ticket.clientName}</td>
                    <td>{ticket.teamName ?? <span className="text-muted">Not routed</span>}</td>
                    <td className="text-nowrap">{formatDate(ticket.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </>
  )
}
```

**Line by line:**
- Line 32-36: the "New ticket" button is shown only to clients. The comment states the rule of this whole frontend: hiding is convenience; the API returns 403 to anyone else.
- Line 33: `navigate('/tickets/new')` changes the page from code.
- Line 41-47: four mutually exclusive states drawn with `&&`: error, loading, empty, table. `tickets?.length` uses optional chaining because `tickets` is `null` while loading; `null?.length` is `undefined`, which is neither `0` nor `> 0`, so neither block shows.
- Line 62-63: one `<tr>` per ticket with `key={ticket.id}` (the Guid from the API). Clicking anywhere on the row opens the ticket.
- Line 65-69: the title is also a real `<Link>`, so keyboard users can Tab to it and middle-click opens a new tab. A click on the link also bubbles to the row's `onClick`, so both navigate to the same URL. That is harmless.
- Line 71: `className` built with a template literal: status `"InProgress"` gives `badge text-bg-primary`.
- Line 79: `??` again: if the ticket has no team yet, show grey "Not routed".

---

## [CreateTicketPage.jsx](../../mizan-web/src/pages/CreateTicketPage.jsx)

**What it is:** The new-ticket form at `/tickets/new`, for clients. It posts to `POST /api/tickets` and shows the server's validation messages under each field.

**The code (part 1: state and the role check):**

```jsx
import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api/client.js'
import { useAuth } from '../auth/AuthContext.jsx'
import { fieldError, summarizeError } from '../utils/fieldErrors.js'

const priorities = ['Low', 'Medium', 'High', 'Critical']

const emptyForm = {
  title: '',
  description: '',
  repositoryUrl: '',
  branch: 'main',
  commitSha: '',
  priority: 'Medium',
}

export default function CreateTicketPage() {
  const { user } = useAuth()
  const navigate = useNavigate()

  const [form, setForm] = useState(emptyForm)
  const [error, setError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)

  // Only clients create tickets. The API refuses anyone else with 403; this just explains it.
  if (user.role !== 'Client') {
    return (
      <div className="card card-body text-center">
        <p className="mb-2">Only clients can create tickets.</p>
        <Link to="/tickets">Back to tickets</Link>
      </div>
    )
  }
```

**Line by line:**
- Line 7: the priority names match the backend `Priority` enum (sent as strings, because the API uses `JsonStringEnumConverter`).
- Line 9-16: the starting values. One object holds every field, with the same names as the API's create request (camelCase in JSON). `branch` defaults to `main`, `priority` to `Medium`.
- Line 22: the whole form is one piece of state, instead of six `useState` calls.
- Line 24: `fieldErrors` holds the API's `errors` dictionary after a 400.
- Line 26: **`useRef`** gives a box (`submitting.current`) that keeps its value between redraws but, unlike state, changing it does **not** redraw and takes effect **immediately**. It is like a private field on a class. Used below against double submits.
- Line 29-36: a non-client who types the URL gets an explanation instead of a form. The server would refuse with 403 anyway.

**The code (part 2: change and submit handlers):**

```jsx
  // One change handler for every field: the input's name says which value to update
  function handleChange(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
  }

  async function handleSubmit(event) {
    event.preventDefault()
    // A second click before React redraws the disabled button would send a second ticket
    if (submitting.current) return
    submitting.current = true

    setError(null)
    setFieldErrors({})
    setSaving(true)
    try {
      // Pasted values often carry a stray space or newline, which would fail the SHA/URL checks
      const body = Object.fromEntries(Object.entries(form).map(([k, v]) => [k, v.trim()]))
      const ticket = await api('/tickets', { method: 'POST', body })
      // replace: pressing Back afterwards shouldn't land on an empty form and invite a duplicate
      navigate(`/tickets/${ticket.id}`, { replace: true })
    } catch (err) {
      // A 400 lists what's wrong per field: those show under each input, the rest in the alert
      setFieldErrors(err.fieldErrors ?? {})
      setError(summarizeError(err, Object.keys(emptyForm)))
      setSaving(false)
      submitting.current = false
    }
  }
```

**Line by line:**
- Line 39-42: one handler for all inputs. Each input has a `name` equal to its key in `form`. `const { name, value } = event.target` destructures two properties of the input. `{ ...current, [name]: value }` uses the **spread operator** `...` to copy all old fields into a new object, then overwrites one: `[name]` is a computed key, so for the title input it means `title: value`. We make a **new** object instead of changing the old one, because React only notices a change when the reference changes (like replacing a C# `record` with `with { }`). `setForm((current) => ...)` passes a function, so it always works on the latest value.
- Line 47-48: the double-click guard. `setSaving(true)` disables the button, but only at the **next** redraw. Two fast clicks can both arrive before that. The ref changes instantly, so the second click returns at line 47. This prevents creating two tickets.
- Line 55: **trimming input**. `Object.entries(form)` turns the object into `[key, value]` pairs, `.map` trims each value, `Object.fromEntries` turns it back into an object. A SHA copied from GitHub often has a trailing space or newline, which would fail the server's hex check.
- Line 56: `POST /api/tickets`. The API answers with the created ticket (including its `id`).
- Line 58: go to the new ticket's details page with **replace navigation**: the form page is *replaced* in history. If the user presses Back, they go to the list, not to a filled form they might submit again.
- Line 59-65: on error, store the per-field errors (shown under inputs) and a summary for the top alert, then allow submitting again. `err.fieldErrors ?? {}` because an error with status 0 still has `{}`, but this guards anything unexpected.
- Line 62: `Object.keys(emptyForm)` is the list of fields this form shows: `title`, `description`, ... `summarizeError` matches them case-insensitively against the server's keys (which may be `Title`).

**The code (part 3: field helpers and the form):**

```jsx
  // Bootstrap marks a field red with "is-invalid"; the message goes in "invalid-feedback" below it
  const invalid = (name) => (fieldError(fieldErrors, name) ? ' is-invalid' : '')
  const feedback = (name) =>
    fieldError(fieldErrors, name) && <div className="invalid-feedback">{fieldError(fieldErrors, name)}</div>

  return (
    <div className="row justify-content-center">
      <div className="col-lg-8">
        <Link to="/tickets" className="small text-decoration-none">← Back to tickets</Link>
        <h1 className="h4 mt-2 mb-3">New ticket</h1>

        <div className="card shadow-sm">
          <div className="card-body">
            {error && <div className="alert alert-danger py-2">{error}</div>}

            {/* The browser checks only required + length. The real rules (URL format, hex SHA)
                live on the server, and its messages appear under each field. */}
            <form onSubmit={handleSubmit}>
              <div className="mb-3">
                <label htmlFor="title" className="form-label">Title</label>
                <input id="title" name="title" className={`form-control${invalid('Title')}`}
                  value={form.title} onChange={handleChange} required maxLength={200} />
                {feedback('Title')}
              </div>

              <div className="mb-3">
                <label htmlFor="description" className="form-label">What needs fixing?</label>
                <textarea id="description" name="description" rows={5}
                  className={`form-control${invalid('Description')}`}
                  value={form.description} onChange={handleChange} required maxLength={4000} />
                {feedback('Description')}
              </div>

              <div className="mb-3">
                <label htmlFor="repositoryUrl" className="form-label">GitHub repository</label>
                <input id="repositoryUrl" name="repositoryUrl" placeholder="https://github.com/owner/repo"
                  className={`form-control${invalid('RepositoryUrl')}`}
                  value={form.repositoryUrl} onChange={handleChange} required maxLength={500} />
                {feedback('RepositoryUrl')}
              </div>

              <div className="row">
                <div className="col-md-4 mb-3">
                  <label htmlFor="branch" className="form-label">Branch</label>
                  <input id="branch" name="branch" className={`form-control${invalid('Branch')}`}
                    value={form.branch} onChange={handleChange} required maxLength={200} />
                  {feedback('Branch')}
                </div>
                <div className="col-md-5 mb-3">
                  <label htmlFor="commitSha" className="form-label">Commit SHA (the "before" code)</label>
                  <input id="commitSha" name="commitSha" placeholder="e.g. a1b2c3d"
                    className={`form-control font-monospace${invalid('CommitSha')}`}
                    value={form.commitSha} onChange={handleChange} required maxLength={64} />
                  {feedback('CommitSha')}
                </div>
                <div className="col-md-3 mb-3">
                  <label htmlFor="priority" className="form-label">Priority</label>
                  <select id="priority" name="priority" className={`form-select${invalid('Priority')}`}
                    value={form.priority} onChange={handleChange}>
                    {priorities.map((p) => <option key={p}>{p}</option>)}
                  </select>
                  {feedback('Priority')}
                </div>
              </div>

              <div className="d-flex justify-content-end gap-2">
                <Link to="/tickets" className="btn btn-outline-secondary">Cancel</Link>
                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? 'Submitting…' : 'Submit ticket'}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
```

**Line by line:**
- Line 69: `invalid('Title')` returns `' is-invalid'` when the server sent an error for `Title`, else `''`. It is glued onto `form-control`, and Bootstrap draws the field with a red border.
- Line 70-71: `feedback('Title')` returns the message in a `div.invalid-feedback` (Bootstrap shows it in red under the field), or nothing. It uses `&&` the same way as JSX conditional rendering.
- Line 83-84: the browser only checks `required` and `maxLength`. The real rules (URL must be `https://github.com/...`, SHA must be hex) live **once**, on the server (chapter 2), and their messages are shown here per field. No duplicate validation logic to keep in sync.
- Line 88-90 (and the same pattern below): each input is controlled (`value` + `onChange`), has a `name` for `handleChange`, and shows its error with `invalid` / `feedback`. The `maxLength` values match the column lengths in the EF configurations (Title 200, Description 4000, RepositoryUrl 500, Branch 200, CommitSha 64).
- Line 127: the `<select>` options come from `priorities.map`. An `<option>` with no `value` uses its text as the value.
- Line 134: Cancel is a `Link` styled as a button.

**Committee might ask:**
- *Why not validate the URL and SHA in React too?* To keep one source of truth. The server must validate anyway (anyone can call the API without the UI). The UI shows the server's messages next to each field, so the user still gets a clear answer.
- *How do you stop double submission?* A `useRef` flag that changes instantly (state only takes effect on the next redraw), plus the disabled button. And `replace` navigation so Back doesn't return to the filled form.

---

## [TicketDetailsPage.jsx](../../mizan-web/src/pages/TicketDetailsPage.jsx)

**What it is:** The most important page. It shows one ticket (details, code snapshots, responses), the action buttons the server allows, and the history. Every workflow click goes through its `handleAction`.

**The code (part 1: helpers outside the component):**

```jsx
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, isAbort } from '../api/client.js'
import ActionsPanel from '../components/ticket/ActionsPanel.jsx'
import HistoryList from '../components/ticket/HistoryList.jsx'
import { workflowActions } from '../workflow/actions.js'
import { summarizeError } from '../utils/fieldErrors.js'
import { formatDate, priorityColor, splitWords, statusColor } from '../utils/labels.js'

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// Fields with an input that shows its own error (submit-fix form); TeamId, MemberIds etc. go in the alert
const actionFormFields = ['Branch', 'CommitSha', 'Summary']

// "https://github.com/owner/repo/" + sha → a link to that exact commit on GitHub
const commitUrl = (repo, sha) => `${repo.replace(/\/$/, '')}/commit/${sha}`

// The server already only accepts https://github.com/... URLs. Checking again before
// making it a link means a bad value could never become a clickable script link.
const isSafeLink = (url) => url.startsWith('https://')
```

**Line by line:**
- Line 10: a regular expression for a Guid. The `i` flag ignores case. Used to reject URLs like `/tickets/abc` without calling the API (the backend route has `{id:guid}`, so such a call would 404 anyway).
- Line 13: the field names that have an input able to show their own error. Only the submit-fix form has those (branch, SHA, summary). Errors for any other key, such as `TeamId` from the route form or `MemberIds` from the assign form, go in the alert above the actions, so no message is ever lost.
- Line 16: builds a GitHub commit link. `replace(/\/$/, '')` removes one trailing slash so the URL doesn't get `//commit`.
- Line 20: **https-only links**. A URL like `javascript:alert(1)` placed in `href` would run code when clicked. The server already only accepts `https://github.com/...` URLs, but checking again here means a bad value from anywhere can never become a clickable script link. Defense in depth.

**The code (part 2: state, refs, and the two loaders):**

```jsx
export default function TicketDetailsPage() {
  const { id } = useParams()
  const validId = guidPattern.test(id)

  const [ticket, setTicket] = useState(null)
  const [history, setHistory] = useState(null)
  const [historyError, setHistoryError] = useState(null)
  const [loadError, setLoadError] = useState(null)

  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState({})

  // Which ticket this page shows right now. An answer for an older id is ignored,
  // so a slow response can never put ticket A's data on ticket B's page.
  const currentId = useRef(id)
  // Cancels this page's loads when the ticket changes or the page closes
  const pageLoads = useRef(null)
  // Blocks a second click before React has redrawn the buttons as disabled
  const running = useRef(false)

  const loadTicket = useCallback((ticketId, signal) =>
    api(`/tickets/${ticketId}`, { signal })
      .then((data) => {
        if (currentId.current === ticketId) setTicket(data)
      })
      .catch((err) => {
        if (!isAbort(err) && currentId.current === ticketId) setLoadError(err)
      }), [])

  const loadHistory = useCallback((ticketId, signal) =>
    api(`/tickets/${ticketId}/history`, { signal })
      .then((data) => {
        if (currentId.current === ticketId) {
          setHistory(data)
          setHistoryError(null)
        }
      })
      .catch((err) => {
        if (!isAbort(err) && currentId.current === ticketId) setHistoryError(err.message)
      }), [])
```

**Line by line:**
- Line 23: **`useParams`** reads route parameters. For `/tickets/:id`, it returns `{ id: '...' }`.
- Line 26-33: state for the ticket, the history, their errors, whether an action is running (`busy`), and the action's errors.
- Line 37: **`currentId` ref**. React Router reuses this same page component when you go from `/tickets/A` to `/tickets/B` (only `id` changes). A slow answer for A could arrive after you are on B. Every loader checks `currentId.current === ticketId` before saving, so an answer for another ticket is ignored. A ref is used (not state) because the loaders need the latest value instantly, and changing it must not cause a redraw.
- Line 39: `pageLoads` holds the AbortController of the current page load, so later reloads (after an action) can use the same signal and be cancelled when the page closes or the ticket changes.
- Line 41: `running` ref, the double-click guard (same idea as `submitting` in `CreateTicketPage`).
- Line 43-50: `loadTicket` calls `GET /api/tickets/{id}` and stores the result, unless the request was cancelled or is for another ticket. Note it stores the whole error object in `loadError`, because the page needs `status` (404 vs other). Wrapped in `useCallback` with `[]`, so it is the same function forever; that keeps the effect below from re-running.
- Line 52-62: `loadHistory` does the same for `GET /api/tickets/{id}/history`. A history error is shown only in the history card, not over the whole page.

**The code (part 3: load when the id changes):**

```jsx
  useEffect(() => {
    currentId.current = id
    setTicket(null)
    setHistory(null)
    setHistoryError(null)
    setLoadError(null)
    setActionError(null)
    setFieldErrors({})
    if (!validId) return

    const controller = new AbortController()
    pageLoads.current = controller
    loadTicket(id, controller.signal)
    loadHistory(id, controller.signal)
    return () => controller.abort()
  }, [id, validId, loadTicket, loadHistory])
```

**Line by line:**
- Line 79: the dependency array includes `id`, so this runs when the page opens **and** every time the ticket id in the URL changes. `loadTicket` and `loadHistory` never change (see `useCallback`), but React's lint rule wants every value the effect uses listed.
- Line 65-71: first point `currentId` at the new id (from now on, answers for the old id are ignored), then clear everything from the previous ticket so its data never shows on the new one.
- Line 72: an invalid id: stop here; the render shows "Ticket not found."
- Line 74-77: one controller for both loads, saved in `pageLoads` for later use. Ticket and history load in parallel.
- Line 78: cleanup: aborts both loads when the id changes again or the page closes (and once in development because of StrictMode).

**The code (part 4: `handleAction`, where every button ends up):**

```jsx
  // Every button ends up here: POST the step, and the API answers with the updated ticket.
  // Swapping it in redraws the buttons from the new allowedActions.
  async function handleAction(name, body) {
    if (running.current) return
    running.current = true
    const ticketId = id

    setBusy(true)
    setActionError(null)
    setFieldErrors({})
    try {
      const updated = await api(`/tickets/${ticketId}/${workflowActions[name].path}`, { method: 'POST', body })
      if (currentId.current !== ticketId) return
      setTicket(updated)
      loadHistory(ticketId, pageLoads.current?.signal)
    } catch (err) {
      if (currentId.current !== ticketId) return
      setFieldErrors(err.fieldErrors ?? {})
      if (err.status === 409 || err.status === 403) {
        // Someone else moved the ticket first: show the latest version instead of stale buttons
        setActionError(`${err.message} The ticket has been refreshed to its latest version.`)
        loadTicket(ticketId, pageLoads.current?.signal)
        loadHistory(ticketId, pageLoads.current?.signal)
      } else {
        setActionError(summarizeError(err, actionFormFields))
      }
    } finally {
      running.current = false
      setBusy(false)
    }
  }
```

**Line by line:**
- Line 83: `name` is the action name from `allowedActions` (for example `"LeaderApprove"`). `body` is the form data for actions with a form (route, assign, submit) and `undefined` for simple buttons, so `api()` sends no body.
- Line 84-85: double-click guard with the `running` ref.
- Line 86: remember which ticket this click was for.
- Line 88-90: disable all buttons (`busy`) and clear old errors.
- Line 92: `workflowActions[name].path` turns `"LeaderApprove"` into `"leader-approve"`, so the call is `POST /api/tickets/{id}/leader-approve`. Each workflow endpoint answers with the **updated ticket details** (`TicketDetailsDto`, including the new `allowedActions`).
- Line 93: if the user went to another ticket while waiting, drop the answer.
- Line 94: `setTicket(updated)`: React redraws the page with the new status, and `ActionsPanel` redraws the buttons from the new `allowedActions`. No second GET is needed.
- Line 95: reload the history so the new audit row appears. It uses the page's current signal so it is cancelled if the page closes.
- Line 99-103: **409 or 403 = refresh**. 409 Conflict comes from the backend when the ticket's status no longer allows this action (`TicketWorkflow.Apply`) or when someone else saved the same ticket at the same moment (the `RowVersion` check in `TicketRepository`). 403 comes when this user may no longer do it. In both cases the buttons on screen are stale. So: show the server's message plus "refreshed", and reload the ticket and history, so the user sees the real current state and the correct buttons.
- Line 104-106: any other error (for example a 400 validation error from a form): the per-field errors were stored on line 98 and show under the inputs; `summarizeError` builds the top message.
- Line 107-110: `finally` always re-enables the buttons, success or failure.

**The code (part 5: not found, loading, and the header):**

```jsx
  if (!validId || loadError) {
    return (
      <div className="card card-body text-center">
        {/* 404 also covers tickets this user isn't allowed to see: the API doesn't say which */}
        <p className="mb-2">{!validId || loadError.status === 404 ? 'Ticket not found.' : loadError.message}</p>
        <Link to="/tickets">Back to tickets</Link>
      </div>
    )
  }

  if (!ticket) return <p className="text-muted">Loading ticket…</p>

  return (
    <>
      <Link to="/tickets" className="small text-decoration-none">← Back to tickets</Link>

      <div className="d-flex flex-wrap align-items-center gap-2 mt-2 mb-3">
        <h1 className="h4 mb-0 me-2">{ticket.title}</h1>
        <span className={`badge text-bg-${statusColor(ticket.status)}`}>{splitWords(ticket.status)}</span>
        <span className={`badge text-bg-${priorityColor(ticket.priority)}`}>{ticket.priority}</span>
      </div>
```

**Line by line:**
- Line 113-121: an invalid id or a 404 shows "Ticket not found." The backend returns 404 both for a missing ticket and for one this user may not see, so nobody can find out which ticket ids exist. Other errors show their message.
- Line 123: still loading.
- Line 129-133: title and coloured badges for status and priority.

**The code (part 6: details, snapshots, responses):**

```jsx
      <div className="row g-3">
        <div className="col-lg-8 d-flex flex-column gap-3">
          <div className="card shadow-sm">
            <div className="card-body">
              <p style={{ whiteSpace: 'pre-wrap' }}>{ticket.description}</p>
              <dl className="row small mb-0">
                <dt className="col-sm-3">Repository</dt>
                <dd className="col-sm-9">
                  {isSafeLink(ticket.repositoryUrl)
                    ? <a href={ticket.repositoryUrl} target="_blank" rel="noreferrer">{ticket.repositoryUrl}</a>
                    : ticket.repositoryUrl}
                </dd>
                <dt className="col-sm-3">Client</dt>
                <dd className="col-sm-9">{ticket.client.name}</dd>
                <dt className="col-sm-3">Manager</dt>
                <dd className="col-sm-9">{ticket.manager?.name ?? <span className="text-muted">Not routed yet</span>}</dd>
                <dt className="col-sm-3">Team</dt>
                <dd className="col-sm-9">{ticket.teamName ?? <span className="text-muted">Not routed yet</span>}</dd>
                <dt className="col-sm-3">Assigned</dt>
                <dd className="col-sm-9">
                  {ticket.assignees.length > 0
                    ? ticket.assignees.map((a) => a.name).join(', ')
                    : <span className="text-muted">Nobody yet</span>}
                </dd>
                <dt className="col-sm-3">Created</dt>
                <dd className="col-sm-9">{formatDate(ticket.createdAt)}</dd>
                {ticket.closedAt && (
                  <>
                    <dt className="col-sm-3">Closed</dt>
                    <dd className="col-sm-9">{formatDate(ticket.closedAt)}</dd>
                  </>
                )}
              </dl>
            </div>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">Code snapshots</div>
            <ul className="list-group list-group-flush">
              {ticket.snapshots.map((s) => (
                <li key={s.id} className="list-group-item d-flex flex-wrap gap-2 align-items-center">
                  <span className={`badge text-bg-${s.kind === 'Before' ? 'secondary' : 'success'}`}>{s.kind}</span>
                  <span className="small">{s.branch}</span>
                  {isSafeLink(s.repositoryUrl)
                    ? (
                      <a className="font-monospace small" href={commitUrl(s.repositoryUrl, s.commitSha)}
                        target="_blank" rel="noreferrer">
                        {s.commitSha.slice(0, 7)}
                      </a>
                    )
                    : <span className="font-monospace small">{s.commitSha.slice(0, 7)}</span>}
                  <span className="text-muted small ms-auto">{formatDate(s.createdAt)}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">Responses</div>
            <div className="card-body">
              {ticket.responses.length === 0 && <p className="text-muted small mb-0">No response yet.</p>}
              {ticket.responses.map((r) => (
                <div key={r.id} className="border-start border-3 ps-3 mb-3">
                  <div className="d-flex flex-wrap gap-2 align-items-center mb-1">
                    <span className={`badge text-bg-${r.status === 'Sent' ? 'success' : 'warning'}`}>{r.status}</span>
                    <span className="small">by {r.author.name}</span>
                    <span className="text-muted small ms-auto">{formatDate(r.sentAt ?? r.createdAt)}</span>
                  </div>
                  <p className="mb-0" style={{ whiteSpace: 'pre-wrap' }}>{r.summary}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
```

**Line by line:**
- Line 139: `style={{ whiteSpace: 'pre-wrap' }}`: inline styles in JSX are a JavaScript object with camelCase names. `pre-wrap` keeps the line breaks the client typed. The text is still inserted as plain text, so it is safe.
- Line 143-145: the repository becomes a link only if it starts with `https://`. `target="_blank"` opens a new tab; `rel="noreferrer"` stops the new tab from getting a handle back to our page and from receiving our URL as the referrer.
- Line 150: `ticket.manager?.name ?? ...`: the manager is `null` until the ticket is routed. `?.` avoids a crash; `??` shows "Not routed yet".
- Line 155-157: a ternary: the assignees' names joined with commas, or "Nobody yet". `.map(...).join(', ')` is like `string.Join(", ", list.Select(a => a.Name))`.
- Line 161-166: the "Closed" row only appears when `closedAt` has a value. The fragment `<>` groups the `dt` and `dd` without an extra element.
- Line 174-188: the before/after **code snapshots**. Each one shows its kind (Before grey, After green), branch, and a link to that exact commit on GitHub with the short 7-character SHA (`slice(0, 7)`, like `Substring(0, 7)`). This is how the committee can click from "before" to "after".
- Line 195-205: the responses. A response is `Draft` (yellow) until the manager approves it, then `Sent` (green). The date shown is `sentAt` if sent, else `createdAt`. A client only ever sees `Sent` responses here, because the backend filters them (`TicketService.ToDetailsDto` keeps only `Sent` responses for the Client role). The frontend just shows whatever it gets.

**The code (part 7: actions and history column):**

```jsx
        <div className="col-lg-4 d-flex flex-column gap-3">
          <div className="card shadow-sm border-primary">
            <div className="card-header bg-white fw-medium">Your actions</div>
            <div className="card-body">
              <ActionsPanel ticket={ticket} busy={busy} error={actionError} fieldErrors={fieldErrors}
                onAction={handleAction} />
            </div>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">History</div>
            <div className="card-body py-1">
              {historyError
                ? <p className="text-danger small my-2">Couldn't load the history: {historyError}</p>
                : <HistoryList entries={history} />}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
```

**Line by line:**
- Line 214-215: `ActionsPanel` gets the ticket, the busy flag, the errors, and `handleAction` as the `onAction` prop. Passing a function as a prop is how a child tells its parent "this happened", like passing a delegate or event handler in C#. The child never calls the API itself; the page does.
- Line 222-224: the history card shows an error if the history failed, or `HistoryList` otherwise.

**Committee might ask:**
- *How does the page know which buttons to show?* From `ticket.allowedActions`, which the server computes from the same rules table that enforces the actions (`TicketWorkflow.AllowedActions`). The frontend never decides permissions.
- *What if two people act on the same ticket at the same time?* The second one gets 409 Conflict (status check or `RowVersion` check). The page shows the message and reloads the ticket, so the user sees the current state and the right buttons.
- *What if a slow answer arrives after I switched to another ticket?* `currentId` ref: every answer is checked against the ticket currently on screen and ignored if it is for another one. Also the AbortController cancels the old loads.

---

## [ActionsPanel.jsx](../../mizan-web/src/components/ticket/ActionsPanel.jsx)

**What it is:** Draws one control per action in the server's `allowedActions`: a small form for actions that need input, a button for the others.

**The code:**

```jsx
import { workflowActions } from '../../workflow/actions.js'
import AssignForm from './AssignForm.jsx'
import RouteForm from './RouteForm.jsx'
import SubmitFixForm from './SubmitFixForm.jsx'

// One control per action the SERVER says this user can take right now (allowedActions).
// If the list is empty, the user has nothing to do on this ticket at the moment.
export default function ActionsPanel({ ticket, busy, error, fieldErrors, onAction }) {
  const actions = ticket.allowedActions.filter((name) => workflowActions[name])

  // Still show the error: after a 409 the refreshed ticket may leave this user with no actions
  if (actions.length === 0) {
    return (
      <>
        {error && <div className="alert alert-warning py-2">{error}</div>}
        <p className="text-muted small mb-0">Nothing for you to do on this ticket right now.</p>
      </>
    )
  }

  const simple = actions.filter((name) => !workflowActions[name].form)
  const withForm = actions.filter((name) => workflowActions[name].form)

  function runSimple(name) {
    const action = workflowActions[name]
    if (action.confirm && !window.confirm(action.confirm)) return
    onAction(name)
  }

  return (
    <>
      {error && <div className="alert alert-danger py-2">{error}</div>}

      {withForm.map((name) => (
        <div key={name} className="mb-3">
          <div className="fw-medium mb-2">{workflowActions[name].label}</div>
          {workflowActions[name].form === 'route' && <RouteForm busy={busy} onRun={(body) => onAction(name, body)} />}
          {workflowActions[name].form === 'assign' && (
            <AssignForm ticket={ticket} busy={busy} onRun={(body) => onAction(name, body)} />
          )}
          {workflowActions[name].form === 'submit' && (
            <SubmitFixForm busy={busy} fieldErrors={fieldErrors} onRun={(body) => onAction(name, body)} />
          )}
        </div>
      ))}

      {simple.length > 0 && (
        <div className="d-flex flex-wrap gap-2">
          {simple.map((name) => (
            <button
              key={name}
              type="button"
              className={`btn btn-${workflowActions[name].variant}`}
              disabled={busy}
              onClick={() => runSimple(name)}
            >
              {workflowActions[name].label}
            </button>
          ))}
        </div>
      )}
    </>
  )
}
```

**Line by line:**
- Line 8: the props, destructured: the ticket, `busy`, the error message, the field errors, and `onAction` (the page's `handleAction`).
- Line 9: start from `ticket.allowedActions` (an array of strings like `["LeaderApprove", "LeaderSendBack"]`). `.filter` (like LINQ `Where`) keeps only names the frontend knows how to draw. If the server ever adds a new action, the UI just skips it instead of crashing.
- Line 12-19: no actions: say so. The error is still shown (as a warning), because after a 409 refresh the user may have no actions left and must still see why.
- Line 21-22: split into simple actions (one click) and actions with a form.
- Line 24-28: `runSimple`. **`window.confirm`** shows the browser's built-in OK/Cancel box. It is used only for actions that have a `confirm` text in `workflow/actions.js`: Reject, ManagerApprove (sends the response to the client) and Close. If the user clicks Cancel, nothing is sent. Then call `onAction(name)` with no body.
- Line 34-45: one block per form action. `onRun={(body) => onAction(name, body)}`: the form only collects the data and calls `onRun(body)`; this wrapper adds the action name and hands it to the page.
- Line 42: only `SubmitFixForm` gets `fieldErrors`, because only it has text inputs the server validates per field.
- Line 47-61: one button per simple action, coloured by its `variant` (for example `btn-success` for Approve), all disabled while `busy`.

**Committee might ask:**
- *Could a user add a button by editing the page?* They could send any request they like, but the server checks every action against the rules table and returns 403 or 409. The frontend's `allowedActions` is a mirror of the server rules, not the rules.

---

## [RouteForm.jsx](../../mizan-web/src/components/ticket/RouteForm.jsx)

**What it is:** The Teams Manager picks a team from a dropdown and routes the ticket.

**The code:**

```jsx
import { useEffect, useState } from 'react'
import { api, isAbort } from '../../api/client.js'

// Manager picks the team that will work on the ticket
export default function RouteForm({ busy, onRun }) {
  const [teams, setTeams] = useState(null)
  const [teamId, setTeamId] = useState('')
  const [error, setError] = useState(null)

  useEffect(() => {
    const controller = new AbortController()
    api('/teams', { signal: controller.signal })
      .then((list) => {
        setTeams(list)
        if (list.length > 0) setTeamId(list[0].id)
      })
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })
    return () => controller.abort()
  }, [])

  if (error) return <div className="text-danger small">{error}</div>
  if (!teams) return <div className="text-muted small">Loading teams…</div>
  if (teams.length === 0) return <div className="text-muted small">There are no teams to route to.</div>

  return (
    <form
      className="d-flex gap-2"
      onSubmit={(e) => {
        e.preventDefault()
        onRun({ teamId })
      }}
    >
      <select className="form-select" value={teamId} onChange={(e) => setTeamId(e.target.value)} aria-label="Team">
        {teams.map((team) => (
          <option key={team.id} value={team.id}>
            {team.name} (leader: {team.leader.name})
          </option>
        ))}
      </select>
      <button type="submit" className="btn btn-primary text-nowrap" disabled={busy}>Route</button>
    </form>
  )
}
```

**Line by line:**
- Line 10-21: load `GET /api/teams` when the form appears, with the usual AbortController pattern. Pre-select the first team, so the dropdown is never empty when submitted.
- Line 23-25: error, loading, and "no teams" states.
- Line 30-33: on submit, stop the page reload and call `onRun({ teamId })`. That body matches the backend's `RouteTicketRequest` (`TeamId`).
- Line 35: a controlled `<select>`. `aria-label` gives screen readers a name, since there is no visible label.
- Line 38: each option shows the team and its leader, so the manager knows who will receive it.

---

## [AssignForm.jsx](../../mizan-web/src/components/ticket/AssignForm.jsx)

**What it is:** The Team Leader ticks members of the ticket's team. On a reopened ticket, the leader can also continue with the members already assigned.

**The code:**

```jsx
import { useEffect, useState } from 'react'
import { api, isAbort } from '../../api/client.js'

// Leader picks members of their team. People already on the ticket stay on it
// (assignments are never removed), so they show ticked and can't be unticked.
export default function AssignForm({ ticket, busy, onRun }) {
  // undefined = still loading, null = the ticket's team isn't in the list
  const [team, setTeam] = useState(undefined)
  const [selected, setSelected] = useState([])
  const [error, setError] = useState(null)

  const assignedIds = new Set(ticket.assignees.map((a) => a.id))
  const isReopened = ticket.status === 'Reopened'

  useEffect(() => {
    const controller = new AbortController()
    // A leader only gets their own team back from this endpoint
    api('/teams', { signal: controller.signal })
      .then((list) => setTeam(list.find((t) => t.id === ticket.teamId) ?? null))
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })
    return () => controller.abort()
  }, [ticket.teamId])

  function toggle(id) {
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))
  }

  if (error) return <div className="text-danger small">{error}</div>
  if (team === undefined) return <div className="text-muted small">Loading team…</div>
  if (team === null) return <div className="text-danger small">The team for this ticket wasn't found.</div>

  // On a reopened ticket the leader may carry on with the people already assigned,
  // as long as one of them is still an active member (the same rule the server checks)
  const canContinue = isReopened && team.members.some((m) => assignedIds.has(m.id))
  const canSubmit = selected.length > 0 || canContinue

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        onRun({ memberIds: selected })
      }}
    >
      {team.members.length === 0 && <div className="text-muted small mb-2">This team has no active members.</div>}
      {team.members.map((member) => {
        const already = assignedIds.has(member.id)
        return (
          <div className="form-check" key={member.id}>
            <input
              id={`member-${member.id}`}
              type="checkbox"
              className="form-check-input"
              checked={already || selected.includes(member.id)}
              disabled={already}
              onChange={() => toggle(member.id)}
            />
            <label htmlFor={`member-${member.id}`} className="form-check-label">
              {member.name} {already && <span className="text-muted small">(already assigned)</span>}
            </label>
          </div>
        )
      })}
      <button type="submit" className="btn btn-primary mt-2" disabled={busy || !canSubmit}>
        {selected.length === 0 && isReopened ? 'Continue with current team' : 'Assign'}
      </button>
    </form>
  )
}
```

**Line by line:**
- Line 8: `team` uses `undefined` for "loading" and `null` for "not found". Two different "empty" values let the code tell the two cases apart.
- Line 9: `selected` is the list of newly ticked member ids.
- Line 12: a `Set` of already-assigned ids, like a C# `HashSet<Guid>`, for fast `has` checks.
- Line 15-24: load `GET /api/teams` and find the ticket's team. The backend's `TeamService` returns only the leader's own team for a Team Leader. The dependency array is `[ticket.teamId]`, so it reloads only if the team changes. `.find` is like LINQ `FirstOrDefault`.
- Line 26-28: `toggle` adds or removes an id. Again a **new** array each time (`filter` to remove, `[...current, id]` spread to add), never changing the old one in place.
- Line 36-37: `canContinue`: on a reopened ticket the leader may submit with nobody new ticked, if at least one already-assigned person is still an active member of the team. The comment says this mirrors the server's rule; the server checks it again. `.some` is like LINQ `Any`.
- Line 43: send `{ memberIds: selected }`, matching `AssignMembersRequest` (`MemberIds`). On "continue" this is an empty list.
- Line 47-64: one checkbox per active member. Already-assigned members are ticked and disabled, because assignments are never removed in this system.
- Line 65-67: the button is disabled while busy or when there is nothing to submit. Its text changes to "Continue with current team" in the reopened case.

Note: `AssignForm` and `RouteForm` don't receive `fieldErrors`. That's fine: `TeamId` and `MemberIds` are not in `actionFormFields` on the details page, so if the server ever sent a 400 for one of them, `summarizeError` puts its message in the alert above the actions. Errors thrown by the Application layer (`BadRequestException`, `ForbiddenException`, `ConflictException`) come back from `GlobalExceptionHandler` with only a `title` and no field errors, so their message is shown normally.

---

## [SubmitFixForm.jsx](../../mizan-web/src/components/ticket/SubmitFixForm.jsx)

**What it is:** An assigned Team Member hands in the fix: the branch, the "after" commit SHA, and a summary that becomes the response for the client.

**The code:**

```jsx
import { useState } from 'react'
import { fieldError } from '../../utils/fieldErrors.js'

// An assigned member hands in the fix: where the fixed code is, and what was changed.
// The summary becomes the response the client reads once the manager approves it.
export default function SubmitFixForm({ busy, fieldErrors, onRun }) {
  const [form, setForm] = useState({ branch: '', commitSha: '', summary: '' })

  const handleChange = (e) => setForm((current) => ({ ...current, [e.target.name]: e.target.value }))
  const invalid = (name) => (fieldError(fieldErrors, name) ? ' is-invalid' : '')
  const feedback = (name) =>
    fieldError(fieldErrors, name) && <div className="invalid-feedback">{fieldError(fieldErrors, name)}</div>

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        // Trim pasted values: a trailing space would fail the SHA check
        onRun({ branch: form.branch.trim(), commitSha: form.commitSha.trim(), summary: form.summary.trim() })
      }}
    >
      <div className="row g-2">
        <div className="col-md-5">
          <label htmlFor="fix-branch" className="form-label small mb-1">Branch with the fix</label>
          <input id="fix-branch" name="branch" className={`form-control${invalid('Branch')}`}
            value={form.branch} onChange={handleChange} required maxLength={200} />
          {feedback('Branch')}
        </div>
        <div className="col-md-7">
          <label htmlFor="fix-sha" className="form-label small mb-1">Commit SHA (the "after" code)</label>
          <input id="fix-sha" name="commitSha" className={`form-control font-monospace${invalid('CommitSha')}`}
            value={form.commitSha} onChange={handleChange} required maxLength={64} />
          {feedback('CommitSha')}
        </div>
      </div>
      <label htmlFor="fix-summary" className="form-label small mt-2 mb-1">What did you change?</label>
      <textarea id="fix-summary" name="summary" rows={3} className={`form-control${invalid('Summary')}`}
        value={form.summary} onChange={handleChange} required maxLength={4000} />
      {feedback('Summary')}
      <button type="submit" className="btn btn-primary mt-2" disabled={busy}>Submit fix</button>
    </form>
  )
}
```

**Line by line:**
- Line 7: the form's own state. It is kept here, not in the page, so typing doesn't redraw the whole details page.
- Line 9: the same "one handler for every field" pattern as `CreateTicketPage`, written as an arrow function.
- Line 10-12: the same `invalid` / `feedback` helpers, using the `fieldErrors` passed down from the page (after a 400 from `POST .../submit`).
- Line 19: trim every value before sending, then call `onRun` with a body matching `SubmitFixRequest` (`Branch`, `CommitSha`, `Summary`).
- Line 25-38: three controlled inputs with `required` and `maxLength`; the real checks (hex SHA) are on the server.
- On success, the page's `setTicket(updated)` makes the ticket `LeaderReview`, `Submit` disappears from `allowedActions`, and this form is removed from the screen.

---

## [HistoryList.jsx](../../mizan-web/src/components/ticket/HistoryList.jsx)

**What it is:** Shows the ticket's audit log entries in order.

**The code:**

```jsx
import { actionLabel, formatDate, splitWords } from '../../utils/labels.js'

// The audit log, oldest first. Clients get a shorter list: the API leaves out the
// team's internal steps (send-backs, approvals between leader and manager).
export default function HistoryList({ entries }) {
  if (!entries) return <p className="text-muted small mb-0">Loading history…</p>
  if (entries.length === 0) return <p className="text-muted small mb-0">No history yet.</p>

  return (
    <ul className="list-group list-group-flush">
      {entries.map((entry) => (
        <li key={entry.id} className="list-group-item px-0">
          <div className="d-flex justify-content-between gap-2">
            <span className="fw-medium">{actionLabel(entry.action)}</span>
            <span className="text-muted small text-nowrap">{formatDate(entry.createdAt)}</span>
          </div>
          <div className="small text-muted">
            by {entry.actorName}
            {(entry.oldValue || entry.newValue) && (
              <>
                {' · '}
                {entry.oldValue && <>{splitWords(entry.oldValue)} → </>}
                <span className="text-body">{entry.newValue && splitWords(entry.newValue)}</span>
              </>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}
```

**Line by line:**
- Line 3-4: the order and the shorter list for clients come from the API; this component just displays what it receives.
- Line 6-7: `null` = loading (the page sets history to `null` before loading), empty array = nothing yet.
- Line 11-12: one list item per entry, keyed by its id.
- Line 14: `actionLabel` turns `"LeaderApprove"` into "Leader approved".
- Line 18: who did it (`actorName`). The `AuditLog` table stores the actor's id; the API adds the name.
- Line 19-25: if there is an old or new value, show "Old → New", for example "Leader Review → Manager Review". Each part only shows if it has a value (a "Created" entry has no old value).

---

## [actions.js](../../mizan-web/src/workflow/actions.js)

**What it is:** How each workflow action **looks**: its URL path, label, colour, whether it needs a form, and whether to confirm. It does **not** decide who gets which action.

**The code:**

```js
// How each workflow action LOOKS: its URL, button text and colour.
// WHICH actions a user gets is not decided here: the server sends them in allowedActions,
// worked out from the same rules table (TicketWorkflow.cs) that enforces them.
//
// form: the action needs input first (a team, members, or the fix), so it opens a small form.
// confirm: the action can't be undone, so ask before sending it.
export const workflowActions = {
  Route:           { path: 'route',             label: 'Route to team',  variant: 'primary', form: 'route' },
  Reject:          { path: 'reject',            label: 'Reject',         variant: 'outline-danger', confirm: 'Reject this ticket? This cannot be undone.' },
  Assign:          { path: 'assign',            label: 'Assign members', variant: 'primary', form: 'assign' },
  Submit:          { path: 'submit',            label: 'Submit fix',     variant: 'primary', form: 'submit' },
  LeaderApprove:   { path: 'leader-approve',    label: 'Approve',        variant: 'success' },
  LeaderSendBack:  { path: 'leader-send-back',  label: 'Send back',      variant: 'outline-warning' },
  ManagerApprove:  { path: 'manager-approve',   label: 'Approve and send to client', variant: 'success', confirm: 'Send this response to the client?' },
  ManagerSendBack: { path: 'manager-send-back', label: 'Send back',      variant: 'outline-warning' },
  Close:           { path: 'close',             label: 'Accept and close', variant: 'success', confirm: 'Accept the fix and close this ticket?' },
  Reopen:          { path: 'reopen',            label: 'Reopen',         variant: 'outline-danger' },
}
```

**Line by line:**
- Line 7: an object used like a `Dictionary<string, ActionInfo>`. The keys are exactly the names of the backend `WorkflowAction` enum, which the API sends as strings in `allowedActions`.
- `path`: the last part of the endpoint URL, matching the `[HttpPost("...")]` routes in `TicketWorkflowController`.
- `label`: the button text. `variant`: the Bootstrap button colour (`btn-success`, `btn-outline-danger`, ...).
- `form`: which small form `ActionsPanel` shows (`route`, `assign`, `submit`). Actions without `form` are simple buttons.
- `confirm`: for Reject, ManagerApprove and Close, which can't be taken back from the UI, `ActionsPanel` asks with `window.confirm` first.

**Committee might ask:**
- *Isn't this duplicating the workflow rules?* No. It holds no rules: no statuses, no roles. The only rules table is `TicketWorkflow.cs` on the server. This file is a lookup for display, like a resource file of labels.

---

## [labels.js](../../mizan-web/src/utils/labels.js)

**What it is:** Small display helpers: turn API values into friendly text and Bootstrap colours, and format dates.

**The code:**

```js
// Display helpers: turn the API's values into friendly text and Bootstrap colours

const statusColors = {
  Submitted: 'secondary',
  Routed: 'info',
  InProgress: 'primary',
  LeaderReview: 'warning',
  ManagerReview: 'warning',
  ResponseSent: 'success',
  Closed: 'dark',
  Reopened: 'danger',
  Rejected: 'danger',
}

const priorityColors = {
  Low: 'secondary',
  Medium: 'info',
  High: 'warning',
  Critical: 'danger',
}

// "LeaderReview" -> "Leader Review"
export function splitWords(value) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export const statusColor = (status) => statusColors[status] ?? 'secondary'
export const priorityColor = (priority) => priorityColors[priority] ?? 'secondary'
export const roleLabel = (role) => splitWords(role)

// Audit log action names → readable text. Anything not listed is just split into words.
const actionLabels = {
  Created: 'Ticket created',
  Route: 'Routed',
  TeamAssigned: 'Team chosen',
  MemberAssigned: 'Member assigned',
  Assign: 'Work started',
  Submit: 'Fix submitted',
  LeaderApprove: 'Leader approved',
  LeaderSendBack: 'Leader sent back',
  ManagerApprove: 'Manager approved and sent',
  ManagerSendBack: 'Manager sent back',
  Close: 'Closed by client',
  Reopen: 'Reopened by client',
  Reject: 'Rejected',
}
export const actionLabel = (action) => actionLabels[action] ?? splitWords(action)

// The API stores times in UTC. If the value has no zone marker, treat it as UTC
// so the browser converts it to local time instead of reading it as local already.
export function formatDate(value) {
  if (!value) return ''
  const hasZone = /[zZ]|[+-]\d\d:\d\d$/.test(value)
  return new Date(hasZone ? value : `${value}Z`).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}
```

**Line by line:**
- Line 3-13 and 15-20: lookup tables from the backend's `TicketStatus` and `Priority` enum names to Bootstrap colour names. These names are not exported (no `export`), so they are private to this file, like `private static readonly` in C#.
- Line 23-25: a regular expression that puts a space between a lowercase letter and the uppercase letter after it: `"LeaderReview"` becomes `"Leader Review"`. `g` means "every match", not just the first.
- Line 27-29: tiny exported helpers. `?? 'secondary'` gives a grey fallback for any unknown value, so a new enum value never breaks the screen.
- Line 32-47: readable names for audit log actions, with a fallback to `splitWords`.
- Line 51-58: **dates as UTC**. The backend stores `DateTime` in UTC. If a value comes back without a zone marker (no `Z` and no `+hh:mm`), JavaScript's `new Date("2026-11-15T10:00:00")` would read it as *local* time, and the time shown would be off by the time zone difference. So when there is no zone, we add `Z` (UTC). Then `toLocaleString(undefined, ...)` shows it in the browser's local time and language (`undefined` = use the browser's locale), like `ToLocalTime().ToString("g")`.

Note: the regex `/[zZ]|[+-]\d\d:\d\d$/` checks for a `z`/`Z` *anywhere* in the string, not only at the end. For ISO date strings from the API that is fine, because they contain no other letters except `T`.

---

## [fieldErrors.js](../../mizan-web/src/utils/fieldErrors.js)

**What it is:** Two helpers for the API's 400 validation response, which lists messages per field: `{ "RepositoryUrl": ["..."] }`.

**The code:**

```js
// The API's 400 response lists messages per field: { "RepositoryUrl": ["..."] }.
// Returns the first message for one field, or null. The key's casing depends on the
// server's settings, so the lookup ignores case.
export function fieldError(errors, name) {
  if (!errors) return null
  const key = Object.keys(errors).find((k) => k.toLowerCase() === name.toLowerCase())
  if (!key) return null
  const value = errors[key]
  return Array.isArray(value) ? value[0] : value
}

// The one message for the alert at the top of a form.
// knownFields: the inputs this form shows. Their messages appear under the input itself;
// any other key (e.g. "$.teamId" when the JSON didn't bind) has nowhere to show, so it goes here.
export function summarizeError(err, knownFields) {
  const errors = err.fieldErrors ?? {}
  const known = knownFields.map((f) => f.toLowerCase())
  const unmatchedKeys = Object.keys(errors).filter((k) => !known.includes(k.toLowerCase()))
  const matchedCount = Object.keys(errors).length - unmatchedKeys.length

  const parts = unmatchedKeys.flatMap((k) => (Array.isArray(errors[k]) ? errors[k] : [errors[k]]))
  if (matchedCount > 0) parts.unshift('Please fix the highlighted fields.')
  return parts.length > 0 ? parts.join(' ') : err.message
}
```

**Line by line:**
- Line 4-10: `fieldError(errors, 'Title')` finds the key that matches `Title` ignoring case (the server may send `Title` or `title`), and returns the first message. ASP.NET Core sends each field's messages as an array; `Array.isArray` also handles a single string.
- Line 15-24: `summarizeError(err, knownFields)` builds the one message for the top alert:
  - Line 17-19: split the error keys into those this form shows (`known`) and the rest (`unmatchedKeys`). `matchedCount` is how many errors already appear under inputs.
  - Line 21: collect every message of the unmatched keys. `flatMap` is like LINQ `SelectMany`. Example: `"$.teamId"` (from the JSON binder when the body couldn't be read) has no input to show under, so its text goes in the alert.
  - Line 22: if some errors are under inputs, start with "Please fix the highlighted fields." (`unshift` adds to the front of the array).
  - Line 23: join the parts; if there are none (not a validation error at all, for example a 500), fall back to the error's own message (the ProblemDetails title).

**Committee might ask:**
- *Why show server errors per field instead of one message?* The user sees exactly which input is wrong, while the rules stay in one place on the server.

---

## One click, end to end

**The normal case.** The Team Leader is on `/tickets/{id}`. The ticket is in `LeaderReview`, so `GET /api/tickets/{id}` returned `allowedActions: ["LeaderApprove", "LeaderSendBack"]`.

1. **Drawing the button.** `ActionsPanel` filters `allowedActions`, finds that `LeaderApprove` has no `form`, and draws a green "Approve" button (`variant: 'success'`). It has no `confirm`, so no confirmation box.
2. **The click.** `onClick={() => runSimple('LeaderApprove')}` runs. `runSimple` skips the confirm and calls `onAction('LeaderApprove')`, which is the page's `handleAction`.
3. **`handleAction`.** It checks the `running` ref (no second click can get in), remembers `ticketId`, sets `busy` (all buttons disabled at the next redraw), clears old errors, and calls `api('/tickets/{id}/leader-approve', { method: 'POST', body: undefined })`.
4. **`api()`.** Reads the token, adds `Authorization: Bearer ...`, and calls `fetch('/api/tickets/{id}/leader-approve')`. No body, so no `Content-Type`.
5. **The Vite proxy.** The browser sends the request to `localhost:5173`. Vite sees `/api` and forwards it to `http://localhost:5199/api/tickets/{id}/leader-approve`. Same origin for the browser, so no CORS.
6. **The backend** (see [chapter 3: the workflow](03-workflow.md)). JwtBearer validates the token. `TicketWorkflowController.LeaderApprove` calls `TicketWorkflowService.LeaderApproveAsync`, which loads the user and ticket, calls `TicketWorkflow.Apply` (is this the leader of this ticket's team? is the status `LeaderReview`?), changes the status to `ManagerReview`, writes an `AuditLog` row, and saves everything in one transaction. It answers `200 OK` with the updated `TicketDetailsDto`. For the leader, the new `allowedActions` is now empty, because only the routing manager can act in `ManagerReview`.
7. **Back in `api()`.** `response.ok` is true; the body is parsed and returned.
8. **Back in `handleAction`.** `currentId.current === ticketId` (still the same ticket), so `setTicket(updated)`.
9. **Redraw.** React calls `TicketDetailsPage` again with the new ticket. The status badge shows "Manager Review". `ActionsPanel` gets the new `allowedActions` (empty) and shows "Nothing for you to do on this ticket right now." No code in the frontend decided that; the server's list did.
10. **History.** `loadHistory(ticketId, pageLoads.current?.signal)` fetches `/api/tickets/{id}/history`, and `HistoryList` shows the new entry "Leader approved, Leader Review → Manager Review".
11. **`finally`.** `running` is reset and `busy` becomes `false`.

**The same click when someone else changed the ticket first.** Say the leader had the page open in two tabs, and approved in the other tab a minute ago. This tab still shows `LeaderReview` and the Approve button.

1. Steps 1 to 5 are the same.
2. **The backend.** `TicketWorkflow.Apply` sees the leader is allowed (still the leader of the team), but the status is now `ManagerReview`, not `LeaderReview`. It throws `ConflictException("Can't LeaderApprove a ticket that is ManagerReview.")`. (If both saves had happened at the exact same moment, the `RowVersion` check in `TicketRepository` would throw a `ConflictException` instead.) `GlobalExceptionHandler` turns it into `409 Conflict` with a ProblemDetails body whose `title` is that message.
3. **`api()`.** `response.ok` is false. Not a 401, so no logout. There is a JSON body, so it throws `ApiError(409, "Can't LeaderApprove a ticket that is ManagerReview.", {})`.
4. **`handleAction` catch.** Still the same ticket. `fieldErrors` is `{}`. `err.status === 409`, so it sets the action error to the server's message plus " The ticket has been refreshed to its latest version.", and calls `loadTicket` and `loadHistory`.
5. **Refresh.** The fresh `GET /api/tickets/{id}` returns status `ManagerReview` and an empty `allowedActions` for the leader. `setTicket` redraws the page. `ActionsPanel` has no actions, so it shows the message in a yellow warning box above "Nothing for you to do on this ticket right now." The history now also shows the approval made in the other tab.
6. **`finally`.** Buttons are re-enabled (there are none left to click).

The point to make to the committee: the frontend never trusted its own copy of the ticket. The server rejected the stale click with 409, and the page answered by showing the real current state instead of leaving stale buttons on screen.
