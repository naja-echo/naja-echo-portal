import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { LandingPage } from '@/features/auth/pages/LandingPage'
import { AuthCallbackPage } from '@/features/auth/pages/AuthCallbackPage'
import { AuthErrorPage } from '@/features/auth/pages/AuthErrorPage'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { ProfilePage } from '@/features/dashboard/pages/ProfilePage'
import { SettingsPage } from '@/features/dashboard/pages/SettingsPage'
import { DashboardLayout } from '@/features/dashboard/components/DashboardLayout'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'
import { AdminRoute } from '@/features/auth/AdminRoute'
import { DataImportPage } from '@/features/admin/pages/DataImportPage'
import { AdminUsersPage } from '@/features/admin/pages/AdminUsersPage'
import { MyHangarView } from '@/features/hangar/pages/MyHangarView'
import { OrgHangarView } from '@/features/hangar/pages/OrgHangarView'
import { WarehouseItemsView } from '@/features/warehouse/pages/WarehouseItemsView'
import { ShipComponentsView } from '@/features/warehouse/pages/ShipComponentsView'
import { MaterialsView } from '@/features/warehouse/pages/MaterialsView'
import { MyLootPage } from '@/features/crew-resources/pages/MyLootPage'
import { LootDistributionPage } from '@/features/crew-resources/pages/LootDistributionPage'

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/auth/callback" element={<AuthCallbackPage />} />
        <Route path="/auth/error" element={<AuthErrorPage />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<DashboardLayout />}>
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/hangar" element={<Navigate to="/hangar/mine" replace />} />
            <Route path="/hangar/mine" element={<MyHangarView />} />
            <Route path="/hangar/org" element={<OrgHangarView />} />
            <Route path="/warehouse" element={<Navigate to="/warehouse/items" replace />} />
            <Route path="/warehouse/items" element={<WarehouseItemsView />} />
            <Route path="/warehouse/ship-components" element={<ShipComponentsView />} />
            <Route path="/warehouse/materials" element={<MaterialsView />} />
            <Route path="/crew-resources/my-loot" element={<MyLootPage />} />
            <Route path="/crew-resources/loot-distribution" element={<LootDistributionPage />} />
            <Route path="/dashboard/profile" element={<ProfilePage />} />
            <Route path="/dashboard/settings" element={<SettingsPage />} />
            <Route element={<AdminRoute />}>
              <Route path="/dashboard/admin/users" element={<AdminUsersPage />} />
              <Route path="/dashboard/admin/data-import" element={<DataImportPage />} />
            </Route>
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
