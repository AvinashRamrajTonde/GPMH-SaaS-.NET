import { createContext, useContext } from 'react';
import type { AuthenticatedUser, AuthResponse } from '../services/sprint2Api';
import type { SupportImpersonationResult } from '../services/superAdminApi';

export interface SupportImpersonationSession {
  originalToken: string;
  originalUser: AuthenticatedUser;
  targetTenantId: string;
  targetTenantNameEn: string;
  targetTenantNameMr: string;
  targetLgdCode: string;
  reason: string;
  expiresAtUtc: string;
}

export interface AuthContextValue {
  user: AuthenticatedUser | null;
  accessToken: string | null;
  isBootstrapping: boolean;
  impersonationSession: SupportImpersonationSession | null;
  isImpersonating: boolean;
  login: (username: string, password: string, tenantId?: string | null) => Promise<AuthResponse>;
  loginAgent: (identifier: string, password: string) => Promise<AuthResponse>;
  switchTenant: (tenantId: string) => Promise<void>;
  logout: () => Promise<void>;
  startSupportImpersonation: (result: SupportImpersonationResult) => void;
  exitSupportImpersonation: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used inside AuthProvider');
  }
  return ctx;
}
