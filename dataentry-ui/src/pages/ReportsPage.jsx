import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { reportService, employeeService, adminService } from '../services/dataService';
import DataTable from '../components/DataTable';
import StatCard from '../components/StatCard';
import Modal from '../components/Modal';
import LoadingSpinner from '../components/LoadingSpinner';
import toast from 'react-hot-toast';
import { TrendingUp, Wallet, Receipt, Download, Trash2, Banknote, IndianRupee } from 'lucide-react';

export default function ReportsPage() {
  const { isAdmin, user } = useAuth();
  const [tab, setTab] = useState('daily');
  const [date, setDate] = useState(new Date().toISOString().split('T')[0]);
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [employees, setEmployees] = useState([]);
  const [selectedEmployee, setSelectedEmployee] = useState('');
  const [fromDate, setFromDate] = useState(() => {
    const d = new Date(); d.setDate(d.getDate() - 30);
    return d.toISOString().split('T')[0];
  });
  const [toDate, setToDate] = useState(new Date().toISOString().split('T')[0]);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);

  // Export modal
  const [showExportModal, setShowExportModal] = useState(false);
  const [exportFrom, setExportFrom] = useState(() => {
    const d = new Date(); d.setDate(d.getDate() - 30);
    return d.toISOString().split('T')[0];
  });
  const [exportTo, setExportTo] = useState(new Date().toISOString().split('T')[0]);
  const [exporting, setExporting] = useState(false);
  const [resetting, setResetting] = useState(false);

  useEffect(() => {
    if (isAdmin) {
      employeeService.getAll().then(r => {
        setEmployees(r.data);
        if (r.data.length > 0) setSelectedEmployee(r.data[0].id);
      });
    } else {
      setSelectedEmployee(user.id);
    }
  }, []);

  useEffect(() => { loadReport(); }, [tab, date, month, selectedEmployee, fromDate, toDate]);

  const loadReport = async () => {
    setLoading(true);
    try {
      if (tab === 'daily') {
        const { data } = await reportService.daily(date);
        setData(data);
      } else if (tab === 'monthly') {
        const [y, m] = month.split('-');
        const { data } = await reportService.monthly(Number(y), Number(m));
        setData(data);
      } else if (tab === 'employee' && selectedEmployee) {
        const { data } = await reportService.employee(selectedEmployee, fromDate, toDate);
        setData(data);
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleExportCsv = async () => {
    setExporting(true);
    try {
      const response = await reportService.exportCsv(exportFrom, exportTo);
      const url = URL.createObjectURL(new Blob([response.data], { type: 'text/csv' }));
      const a = document.createElement('a');
      a.href = url;
      a.download = `daybook_export_${exportFrom}_${exportTo}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success('Export downloaded');
      setShowExportModal(false);
    } catch {
      toast.error('Export failed');
    } finally {
      setExporting(false);
    }
  };

  const handleReset = async () => {
    const confirmed = window.confirm(
      'WARNING: This will permanently delete ALL daybook entries, sales, and expenses. This cannot be undone.'
    );
    if (!confirmed) return;
    const typed = window.prompt('Type RESET to confirm:');
    if (typed !== 'RESET') { toast.error('Reset cancelled — nothing was deleted'); return; }
    setResetting(true);
    try {
      await adminService.resetData();
      toast.success('All daybook data has been reset');
      setData(null);
    } catch {
      toast.error('Reset failed');
    } finally {
      setResetting(false);
    }
  };

  const tabs = [
    { id: 'monthly', label: 'Monthly Summary' },
    { id: 'employee', label: 'Employee Report' },
  ];

  return (
    <div>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
        <h2 className="text-2xl font-bold text-gray-900">Reports</h2>
        {isAdmin && (
          <div className="flex items-center gap-2">
            <button
              onClick={() => setShowExportModal(true)}
              className="flex items-center gap-2 bg-primary text-white px-4 py-2.5 rounded-xl text-sm font-medium hover:bg-primary-dark transition-all cursor-pointer shadow-sm hover:shadow-md"
            >
              <Download className="w-4 h-4" /> Export CSV
            </button>
            <button
              onClick={handleReset}
              disabled={resetting}
              className="flex items-center gap-2 bg-danger text-white px-4 py-2.5 rounded-xl text-sm font-medium hover:bg-red-600 transition-all cursor-pointer disabled:opacity-60 shadow-sm"
            >
              <Trash2 className="w-4 h-4" /> {resetting ? 'Resetting…' : 'Reset All Data'}
            </button>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-gray-100 p-1 rounded-xl w-fit mb-6">
        {tabs.map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`px-4 py-2.5 text-sm font-medium rounded-lg transition-all cursor-pointer ${
              tab === t.id ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3 mb-6">
        {tab === 'daily' && (
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
        )}
        {tab === 'monthly' && (
          <input type="month" value={month} onChange={(e) => setMonth(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
        )}
        {tab === 'employee' && (
          <>
            {isAdmin && (
              <select value={selectedEmployee} onChange={(e) => setSelectedEmployee(e.target.value)}
                className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none">
                {employees.map(emp => (
                  <option key={emp.id} value={emp.id}>{emp.name}</option>
                ))}
              </select>
            )}
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            <span className="text-gray-400 text-sm">to</span>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
          </>
        )}
      </div>

      {loading && <div className="text-center text-gray-400 py-12">Loading...</div>}

      {!loading && data && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
            <StatCard
              title={tab === 'monthly' ? 'Monthly Sales' : tab === 'daily' ? 'Daily Sales' : 'Total Sales'}
              value={data.grandTotalSales ?? data.totalSales ?? 0} icon={TrendingUp} color="primary" />
            <StatCard
              title={tab === 'monthly' ? 'Monthly Cash' : tab === 'daily' ? 'Daily Cash' : 'Total Cash'}
              value={data.grandTotalCash ?? data.totalCash ?? 0} icon={Wallet} color="success" />
            <StatCard
              title={tab === 'monthly' ? 'Monthly Expenses' : tab === 'daily' ? 'Daily Expenses' : 'Total Expenses'}
              value={data.grandTotalExpenses ?? data.totalExpenses ?? 0} icon={Receipt} color="danger" />
          </div>

          {/* Monthly-only salary & net income cards */}
          {tab === 'monthly' && (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
              <StatCard title="Total Salaries Paid" value={data.grandTotalSalaries ?? 0} icon={Banknote} color="warning" />
              <StatCard title="Monthly Pending" value={data.grandTotalPending ?? 0} icon={Receipt} color="danger" />
              <div className="bg-white rounded-xl border border-gray-200 p-5 hover:shadow-md transition-shadow">
                <div className="flex items-center justify-between mb-3">
                  <p className="text-sm font-medium text-gray-500">Net Monthly Income</p>
                  <div className={`p-2 rounded-lg ${(data.netIncome ?? 0) >= 0 ? 'bg-success/10 text-success' : 'bg-danger/10 text-danger'}`}>
                    <IndianRupee className="w-5 h-5" />
                  </div>
                </div>
                <p className={`text-2xl font-bold ${(data.netIncome ?? 0) >= 0 ? 'text-success' : 'text-danger'}`}>
                  ₹{(data.netIncome ?? 0).toLocaleString('en-IN')}
                </p>
                <p className="text-xs text-gray-400 mt-1">Sales − Expenses − Salaries</p>
              </div>
            </div>
          )}

          {/* Data table */}
          {tab === 'daily' && data.employees && (
            <DataTable
              columns={[
                { header: 'Employee', accessor: 'employeeName' },
                { header: 'Opening', render: r => `₹${r.openingBalance.toLocaleString('en-IN')}` },
                { header: 'Sales', render: r => `₹${r.totalSales.toLocaleString('en-IN')}` },
                { header: 'Cash', render: r => `₹${r.totalCash.toLocaleString('en-IN')}` },
                { header: 'Card', render: r => `₹${r.totalCard.toLocaleString('en-IN')}` },
                { header: 'UPI', render: r => `₹${r.totalUpi.toLocaleString('en-IN')}` },
                { header: 'Expenses', render: r => `₹${r.totalExpenses.toLocaleString('en-IN')}` },
                { header: 'Closing', render: r => <span className="font-bold">₹{r.closingBalance.toLocaleString('en-IN')}</span> },
              ]}
              data={data.employees}
              emptyMessage="No data for this date"
            />
          )}

          {tab === 'monthly' && data.dailyTotals && (
            <DataTable
              columns={[
                { header: 'Date', render: r => r.date },
                { header: 'Sales', render: r => `₹${r.totalSales.toLocaleString('en-IN')}` },
                { header: 'Cash', render: r => `₹${r.totalCash.toLocaleString('en-IN')}` },
                { header: 'Expenses', render: r => `₹${r.totalExpenses.toLocaleString('en-IN')}` },
                { header: 'Transactions', render: r => r.transactionCount },
              ]}
              data={data.dailyTotals}
              emptyMessage="No data for this month"
            />
          )}

          {tab === 'employee' && data.entries && (
            <DataTable
              columns={[
                { header: 'Date', render: r => r.date },
                { header: 'Opening', render: r => `₹${r.openingBalance.toLocaleString('en-IN')}` },
                { header: 'Sales', render: r => `₹${r.totalSales.toLocaleString('en-IN')}` },
                { header: 'Cash', render: r => `₹${r.totalCash.toLocaleString('en-IN')}` },
                { header: 'Expenses', render: r => `₹${r.totalExpenses.toLocaleString('en-IN')}` },
                { header: 'Closing', render: r => <span className="font-bold">₹{r.closingBalance.toLocaleString('en-IN')}</span> },
                { header: 'Txns', render: r => r.transactionCount },
              ]}
              data={data.entries}
              emptyMessage="No data for this period"
            />
          )}
        </>
      )}

      {/* Export CSV Modal */}
      <Modal isOpen={showExportModal} onClose={() => setShowExportModal(false)} title="Export Data as CSV">
        <div className="space-y-4">
          <p className="text-sm text-gray-500">Select a date range to export all sales and expenses as a CSV file.</p>
          <div className="flex items-center gap-3">
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">From</label>
              <input
                type="date"
                value={exportFrom}
                onChange={(e) => setExportFrom(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
            <span className="text-gray-400 text-sm mt-5">to</span>
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">To</label>
              <input
                type="date"
                value={exportTo}
                onChange={(e) => setExportTo(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowExportModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button
              onClick={handleExportCsv}
              disabled={exporting || !exportFrom || !exportTo}
              className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer disabled:opacity-60"
            >
              <Download className="w-4 h-4" /> {exporting ? 'Exporting…' : 'Download CSV'}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
