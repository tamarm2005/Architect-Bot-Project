# Copilot Instructions for myprojectlibrary

## Project Overview
A React 19 application with RTL (right-to-left) Hebrew UI support. Uses React Router v7 for client-side routing with a navigation bar and chat-based UI components. The app is a Create React App bootstrapped project with no backend integration yet.

**Key Stack:** React 19, React Router 7, React Scripts 5, Testing Library

## Architecture & Data Flow

### Route Structure
The app implements a sidebar navigation pattern with routes in [src/App.js](src/App.js):
- `/` → `LoginScreen` (home/landing)
- `/RegistrationSite` → User registration form
- `/LoginSite` → User login form  
- `/StartingConversation` → Chat interface with sidebar
- `/d`, `/e`, `/f` → Placeholder components for future features

Routes are wrapped with `BrowserRouter` in [src/index.js](src/index.js).

### State Management Pattern
⚠️ **Critical:** Components use local variables with `onChange` handlers, NOT React state (e.g., `LoginSite.js` line 5-6). This is a bug pattern—email/password aren't maintained across renders. When adding state management:
- Replace direct variable assignment with `useState`
- Example fix: `const [email, setEmail] = useState("")` instead of `let email = ""`

### UI Conventions
- **RTL Direction:** CSS sets `direction: rtl` globally ([Style.css](src/components/Style.css) line 6)
- **Hebrew Content:** All UI text is Hebrew except placeholder "link to d/e/f"
- **Component Styling:** Centralized in `src/components/Style.css` (173 lines)
- **Layout Classes:** `.container` (login/registration), `.chat-container` (conversation view)

## Key Components

| Component | Purpose | Notes |
|-----------|---------|-------|
| [LoginScreen.js](src/components/LoginScreen.js) | Landing page | Navigation buttons to registration/login |
| [LoginSite.js](src/components/LoginSite.js) | Login form | Accepts `userList` prop, has state bug |
| [RegistrationSite.js](src/components/RegistrationSite.js) | Signup form | Similar state bug as LoginSite |
| [StartingConversation.js](src/components/StartingConversation.js) | Chat interface | Sidebar + main chat area, uses flex layout |
| D.js, E.js, F.js | Stub components | Minimal implementations, ready for expansion |

## Developer Workflows

### Build & Run
```bash
npm start        # Dev server on http://localhost:3000 (auto-reload)
npm test         # Jest in watch mode
npm run build    # Production bundle to /build folder
```

### Testing Configuration
- Uses Jest + React Testing Library (v16, v10, v6)
- Config extends `react-app` in [package.json](package.json) line 32-35
- Test setup in [src/setupTests.js](src/setupTests.js)

### Browser Support
Production targets: >0.2% market share, excludes old Opera mini  
Development: Latest 1 version of Chrome, Firefox, Safari

## Code Patterns & Conventions

### Form Handling Anti-Pattern (Current Implementation)
❌ Do NOT replicate this pattern:
```javascript
// LoginSite.js - Problematic code
let email = ""; // Lost on re-render
<input onChange={(e) => { email = e.target.value; }} />
```

✅ Use React state instead:
```javascript
const [email, setEmail] = useState("");
<input onChange={(e) => setEmail(e.target.value)} />
```

### Navigation
Use `useNavigate` hook from react-router-dom:
```javascript
const navigate = useNavigate();
navigate("/RegistrationSite");  // Push route
navigate(-1);                   // Back button (used in App.js)
```

### Styling
- Global styles in `src/index.css` and `src/components/Style.css`
- No CSS modules or styled-components in use
- Flexbox layout system (see `.chat-container` for reference)
- RTL handled via CSS `direction: rtl`

## File Organization
```
src/
├── App.js              # Main router & layout
├── index.js            # BrowserRouter wrapper
├── App.css, index.css  # Global styles (mostly unused)
└── components/
    ├── *.js            # Page components
    └── Style.css       # Centralized component styles
```

## Critical Issues to Address
1. **Form State Bug:** Replace `let` with `useState` in LoginSite, RegistrationSite, RegistrationSite
2. **Event Handler:** LoginSite/RegistrationSite use `onChange` on buttons instead of `onClick`
3. **User Persistence:** No state management (Redux/Context/Zustand) for logged-in user
4. **No Backend:** API calls not implemented; `userList` prop passed but source unknown

## Integration Points
- **React Router v7:** Route definitions and navigation
- **No external APIs:** Chat interface is UI-only skeleton
- **No state library:** Use React Context or `useState` for shared state
- **No authentication:** Login validation is mock/client-side only

## When Adding Features
- **New routes?** Add to [App.js Routes](src/App.js#L30-L37) and navbar link
- **New page?** Create component in `src/components/`, import in App.js
- **Styling?** Add to `src/components/Style.css` (keep classes consistent with existing naming)
- **User data?** Lift state to App.js level and pass as props, or implement Context
