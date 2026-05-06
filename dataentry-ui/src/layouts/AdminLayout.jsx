import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  LayoutDashboard, BookOpen, Users, Wrench, BarChart3,
  UserCog, LogOut, Menu, X, Banknote, ChevronRight
} from 'lucide-react';
import { useState, useEffect } from 'react';
import logo from '../assets/logo.jpg';

const navItems = [
  { to: '/admin/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/admin/daybook', icon: BookOpen, label: 'Daybook' },
  { to: '/admin/customers', icon: Users, label: 'Customers' },
  { to: '/admin/services', icon: Wrench, label: 'Services', adminOnly: true },
  { to: '/admin/reports', icon: BarChart3, label: 'Reports' },
  { to: '/admin/employees', icon: UserCog, label: 'Employees', adminOnly: true },
  { to: '/admin/salary', icon: Banknote, label: 'Salary', adminOnly: true },
];

export default function AdminLayout() {
  const { user, logout, isAdmin } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Close sidebar on route change (mobile)
  useEffect(() => {
    setSidebarOpen(false);
  }, [location.pathname]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const filteredNav = navItems.filter(item => !item.adminOnly || isAdmin);

  // Get current page title for breadcrumb
  const currentPage = filteredNav.find(item => location.pathname.startsWith(item.to));

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 backdrop-blur-sm z-40 lg:hidden animate-fade-in"
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
          <img src={logo} alt="Pitstop logo" className="w-10 h-10 rounded-lg object-cover ring-2 ring-white/20" />
          <div>
            <h1 className="text-lg font-bold text-white tracking-tight">Pitstop</h1>
            <p className="text-xs text-sidebar-text/70">Detailing Studio</p>
          </div>
        </div>

        <nav className="mt-6 px-3 space-y-1">
          {filteredNav.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm font-medium transition-all duration-150
                ${isActive
                  ? 'bg-sidebar-active text-white shadow-lg shadow-primary/20'
                  : 'text-sidebar-text hover:bg-white/8 hover:text-white hover:translate-x-0.5'}`
              }
            >
              <Icon className="w-5 h-5 flex-shrink-0" />
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-white/10">
          <div className="flex items-center gap-3 px-2 mb-3">
            <div className="w-9 h-9 rounded-full bg-gradient-to-br from-primary-light to-primary flex items-center justify-center text-white text-sm font-bold shadow-md">
              {user?.name?.charAt(0)?.toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-white truncate">{user?.name}</p>
              <p className="text-xs text-sidebar-text/70 capitalize">{user?.role}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 w-full px-4 py-2.5 text-sm text-sidebar-text hover:text-white hover:bg-white/10 rounded-lg transition-colors cursor-pointer group"
          >
            <LogOut className="w-4 h-4 group-hover:translate-x-[-2px] transition-transform" />
            Sign Out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar */}
        <header className="h-16 bg-white border-b border-gray-200/80 flex items-center justify-between px-4 lg:px-8 shrink-0 shadow-sm">
          <div className="flex items-center gap-3">
            <button
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden p-2 rounded-lg hover:bg-gray-100 cursor-pointer"
            >
              <Menu className="w-5 h-5" />
            </button>
            {/* Breadcrumb */}
            <div className="hidden sm:flex items-center gap-1.5 text-sm text-gray-400">
              <span>Pitstop</span>
              <ChevronRight className="w-3.5 h-3.5" />
              <span className="text-gray-700 font-medium">{currentPage?.label || 'Dashboard'}</span>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <div className="text-sm text-gray-500 hidden md:block">
              {new Date().toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'short', year: 'numeric' })}
            </div>
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary-light to-primary flex items-center justify-center text-white text-xs font-bold">
                {user?.name?.charAt(0)?.toUpperCase()}
              </div>
              <div className="hidden sm:block">
                <p className="text-sm font-medium text-gray-700 leading-tight">{user?.name}</p>
                <p className="text-xs text-gray-400 capitalize">{user?.role}</p>
              </div>
            </div>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-auto p-4 lg:p-8">
          <div className="animate-fade-in">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
