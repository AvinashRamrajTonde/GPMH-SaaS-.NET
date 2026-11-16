import { createContext, useContext } from 'react';
import type { AuthenticatedUser, AuthResponse } from '../services/sprint2Api';

export interface AuthContextValue {
  user: AuthenticatedUser | null;
  accessToken: string | null;
  isBootstrapping: boolean;
  login: (username: string, password: string, tenantId?: string | null) => Promise<AuthResponse>;
  loginAgent: (identifier: string, password: string) => Promise<AuthResponse>;
  switchTenant: (tenantId: string) => Promise<void>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used inside AuthProvider');
  }
  return ctx;
}
