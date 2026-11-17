import { useCallback, useEffect, useState, type PropsWithChildren } from 'react';
import { setClientSessionContext } from '../services/apiClient';
import { sprint2Api, type AuthenticatedUser, type AuthResponse } from '../services/sprint2Api';
import { superAdminApi, type SupportImpersonationResult } from '../services/superAdminApi';
import { AuthContext, type SupportImpersonationSession } from './useAuth';

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
  const [impersonationSession, setImpersonationSession] =
    useState<SupportImpersonationSession | null>(null);

  const applySession = useCallback((auth: AuthResponse) => {
    setImpersonationSession(null);
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
      if (impersonationSession) return;
      const auth = await sprint2Api.refresh(tenantId);
      applySession(auth);
    },
    [applySession, impersonationSession],
  );

  const startSupportImpersonation = useCallback(
    (result: SupportImpersonationResult) => {
      if (!accessToken || !user) return;
      const originalToken = impersonationSession?.originalToken ?? accessToken;
      const originalUser = impersonationSession?.originalUser ?? user;
      setImpersonationSession({
        originalToken,
        originalUser,
        targetTenantId: result.targetTenantId,
        targetTenantNameEn: result.targetTenantNameEn,
        targetTenantNameMr: result.targetTenantNameMr,
        targetLgdCode: result.targetLgdCode,
        reason: result.reason,
        expiresAtUtc: result.expiresAtUtc,
      });
      setAccessToken(result.accessToken);
      setUser({
        userId: originalUser.userId,
        username: originalUser.username,
        fullNameEn: originalUser.fullNameEn,
        fullNameMr: originalUser.fullNameMr,
        role: 'Auditor',
        activeTenantId: result.targetTenantId,
        memberships: [{ tenantId: result.targetTenantId, role: 'Auditor', isDefault: true }],
      });
      setClientSessionContext(result.targetTenantId, result.accessToken);
    },
    [accessToken, user, impersonationSession],
  );

  const exitSupportImpersonation = useCallback(async () => {
    const current = impersonationSession;
    if (!current) return;
    try {
      await superAdminApi.exitImpersonation(current.targetTenantId);
    } catch {
      // Token may have expired on server; still restore the SuperAdmin / Support_Admin session.
    } finally {
      setImpersonationSession(null);
      setAccessToken(current.originalToken);
      setUser(current.originalUser);
      setClientSessionContext(current.originalUser.activeTenantId, current.originalToken);
    }
  }, [impersonationSession]);

  const logout = useCallback(async () => {
    if (impersonationSession) {
      try {
        await superAdminApi.exitImpersonation(impersonationSession.targetTenantId);
      } catch {
        // Best-effort exit log
      }
    }
    try {
      await sprint2Api.logout();
    } catch {
      // Clear local session state even if network logout fails
    } finally {
      setImpersonationSession(null);
      setSessionHint(false);
      setAccessToken(null);
      setUser(null);
      setClientSessionContext(null, null);
    }
  }, [impersonationSession]);

  return (
    <AuthContext.Provider
      value={{
        user,
        accessToken,
        isBootstrapping,
        impersonationSession,
        isImpersonating: impersonationSession !== null,
        login,
        loginAgent,
        switchTenant,
        logout,
        startSupportImpersonation,
        exitSupportImpersonation,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
