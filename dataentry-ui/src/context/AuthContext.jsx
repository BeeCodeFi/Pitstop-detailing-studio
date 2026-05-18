import { createContext, useContext, useState, useEffect } from 'react';
import { authService } from '../services/dataService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem('user');
    return saved ? JSON.parse(saved) : null;
  });
  const [loading, setLoading] = useState(false);
  const [activeDaybookDate, setActiveDaybookDate] = useState(
    new Date().toISOString().split('T')[0]
  );

  const login = async (username, password) => {
    setLoading(true);
    try {
      const { data } = await authService.login(username, password);
      localStorage.setItem('token', data.token);
      localStorage.setItem('user', JSON.stringify({
        id: data.employeeId,
        name: data.name,
        role: data.role,
      }));
      setUser({ id: data.employeeId, name: data.name, role: data.role });
      return { success: true };
    } catch (err) {
      return { success: false, message: err.response?.data?.message || 'Login failed' };
    } finally {
      setLoading(false);
    }
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
  };

  const isAdmin = user?.role === 'Admin';
  const isExplorer = user?.role === 'Explorer';

  return (
    <AuthContext.Provider value={{ user, login, logout, loading, isAdmin, isExplorer, activeDaybookDate, setActiveDaybookDate }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
};
