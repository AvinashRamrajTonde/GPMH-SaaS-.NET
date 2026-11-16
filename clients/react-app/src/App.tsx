import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { AgentLoginPage } from './features/agents/AgentLoginPage';
import { AgentRegisterPage } from './features/agents/AgentRegisterPage';
import { AgentWorkspacePage } from './features/agents/AgentWorkspacePage';
import { SuperAdminProvisionPage } from './features/superadmin/SuperAdminProvisionPage';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { OnboardingLgdPage } from './pages/OnboardingLgdPage';
import { PlannedModulePage } from './pages/PlannedModulePage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <Layout>
            <Routes>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/onboarding" element={<OnboardingLgdPage />} />
              <Route path="/login" element={<LoginPage />} />
              <Route path="/superadmin" element={<SuperAdminProvisionPage />} />
              <Route path="/superadmin/provision-gp" element={<SuperAdminProvisionPage />} />
              <Route path="/agent/register" element={<AgentRegisterPage />} />
              <Route path="/agent/login" element={<AgentLoginPage />} />
              <Route path="/agent/dashboard" element={<AgentWorkspacePage />} />
              <Route
                path="/namuna-8/add"
                element={<PlannedModulePage titleKey="dashboard.actionNamuna8" targetSprint="Sprint 3" />}
              />
              <Route
                path="/namuna-10/collect"
                element={<PlannedModulePage titleKey="dashboard.actionCollect" targetSprint="Sprint 5" />}
              />
              <Route
                path="/namuna-9ka/generate"
                element={<PlannedModulePage titleKey="dashboard.actionBills" targetSprint="Sprint 4" />}
              />
              <Route
                path="/namuna-8/mutation/new"
                element={<PlannedModulePage titleKey="dashboard.actionMutation" targetSprint="Sprint 3" />}
              />
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </Layout>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}

export default App;
