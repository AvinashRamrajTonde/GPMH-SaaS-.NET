import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { lazy, Suspense } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { AuthProvider } from './context/AuthContext';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';

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
const Namuna8PrintPage = lazy(() =>
  import('./features/print/Namuna8PrintPage').then((m) => ({ default: m.Namuna8PrintPage })),
);
const ExtractVerifyPage = lazy(() =>
  import('./features/print/ExtractVerifyPage').then((m) => ({ default: m.ExtractVerifyPage })),
);
const ReceiptVerifyPage = lazy(() =>
  import('./features/namuna10/ReceiptVerifyPage').then((m) => ({ default: m.ReceiptVerifyPage })),
);
const MutationRegisterPage = lazy(() =>
  import('./features/mutations/MutationRegisterPage').then((m) => ({
    default: m.MutationRegisterPage,
  })),
);
const PropertyPartitionWizardPage = lazy(() =>
  import('./features/partitions/PropertyPartitionWizardPage').then((m) => ({
    default: m.PropertyPartitionWizardPage,
  })),
);
const MutationFormPage = lazy(() =>
  import('./features/mutations/MutationFormPage').then((m) => ({
    default: m.MutationFormPage,
  })),
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
const Namuna9DemandRegisterPage = lazy(() =>
  import('./features/namuna9/Namuna9DemandRegisterPage').then((m) => ({
    default: m.Namuna9DemandRegisterPage,
  })),
);
const Namuna9KaBillsPage = lazy(() =>
  import('./features/namuna9/Namuna9KaBillsPage').then((m) => ({
    default: m.Namuna9KaBillsPage,
  })),
);
const Namuna10RegisterPage = lazy(() =>
  import('./features/namuna10/Namuna10RegisterPage').then((m) => ({
    default: m.Namuna10RegisterPage,
  })),
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
                <Route path="/namuna-8/print" element={<Namuna8PrintPage />} />
                <Route path="/namuna-8/print/:id" element={<Namuna8PrintPage />} />
                <Route path="/verify/extract/:token" element={<ExtractVerifyPage />} />
                <Route path="/verify/receipt/:token" element={<ReceiptVerifyPage />} />
                <Route path="/namuna-8/partition" element={<PropertyPartitionWizardPage />} />
                <Route path="/namuna-8/partition/:id" element={<PropertyPartitionWizardPage />} />
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
                <Route path="/namuna-9" element={<Namuna9DemandRegisterPage />} />
                <Route path="/namuna-9/register" element={<Namuna9DemandRegisterPage />} />
                <Route path="/namuna-10" element={<Namuna10RegisterPage />} />
                <Route path="/namuna-10/collect" element={<Namuna10RegisterPage />} />
                <Route path="/namuna-9ka/generate" element={<Namuna9KaBillsPage />} />
                <Route path="/namuna-9ka/bills" element={<Namuna9KaBillsPage />} />
                <Route path="/namuna-8/mutation" element={<MutationRegisterPage />} />
                <Route path="/namuna-8/mutation/new" element={<MutationFormPage />} />
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
