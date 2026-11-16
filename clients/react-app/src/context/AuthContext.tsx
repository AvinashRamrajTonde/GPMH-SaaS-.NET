import { useCallback, useState, type PropsWithChildren } from 'react';
import { setClientSessionContext } from '../services/apiClient';
import { sprint2Api, type AuthenticatedUser, type AuthResponse } from '../services/sprint2Api';
import { AuthContext } from './useAuth';

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);

  const applySession = useCallback((auth: AuthResponse) => {
    setAccessToken(auth.accessToken);
    setUser(auth.user);
    setClientSessionContext(auth.user.activeTenantId, auth.accessToken);
  }, []);

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
    } finally {
      setAccessToken(null);
      setUser(null);
      setClientSessionContext(null, null);
    }
  }, []);

  return (
    <AuthContext.Provider value={{ user, accessToken, login, loginAgent, switchTenant, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
