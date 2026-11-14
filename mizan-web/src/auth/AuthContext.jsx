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
