import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { daybookService, reportService } from '../services/dataService';
import StatCard from '../components/StatCard';
import { IndianRupee, TrendingUp, Wallet, Receipt, CreditCard, Smartphone } from 'lucide-react';

export default function DashboardPage() {
  const { user, isAdmin } = useAuth();
  const [daybook, setDaybook] = useState(null);
  const [dailySummary, setDailySummary] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const today = new Date().toISOString().split('T')[0];

      // Load current employee's daybook
      const { data } = await daybookService.get(today);
      setDaybook(data);

      // Admin gets all-employees summary
      if (isAdmin) {
        const { data: summary } = await reportService.daily(today);
        setDailySummary(summary);
      }
    } catch (err) {
      console.error('Failed to load dashboard', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center h-64 text-gray-400">Loading...</div>;
  }

  const summary = isAdmin && dailySummary ? {
    totalSales: dailySummary.grandTotalSales,
    totalCash: dailySummary.grandTotalCash,
    totalCard: dailySummary.grandTotalCard,
    totalUpi: dailySummary.grandTotalUpi,
    totalExpenses: dailySummary.grandTotalExpenses,
    employeeCount: dailySummary.employees?.length || 0,
  } : {
    totalSales: daybook?.totalSales || 0,
    totalCash: daybook?.totalCashCollected || 0,
    totalCard: daybook?.totalCardCollected || 0,
    totalUpi: daybook?.totalUpiCollected || 0,
    totalExpenses: daybook?.totalExpenses || 0,
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-2xl font-bold text-gray-900">
          {isAdmin ? 'Business Dashboard' : `Welcome, ${user?.name}`}
        </h2>
        <p className="text-sm text-gray-500 mt-1">Today's overview</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 mb-8">
        <StatCard title="Total Sales" value={summary.totalSales} icon={TrendingUp} color="primary" />
        <StatCard title="Cash Collected" value={summary.totalCash} icon={Wallet} color="success" />
        <StatCard title="Card Payments" value={summary.totalCard} icon={CreditCard} color="accent" />
        <StatCard title="UPI Payments" value={summary.totalUpi} icon={Smartphone} color="warning" />
        <StatCard title="Expenses" value={summary.totalExpenses} icon={Receipt} color="danger" />
        <StatCard
          title="Closing Balance"
          value={daybook?.closingBalance || 0}
          icon={IndianRupee}
          color="primary"
          subtitle="Cash in hand"
        />
      </div>

      {/* Quick summary of today's transactions */}
      {daybook && daybook.sales?.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">
            {isAdmin ? "Today's Transactions (All Employees)" : "Your Today's Transactions"}
          </h3>
          <div className="space-y-3">
            {daybook.sales.slice(0, 5).map(sale => (
              <div key={sale.id} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                <div>
                  <p className="text-sm font-medium text-gray-700">{sale.serviceTypeName}</p>
                  <p className="text-xs text-gray-400">
                    {sale.customerName || 'Walk-in'} {sale.vehicleNumber ? `• ${sale.vehicleNumber}` : ''}
                  </p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-gray-900">₹{sale.amount.toLocaleString('en-IN')}</p>
                  <span className={`text-xs px-2 py-0.5 rounded-full ${
                    sale.paymentMode === 'Cash' ? 'bg-green-100 text-green-700' :
                    sale.paymentMode === 'Card' ? 'bg-blue-100 text-blue-700' :
                    'bg-yellow-100 text-yellow-700'
                  }`}>
                    {sale.paymentMode}
                  </span>
                </div>
              </div>
            ))}
            {daybook.sales.length > 5 && (
              <p className="text-xs text-gray-400 text-center pt-2">
                +{daybook.sales.length - 5} more transactions
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
