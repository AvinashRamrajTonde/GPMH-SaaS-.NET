import { useCallback, useEffect, useState, type PropsWithChildren } from 'react';
import { setClientSessionContext } from '../services/apiClient';
import { sprint2Api, type AuthenticatedUser, type AuthResponse } from '../services/sprint2Api';
import { AuthContext } from './useAuth';

const SESSION_HINT_KEY = 'gpmh_has_session';

function hasSessionHint(): boolean {
  try {
    return typeof window !== 'undefined' && window.localStorage.getItem(SESSION_HINT_KEY) === '1';
  } catch {
    return false;
  }
}

function setSessionHint(active: boolean): void {
  try {
    if (typeof window === 'undefined') return;
    if (active) {
      window.localStorage.setItem(SESSION_HINT_KEY, '1');
    } else {
      window.localStorage.removeItem(SESSION_HINT_KEY);
    }
  } catch {
    // Ignore storage restriction
  }
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(() => hasSessionHint());

  const applySession = useCallback((auth: AuthResponse) => {
    setSessionHint(true);
    setAccessToken(auth.accessToken);
    setUser(auth.user);
    setClientSessionContext(auth.user.activeTenantId, auth.accessToken);
  }, []);

  useEffect(() => {
    if (!hasSessionHint()) {
      return;
    }

    let active = true;
    async function restoreSession() {
      try {
        const refreshed = await sprint2Api.refresh(null);
        if (active) {
          applySession(refreshed);
        }
      } catch {
        if (active) {
          setSessionHint(false);
          setAccessToken(null);
          setUser(null);
          setClientSessionContext(null, null);
        }
      } finally {
        if (active) {
          setIsBootstrapping(false);
        }
      }
    }
    void restoreSession();
    return () => {
      active = false;
    };
  }, [applySession]);

  const login = useCallback(
    async (username: string, password: string, tenantId?: string | null) => {
      const auth = await sprint2Api.login(username, password, tenantId);
      applySession(auth);
      return auth;
    },
    [applySession],
  );

  const loginAgent = useCallback(
    async (identifier: string, password: string) => {
      const auth = await sprint2Api.loginAgent(identifier, password);
      applySession(auth);
      return auth;
    },
    [applySession],
  );

  const switchTenant = useCallback(
    async (tenantId: string) => {
      const auth = await sprint2Api.refresh(tenantId);
      applySession(auth);
    },
    [applySession],
  );

  const logout = useCallback(async () => {
    try {
      await sprint2Api.logout();
    } catch {
      // Clear local session state even if network logout fails
    } finally {
      setSessionHint(false);
      setAccessToken(null);
      setUser(null);
      setClientSessionContext(null, null);
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{ user, accessToken, isBootstrapping, login, loginAgent, switchTenant, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}
