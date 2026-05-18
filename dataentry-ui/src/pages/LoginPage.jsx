import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import toast from 'react-hot-toast';
import { Eye, EyeOff, Loader2, Compass } from 'lucide-react';
import logo from '../assets/logo.jpg';

export default function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const { login, loading, exploreAsGuest } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    const result = await login(username, password);
    if (result.success) {
      toast.success('Welcome back!');
      navigate('/admin/dashboard');
    } else {
      toast.error(result.message);
    }
  };

  const handleExplore = () => {
    exploreAsGuest();
    navigate('/admin/dashboard');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-dark via-primary to-primary-light flex items-center justify-center p-4 relative overflow-hidden">
      {/* Background decorative elements */}
      <div className="absolute inset-0 overflow-hidden">
        <div className="absolute -top-40 -right-40 w-80 h-80 bg-white/5 rounded-full" />
        <div className="absolute -bottom-40 -left-40 w-96 h-96 bg-white/5 rounded-full" />
        <div className="absolute top-1/4 left-1/4 w-64 h-64 bg-white/3 rounded-full" />
      </div>

      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-8 relative animate-slide-up">
        <div className="text-center mb-8">
          <img src={logo} alt="Pitstop logo" className="w-20 h-20 rounded-2xl object-cover mx-auto mb-4 shadow-lg ring-4 ring-gray-100" />
          <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Pitstop</h1>
          <p className="text-sm text-gray-500 mt-1">Detailing Studio Management</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">Username</label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              autoFocus
              className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm"
              placeholder="Enter your username"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">Password</label>
            <div className="relative">
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                className="w-full px-4 py-3 pr-11 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm"
                placeholder="Enter your password"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-gray-400 hover:text-gray-600 cursor-pointer"
                tabIndex={-1}
              >
                {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 bg-primary text-white rounded-xl font-medium hover:bg-primary-dark transition-all disabled:opacity-50 cursor-pointer shadow-lg shadow-primary/25 hover:shadow-xl hover:shadow-primary/30 active:scale-[0.98] flex items-center justify-center gap-2"
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                Signing in...
              </>
            ) : (
              'Sign In'
            )}
          </button>
        </form>

        <div className="flex items-center gap-3 my-5">
          <div className="flex-1 h-px bg-gray-200" />
          <span className="text-xs text-gray-400 font-medium">or</span>
          <div className="flex-1 h-px bg-gray-200" />
        </div>

        <button
          type="button"
          onClick={handleExplore}
          className="w-full py-3 border-2 border-dashed border-amber-300 text-amber-700 bg-amber-50 rounded-xl font-medium hover:bg-amber-100 hover:border-amber-400 transition-all cursor-pointer flex items-center justify-center gap-2 group"
        >
          <Compass className="w-4 h-4 group-hover:rotate-12 transition-transform" />
          Explore with Demo Data
        </button>
        <p className="text-center text-xs text-amber-600/80 mt-2">
          Browse all features using sample data — nothing is saved
        </p>

        <p className="text-center text-xs text-gray-400 mt-6">
          Secure access for authorized personnel only
        </p>
      </div>
    </div>
  );
}
