import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { daybookService, reportService } from '../services/dataService';
import StatCard from '../components/StatCard';
import LoadingSpinner from '../components/LoadingSpinner';
import { IndianRupee, TrendingUp, Wallet, Receipt, CreditCard, Smartphone, CalendarDays, Clock, ArrowUpRight, ArrowDownRight } from 'lucide-react';

const MONTH_NAMES = ['January','February','March','April','May','June','July','August','September','October','November','December'];

function getGreeting() {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}

export default function DashboardPage() {
  const { user, isAdmin } = useAuth();
  const [todayDaybook, setTodayDaybook] = useState(null);
  const [monthlyData, setMonthlyData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const now = new Date();
      const year = now.getFullYear();
      const month = now.getMonth() + 1;
      const today = now.toISOString().split('T')[0];
      const firstOfMonth = `${year}-${String(month).padStart(2, '0')}-01`;

      // Always load today's daybook for "today at a glance"
      const { data: db } = await daybookService.get(today);
      setTodayDaybook(db);

      // Monthly MTD summary
      if (isAdmin) {
        const { data: monthly } = await reportService.monthly(year, month);
        setMonthlyData({
          totalSales: monthly.grandTotalSales,
          totalCash: monthly.grandTotalCash,
          totalExpenses: monthly.grandTotalExpenses,
          daysWithData: monthly.dailyTotals?.length || 0,
          dailyTotals: monthly.dailyTotals || [],
        });
      } else {
        const { data: empReport } = await reportService.employee(user.id, firstOfMonth, today);
        setMonthlyData({
          totalSales: empReport?.totalSales || 0,
          totalCash: empReport?.totalCash || 0,
          totalExpenses: empReport?.totalExpenses || 0,
          daysWithData: empReport?.entries?.length || 0,
          dailyTotals: empReport?.entries || [],
        });
      }
    } catch (err) {
      console.error('Failed to load dashboard', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <LoadingSpinner message="Loading dashboard..." />;
  }

  const now = new Date();
  const monthLabel = `${MONTH_NAMES[now.getMonth()]} ${now.getFullYear()}`;

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold text-gray-900">
          {isAdmin ? 'Business Dashboard' : `${getGreeting()}, ${user?.name}`}
        </h2>
        <div className="flex items-center gap-2 mt-1">
          <CalendarDays className="w-4 h-4 text-gray-400" />
          <p className="text-sm text-gray-500">Month-to-date: <span className="font-medium text-gray-700">{monthLabel}</span></p>
        </div>
      </div>

      {/* Monthly MTD Cards */}
      <div>
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">This Month So Far</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
          <StatCard title="Monthly Sales" value={monthlyData?.totalSales || 0} icon={TrendingUp} color="primary" />
          <StatCard title="Monthly Cash" value={monthlyData?.totalCash || 0} icon={Wallet} color="success" />
          <StatCard title="Monthly Expenses" value={monthlyData?.totalExpenses || 0} icon={Receipt} color="danger" />
          <StatCard title="Monthly Pending" value={monthlyData?.grandTotalPending || monthlyData?.totalPending || 0} icon={Clock} color="danger" />
          <StatCard title="Net Income" value={(monthlyData?.totalSales || 0) - (monthlyData?.totalExpenses || 0)} icon={IndianRupee} color="accent" />
        </div>
      </div>

      {/* Today at a glance */}
      <div>
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Today at a Glance</h3>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-7 gap-3">
          <StatCard title="Today's Sales" value={todayDaybook?.totalSales || 0} icon={TrendingUp} color="success" />
          <StatCard title="Today's Cash" value={todayDaybook?.totalCashCollected || 0} icon={Wallet} color="success" />
          <StatCard title="Today's Card" value={todayDaybook?.totalCardCollected || 0} icon={CreditCard} color="accent" />
          <StatCard title="Today's UPI" value={todayDaybook?.totalUpiCollected || 0} icon={Smartphone} color="warning" />
          <StatCard title="Today's Pending" value={todayDaybook?.totalPendingCollected || 0} icon={Clock} color="danger" />
          <StatCard title="Today's Expenses" value={todayDaybook?.totalExpenses || 0} icon={Receipt} color="danger" />
          <StatCard title="Cash in Hand" value={todayDaybook?.closingBalance || 0} icon={IndianRupee} color="primary" />
        </div>
      </div>

      {/* Monthly trend table */}
      {monthlyData?.dailyTotals?.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">{monthLabel} — Daily Breakdown</h3>
          <div className="overflow-auto max-h-72">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-white">
                <tr className="border-b border-gray-100 text-left text-gray-500 text-xs uppercase">
                  <th className="pb-3 font-medium">Date</th>
                  <th className="pb-3 font-medium text-right">Sales</th>
                  <th className="pb-3 font-medium text-right">Cash</th>
                  <th className="pb-3 font-medium text-right">Expenses</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {[...monthlyData.dailyTotals].reverse().map((row, i) => (
                  <tr key={i} className="hover:bg-gray-50/50 transition-colors">
                    <td className="py-2.5 text-gray-600">
                      {new Date((row.date || row.date) + 'T00:00:00').toLocaleDateString('en-IN', {
                        weekday: 'short', day: 'numeric', month: 'short'
                      })}
                    </td>
                    <td className="py-2.5 text-right font-medium text-gray-800">₹{(row.totalSales).toLocaleString('en-IN')}</td>
                    <td className="py-2.5 text-right text-gray-600">₹{(row.totalCash).toLocaleString('en-IN')}</td>
                    <td className="py-2.5 text-right text-danger">₹{(row.totalExpenses).toLocaleString('en-IN')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Today's recent transactions */}
      {todayDaybook?.sales?.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-semibold text-gray-900">
              {isAdmin ? "Today's Transactions" : "Your Transactions Today"}
            </h3>
            <span className="text-xs bg-gray-100 text-gray-600 px-2.5 py-1 rounded-full font-medium">
              {todayDaybook.sales.length} total
            </span>
          </div>
          <div className="space-y-1">
            {todayDaybook.sales.slice(0, 5).map(sale => (
              <div key={sale.id} className="flex items-center justify-between py-3 px-3 -mx-3 rounded-lg hover:bg-gray-50 transition-colors">
                <div>
                  <p className="text-sm font-medium text-gray-700">{sale.serviceTypeName}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {sale.customerName || 'Walk-in'}{sale.vehicleNumber ? ` • ${sale.vehicleNumber}` : ''}
                  </p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-gray-900">₹{sale.amount.toLocaleString('en-IN')}</p>
                  <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                    sale.paymentMode === 'Cash' ? 'bg-green-100 text-green-700' :
                    sale.paymentMode === 'Card' ? 'bg-blue-100 text-blue-700' :
                    sale.paymentMode === 'UPI' ? 'bg-yellow-100 text-yellow-700' :
                    'bg-red-100 text-red-700'
                  }`}>
                    {sale.paymentMode}
                  </span>
                </div>
              </div>
            ))}
            {todayDaybook.sales.length > 5 && (
              <p className="text-xs text-gray-400 text-center pt-3 border-t border-gray-100 mt-2">
                +{todayDaybook.sales.length - 5} more transactions
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
