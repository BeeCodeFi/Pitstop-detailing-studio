import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  LayoutDashboard, BookOpen, Users, Wrench, BarChart3,
  UserCog, LogOut, Menu, X
} from 'lucide-react';
import { useState } from 'react';
import logo from '../assets/logo.jpg';

const navItems = [
  { to: '/admin/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/admin/daybook', icon: BookOpen, label: 'Daybook' },
  { to: '/admin/customers', icon: Users, label: 'Customers' },
  { to: '/admin/services', icon: Wrench, label: 'Services', adminOnly: true },
  { to: '/admin/reports', icon: BarChart3, label: 'Reports' },
  { to: '/admin/employees', icon: UserCog, label: 'Employees', adminOnly: true },
];

export default function AdminLayout() {
  const { user, logout, isAdmin } = useAuth();
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const filteredNav = navItems.filter(item => !item.adminOnly || isAdmin);

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside className={`
        fixed inset-y-0 left-0 z-50 w-64 bg-sidebar text-sidebar-text
        transform transition-transform duration-200 ease-in-out
        lg:relative lg:translate-x-0
        ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}
      `}>
        <div className="flex items-center gap-3 px-6 py-5 border-b border-white/10">
          <img src={logo} alt="Pitstop logo" className="w-10 h-10 rounded-lg object-cover" />
          <div>
            <h1 className="text-lg font-bold text-white">Pitstop</h1>
            <p className="text-xs text-sidebar-text">Detailing Studio</p>
          </div>
        </div>

        <nav className="mt-4 px-3 space-y-1">
          {filteredNav.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              onClick={() => setSidebarOpen(false)}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm font-medium transition-colors
                ${isActive
                  ? 'bg-sidebar-active text-white'
                  : 'text-sidebar-text hover:bg-white/10 hover:text-white'}`
              }
            >
              <Icon className="w-5 h-5" />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-white/10">
          <div className="flex items-center gap-3 px-2 mb-3">
            <div className="w-8 h-8 rounded-full bg-primary-light flex items-center justify-center text-white text-sm font-bold">
              {user?.name?.charAt(0)?.toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-white truncate">{user?.name}</p>
              <p className="text-xs text-sidebar-text">{user?.role}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 w-full px-4 py-2 text-sm text-sidebar-text hover:text-white hover:bg-white/10 rounded-lg transition-colors cursor-pointer"
          >
            <LogOut className="w-4 h-4" />
            Sign Out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar */}
        <header className="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-4 lg:px-8 shrink-0">
          <button
            onClick={() => setSidebarOpen(true)}
            className="lg:hidden p-2 rounded-lg hover:bg-gray-100 cursor-pointer"
          >
            <Menu className="w-5 h-5" />
          </button>
          <div className="text-sm text-gray-500">
            {new Date().toLocaleDateString('en-IN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
          </div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium hidden sm:block">{user?.name}</span>
            <span className="text-xs bg-primary/10 text-primary px-2 py-1 rounded-full">{user?.role}</span>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-auto p-4 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
