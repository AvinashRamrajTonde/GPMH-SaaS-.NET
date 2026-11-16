import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { lazy, Suspense } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { PlannedModulePage } from './pages/PlannedModulePage';

const OnboardingLgdPage = lazy(() =>
  import('./pages/OnboardingLgdPage').then((m) => ({ default: m.OnboardingLgdPage })),
);
const PropertiesPage = lazy(() =>
  import('./features/properties/PropertiesPage').then((m) => ({ default: m.PropertiesPage })),
);
const PropertyFormPage = lazy(() =>
  import('./features/properties/PropertyFormPage').then((m) => ({ default: m.PropertyFormPage })),
);
const PropertyDetailPage = lazy(() =>
  import('./features/properties/PropertyDetailPage').then((m) => ({
    default: m.PropertyDetailPage,
  })),
);
const CitizensPage = lazy(() =>
  import('./features/citizens/CitizensPage').then((m) => ({ default: m.CitizensPage })),
);
const FormulaMastersPage = lazy(() =>
  import('./features/formula/FormulaMastersPage').then((m) => ({ default: m.FormulaMastersPage })),
);
const AuditLogPage = lazy(() =>
  import('./features/audit/AuditLogPage').then((m) => ({ default: m.AuditLogPage })),
);
const SuperAdminProvisionPage = lazy(() =>
  import('./features/superadmin/SuperAdminProvisionPage').then((m) => ({
    default: m.SuperAdminProvisionPage,
  })),
);
const AgentLoginPage = lazy(() =>
  import('./features/agents/AgentLoginPage').then((m) => ({ default: m.AgentLoginPage })),
);
const AgentRegisterPage = lazy(() =>
  import('./features/agents/AgentRegisterPage').then((m) => ({ default: m.AgentRegisterPage })),
);
const AgentWorkspacePage = lazy(() =>
  import('./features/agents/AgentWorkspacePage').then((m) => ({ default: m.AgentWorkspacePage })),
);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function RouteLoadingFallback() {
  return (
    <div className="space-y-4">
      <div className="h-24 animate-pulse rounded-2xl bg-slate-200/70" />
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="h-28 animate-pulse rounded-2xl bg-slate-200/70" />
        <div className="h-28 animate-pulse rounded-2xl bg-slate-200/70" />
        <div className="h-28 animate-pulse rounded-2xl bg-slate-200/70" />
      </div>
    </div>
  );
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <Layout>
            <Suspense fallback={<RouteLoadingFallback />}>
              <Routes>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/dashboard" element={<DashboardPage />} />
                <Route path="/onboarding" element={<OnboardingLgdPage />} />
                <Route path="/properties" element={<PropertiesPage />} />
                <Route path="/namuna-8" element={<PropertiesPage />} />
                <Route path="/namuna-8/add" element={<PropertyFormPage />} />
                <Route path="/namuna-8/edit/:id" element={<PropertyFormPage />} />
                <Route path="/namuna-8/view/:id" element={<PropertyDetailPage />} />
                <Route
                  path="/namuna-8/print"
                  element={<PlannedModulePage titleKey="properties.printExtractBtn" sprintNumber={3} />}
                />
                <Route
                  path="/namuna-8/partition/:id"
                  element={<PlannedModulePage titleKey="properties.partitionBtn" sprintNumber={3} />}
                />
                <Route path="/citizens" element={<CitizensPage />} />
                <Route path="/formula" element={<FormulaMastersPage />} />
                <Route path="/namuna-8/formula" element={<FormulaMastersPage />} />
                <Route path="/audit" element={<AuditLogPage />} />
                <Route path="/audit/:entityName/:entityId" element={<AuditLogPage />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/superadmin" element={<SuperAdminProvisionPage />} />
                <Route path="/superadmin/provision-gp" element={<SuperAdminProvisionPage />} />
                <Route path="/agent/register" element={<AgentRegisterPage />} />
                <Route path="/agent/login" element={<AgentLoginPage />} />
                <Route path="/agent/dashboard" element={<AgentWorkspacePage />} />
                <Route
                  path="/namuna-10/collect"
                  element={<PlannedModulePage titleKey="dashboard.actionCollect" sprintNumber={5} />}
                />
                <Route
                  path="/namuna-9ka/generate"
                  element={<PlannedModulePage titleKey="dashboard.actionBills" sprintNumber={4} />}
                />
                <Route
                  path="/namuna-8/mutation/new"
                  element={<PlannedModulePage titleKey="dashboard.actionMutation" sprintNumber={3} />}
                />
                <Route path="*" element={<NotFoundPage />} />
              </Routes>
            </Suspense>
          </Layout>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}

export default App;
